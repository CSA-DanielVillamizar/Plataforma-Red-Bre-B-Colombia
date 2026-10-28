using System.Text.Json;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// La cola local de operaciones pendientes, persistida en disco.
/// <para>
/// <b>No tiene una sola referencia a MAUI.</b> Recibe la carpeta donde
/// escribir y nada más. Eso no es casualidad: es lo que permite probarla con
/// una consola normal, sin abrir la app ni un emulador — y es exactamente el
/// argumento de la clase del 9 de noviembre.
/// </para>
/// <para>
/// El contrato que cumple es corto y es todo lo que hace falta:
/// </para>
/// <list type="number">
///   <item>Lo encolado sobrevive a que el proceso muera.</item>
///   <item>Una operación sale de la cola <b>sólo</b> cuando alguien confirma.</item>
///   <item>Un fallo a mitad de escritura no corrompe la cola.</item>
/// </list>
/// </summary>
public sealed class ColaOperaciones
{
    private const string Archivo = "operaciones-pendientes.json";

    // Las escrituras se serializan. Dos reenvíos que terminen al mismo tiempo
    // podrían leer-modificar-escribir sobre el mismo archivo y uno perdería
    // el cambio del otro. Es el mismo problema de la Semana 5, a escala de
    // un archivo en vez de una fila de PostgreSQL, y se resuelve igual:
    // haciendo fila en vez de confiar en que no choquen.
    private readonly SemaphoreSlim _candado = new(1, 1);
    private readonly string _ruta;

    public ColaOperaciones(string carpeta)
    {
        Directory.CreateDirectory(carpeta);
        _ruta = Path.Combine(carpeta, Archivo);
    }

    /// <summary>Dónde quedó el archivo. Útil para diagnosticar en clase.</summary>
    public string Ruta => _ruta;

    /// <summary>
    /// Guarda la operación en disco. Se llama ANTES de intentar la red: si la
    /// app se cierra entre el toque del usuario y la respuesta del servidor,
    /// la intención ya está a salvo.
    /// <para>
    /// Si la llave ya estaba encolada, no se duplica la entrada. La cola es
    /// idempotente igual que el endpoint: encolar dos veces la misma llave es
    /// encolarla una vez.
    /// </para>
    /// </summary>
    public async Task EncolarAsync(OperacionPendiente op)
    {
        await _candado.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            if (cola.Any(o => o.Llave == op.Llave)) return;
            cola.Add(op);
            await EscribirAsync(cola);
        }
        finally { _candado.Release(); }
    }

    /// <summary>Las operaciones que esperan, de la más vieja a la más nueva.</summary>
    public async Task<IReadOnlyList<OperacionPendiente>> PendientesAsync()
    {
        await _candado.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            return cola.OrderBy(o => o.CreadaEn).ToList();
        }
        finally { _candado.Release(); }
    }

    /// <summary>
    /// Saca la operación de la cola. <b>Sólo se llama cuando el servidor
    /// confirmó</b>, sea con repetida=false o con repetida=true: las dos
    /// significan «esto ya está hecho».
    /// <para>
    /// Y aquí se cierra el círculo con la decisión del servidor de responder
    /// 200 y no 409. Un 409 lo leeríamos como fallo, no borraríamos la
    /// entrada, y la operación quedaría atrapada reintentándose para siempre
    /// algo que ya estaba hecho.
    /// </para>
    /// </summary>
    public async Task ConfirmarAsync(Guid llave)
    {
        await _candado.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            if (cola.RemoveAll(o => o.Llave == llave) > 0)
                await EscribirAsync(cola);
        }
        finally { _candado.Release(); }
    }

    /// <summary>
    /// Suma uno al contador de intentos. Se llama cuando un envío falla, para
    /// poder decirle al usuario cuántas veces se ha intentado en vez de
    /// reintentar en silencio.
    /// </summary>
    public async Task RegistrarIntentoAsync(Guid llave)
    {
        await _candado.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            var i = cola.FindIndex(o => o.Llave == llave);
            if (i < 0) return;
            cola[i] = cola[i] with { Intentos = cola[i].Intentos + 1 };
            await EscribirAsync(cola);
        }
        finally { _candado.Release(); }
    }

    /// <summary>Descarta una operación sin enviarla, porque el usuario lo pidió.</summary>
    public Task DescartarAsync(Guid llave) => ConfirmarAsync(llave);

    // ── Lectura ────────────────────────────────────────────────────────────
    private async Task<List<OperacionPendiente>> LeerAsync()
    {
        if (!File.Exists(_ruta)) return [];
        try
        {
            await using var fs = File.OpenRead(_ruta);
            return await JsonSerializer.DeserializeAsync<List<OperacionPendiente>>(fs) ?? [];
        }
        catch (JsonException)
        {
            // Archivo corrupto. Se prefiere una cola vacía a que la app no
            // arranque: perder operaciones pendientes es malo, pero una app
            // que no abre es peor, y el usuario no tiene forma de arreglarla.
            //
            // Que esto sea casi imposible depende de EscribirAsync, abajo.
            return [];
        }
    }

    // ── Escritura atómica ──────────────────────────────────────────────────
    // Se escribe a un temporal y después se MUEVE sobre el definitivo.
    //
    // Por qué no escribir directo: si el proceso muere a mitad de un
    // File.WriteAllText, el archivo queda con JSON truncado — y la cola
    // completa se vuelve ilegible. Se perderían TODAS las operaciones
    // pendientes, no sólo la que se estaba escribiendo.
    //
    // File.Move con overwrite es atómico en el sistema de archivos: o está
    // el contenido viejo entero, o el nuevo entero. Nunca mitad y mitad.
    // Es la misma idea del Outbox de la Semana 2 —el saldo y el evento se
    // guardan juntos o no se guardan— aplicada a un archivo.
    private async Task EscribirAsync(List<OperacionPendiente> cola)
    {
        var temporal = _ruta + ".tmp";
        await using (var fs = File.Create(temporal))
        {
            await JsonSerializer.SerializeAsync(fs, cola,
                new JsonSerializerOptions { WriteIndented = true });
            await fs.FlushAsync();
        }
        File.Move(temporal, _ruta, overwrite: true);
    }
}
