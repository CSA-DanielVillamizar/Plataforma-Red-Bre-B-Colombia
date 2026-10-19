using System.Net.Http.Headers;
using System.Net.Http.Json;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Envuelve el HttpClient registrado en MauiProgram.cs. Un solo lugar que
/// sabe hablar con Breb.Cuentas; los ViewModels no conocen rutas ni JSON.
/// Cada llamada queda registrada en la <see cref="ConsolaLog"/> y el token
/// se lee del almacén seguro, nunca de una variable suelta.
/// </summary>
public sealed class ClienteBrebApi(HttpClient http, ConsolaLog consola, SesionSegura sesion)
{
    // ── Login: POST /token ──────────────────────────────────────────────
    public async Task<RespuestaToken> ObtenerTokenAsync(string usuario, string clave)
    {
        consola.Peticion("POST", "/token");
        consola.Info($"usuario={usuario}");

        var respuesta = await http.PostAsJsonAsync("/token", new SolicitudToken(usuario, clave));
        consola.Respuesta((int)respuesta.StatusCode);

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Login rechazado: HTTP {(int)respuesta.StatusCode}");

        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>()
                    ?? throw new InvalidOperationException("Respuesta de /token vacía.");

        await sesion.GuardarAsync(token.Token, token.Rol);
        consola.Exito($"token guardado en SecureStorage · rol={token.Rol} · expira en {token.DuracionMinutos} min");
        return token;
    }

    // ── Saldo: GET /cuentas/{id}/saldo ──────────────────────────────────
    public async Task<RespuestaSaldo> ConsultarSaldoAsync(Guid cuentaId)
    {
        consola.Peticion("GET", $"/cuentas/{cuentaId}/saldo");
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"/cuentas/{cuentaId}/saldo");
        await AgregarTokenAsync(solicitud);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Saldo rechazado: HTTP {(int)respuesta.StatusCode}");

        var saldo = await respuesta.Content.ReadFromJsonAsync<RespuestaSaldo>()
                    ?? throw new InvalidOperationException("Respuesta de /saldo vacía.");
        consola.Exito($"disponible={saldo.SaldoDisponible:N2} · retenido={saldo.SaldoRetenido:N2}");
        return saldo;
    }

    // ── Transferencia (retener): POST /cuentas/{id}/retener?montoUVB=X ──
    public async Task<RespuestaRetencion> RetenerAsync(Guid cuentaId, decimal montoUVB)
    {
        var ruta = $"/cuentas/{cuentaId}/retener?montoUVB={montoUVB.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        consola.Peticion("POST", ruta);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        await AgregarTokenAsync(solicitud);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("403: tu rol no puede retener. Entra como 'ana.operadora' (rol operador).");
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Retención rechazada: HTTP {(int)respuesta.StatusCode}");

        var retencion = await respuesta.Content.ReadFromJsonAsync<RespuestaRetencion>()
                        ?? throw new InvalidOperationException("Respuesta de /retener vacía.");
        consola.Exito($"transferencia creada: {retencion.TransferenciaId}");
        return retencion;
    }

    // Igual que RetenerAsync, pero con un token EFÍMERO del usuario indicado
    // (p.ej. el operador), sin tocar la sesión que haya iniciado el usuario.
    // Así la pantalla de Transferencia usa el rol correcto para cada paso.
    public async Task<RespuestaRetencion> RetenerComoAsync(Guid cuentaId, decimal montoUVB, string usuario, string clave)
    {
        var token = await TokenEfimeroAsync(usuario, clave);
        var ruta = $"/cuentas/{cuentaId}/retener?montoUVB={montoUVB.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        consola.Peticion("POST", ruta);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException($"403: «{usuario}» no tiene permiso para retener (se necesita rol operador).");
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Retención rechazada: HTTP {(int)respuesta.StatusCode}");

        var retencion = await respuesta.Content.ReadFromJsonAsync<RespuestaRetencion>()
                        ?? throw new InvalidOperationException("Respuesta de /retener vacía.");
        consola.Exito($"transferencia creada: {retencion.TransferenciaId}");
        return retencion;
    }

    // ── Confirmar abono: POST /transferencias/{id}/confirmar-abono ──────
    public async Task ConfirmarAbonoAsync(Guid transferenciaId)
    {
        var ruta = $"/transferencias/{transferenciaId}/confirmar-abono";
        consola.Peticion("POST", ruta);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        await AgregarTokenAsync(solicitud);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("403: confirmar abono requiere rol 'banco' (core-bancario).");
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Confirmación rechazada: HTTP {(int)respuesta.StatusCode}");

        consola.Exito("abono confirmado · la saga completará la transferencia");
    }

    // Confirmar abono con un token EFÍMERO del usuario indicado (el banco).
    public async Task ConfirmarAbonoComoAsync(Guid transferenciaId, string usuario, string clave)
    {
        var token = await TokenEfimeroAsync(usuario, clave);
        var ruta = $"/transferencias/{transferenciaId}/confirmar-abono";
        consola.Peticion("POST", ruta);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException($"403: «{usuario}» no puede confirmar abonos (se necesita rol banco).");
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Confirmación rechazada: HTTP {(int)respuesta.StatusCode}");

        consola.Exito("abono confirmado · la saga completará la transferencia");
    }

    // Pide un token para un usuario concreto SIN guardarlo en la sesión.
    // Sirve para las operaciones que exigen un rol específico (operador/banco)
    // independientemente de con quién haya iniciado sesión el usuario.
    private async Task<string> TokenEfimeroAsync(string usuario, string clave)
    {
        consola.Peticion("POST", "/token");
        consola.Info($"token efímero para {usuario}");
        var resp = await http.PostAsJsonAsync("/token", new SolicitudToken(usuario, clave));
        consola.Respuesta((int)resp.StatusCode);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"No se pudo autenticar «{usuario}»: HTTP {(int)resp.StatusCode}");
        var token = await resp.Content.ReadFromJsonAsync<RespuestaToken>()
                    ?? throw new InvalidOperationException("Respuesta de /token vacía.");
        consola.Exito($"token {token.Rol} obtenido para {usuario}");
        return token.Token;
    }

    // ── Diagnóstico: GET /quien-soy ─────────────────────────────────────
    public async Task<RespuestaQuienSoy> QuienSoyAsync()
    {
        consola.Peticion("GET", "/quien-soy");
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/quien-soy");
        await AgregarTokenAsync(solicitud);

        var respuesta = await http.SendAsync(solicitud);
        consola.Respuesta((int)respuesta.StatusCode);

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"/quien-soy rechazó: HTTP {(int)respuesta.StatusCode}");

        var quien = await respuesta.Content.ReadFromJsonAsync<RespuestaQuienSoy>()
                    ?? throw new InvalidOperationException("Respuesta de /quien-soy vacía.");
        consola.Exito($"soy {quien.Nombre} · {quien.Afirmaciones.Count} afirmaciones");
        return quien;
    }

    // Añade el encabezado Authorization: Bearer <token> leyendo del almacén seguro.
    private async Task AgregarTokenAsync(HttpRequestMessage solicitud)
    {
        var token = await sesion.ObtenerTokenAsync();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("No hay sesión. Inicia sesión primero (pantalla Login).");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
