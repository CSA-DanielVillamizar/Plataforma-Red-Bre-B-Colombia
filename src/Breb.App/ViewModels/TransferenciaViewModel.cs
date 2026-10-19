using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla 3: Transferencia. Una transferencia real necesita DOS identidades:
///   · el OPERADOR (ana.operadora) retiene los fondos;
///   · el BANCO destino (core-bancario) confirma el abono.
/// La pantalla usa el rol correcto en cada paso automáticamente, así que el
/// flujo completo funciona sin tener que cerrar sesión y volver a entrar.
/// Toda la coreografía la maneja la saga del backend.
/// </summary>
public partial class TransferenciaViewModel(ClienteBrebApi api, ConsolaLog consola) : ObservableObject
{
    // Credenciales de laboratorio para cada rol (documentadas, no secretas).
    private const string OperadorUsuario = "ana.operadora";
    private const string OperadorClave   = "Operadora-2026";
    private const string BancoUsuario    = "core-bancario";
    private const string BancoClave      = "CoreBancario-2026";

    [ObservableProperty] private string cuentaOrigen = "11111111-1111-1111-1111-111111111111";
    [ObservableProperty] private string montoTexto = "100";
    [ObservableProperty] private string estado = "Lista para transferir";
    [ObservableProperty] private string transferenciaId = "—";
    [ObservableProperty] private bool cargando;
    [ObservableProperty] private bool hayTransferencia;

    private Guid _ultimaTransferencia = Guid.Empty;

    // ── Paso 1: retener (el OPERADOR dispara la transferencia) ──────────
    [RelayCommand]
    private async Task RetenerAsync()
    {
        if (Cargando) return;
        if (!Guid.TryParse(CuentaOrigen, out var cuenta)) { Estado = "Cuenta origen inválida (GUID)."; return; }
        if (!decimal.TryParse(MontoTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var monto) || monto <= 0)
        { Estado = "El monto debe ser un número mayor que 0."; return; }

        Cargando = true;
        Estado = $"Operador reteniendo {monto} UVB…";
        try
        {
            // Siempre con rol OPERADOR, sin importar quién inició sesión en Login.
            var r = await api.RetenerComoAsync(cuenta, monto, OperadorUsuario, OperadorClave);
            _ultimaTransferencia = r.TransferenciaId;
            TransferenciaId = r.TransferenciaId.ToString();
            HayTransferencia = true;
            Estado = $"✅ Fondos retenidos · {r.MontoUVB} UVB · la saga arrancó. Ahora confirma el abono (paso 2).";
        }
        catch (HttpRequestException ex)
        {
            Estado = $"Sin conexión con la API: {ex.Message}";
            consola.Error($"red: {ex.Message}");
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
        }
        finally { Cargando = false; }
    }

    // ── Paso 2: confirmar abono (el BANCO completa la transferencia) ────
    [RelayCommand]
    private async Task ConfirmarAbonoAsync()
    {
        if (Cargando) return;
        if (_ultimaTransferencia == Guid.Empty) { Estado = "Primero retén fondos (paso 1)."; return; }

        Cargando = true;
        Estado = "Banco confirmando abono…";
        try
        {
            // Siempre con rol BANCO (core-bancario): es quien puede confirmar.
            await api.ConfirmarAbonoComoAsync(_ultimaTransferencia, BancoUsuario, BancoClave);
            Estado = "✅ Abono confirmado por el banco · la saga completó la transferencia.";
            HayTransferencia = false;
            _ultimaTransferencia = Guid.Empty;
        }
        catch (HttpRequestException ex)
        {
            Estado = $"Sin conexión con la API: {ex.Message}";
            consola.Error($"red: {ex.Message}");
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
        }
        finally { Cargando = false; }
    }

    // ── Función extra: demostrar la COMPENSACIÓN de la saga ─────────────
    // Retiene un monto y NO confirma: a los 15 s la saga compensa y reintegra
    // el saldo. Es el corazón del proyecto. Se retiene como operador.
    [RelayCommand]
    private async Task DemoCompensacionAsync()
    {
        if (Cargando) return;
        if (!Guid.TryParse(CuentaOrigen, out var cuenta)) { Estado = "Cuenta origen inválida (GUID)."; return; }

        MontoTexto = "250";
        Cargando = true;
        Estado = "DEMO: operador retiene 250 UVB y NADIE confirma…";
        consola.Info("DEMO compensación: retener 250 UVB y NO confirmar · la saga reintegra a los 15 s");
        try
        {
            var r = await api.RetenerComoAsync(cuenta, 250m, OperadorUsuario, OperadorClave);
            _ultimaTransferencia = r.TransferenciaId;
            TransferenciaId = r.TransferenciaId.ToString();
            HayTransferencia = true;
            Estado = "⏳ Retención hecha SIN confirmar · ve a Saldo en 15 s: la saga ya compensó (retenido vuelve a 0).";
        }
        catch (HttpRequestException ex)
        {
            Estado = $"Sin conexión con la API: {ex.Message}";
            consola.Error($"red: {ex.Message}");
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
        }
        finally { Cargando = false; }
    }
}
