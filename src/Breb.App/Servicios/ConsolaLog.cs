using System.Collections.ObjectModel;
using System.Text;

namespace Breb.App.Servicios;

/// <summary>
/// Consola de diagnóstico en vivo. Un único servicio (singleton) que
/// cualquier ViewModel puede inyectar para dejar constancia, con marca de
/// tiempo, de CADA paso: petición HTTP, código de respuesta, token guardado,
/// error de red. La pantalla de Consola hace binding a <see cref="Lineas"/>
/// y así se demuestra EN VIVO qué está haciendo la app.
/// </summary>
public sealed class ConsolaLog
{
    // ObservableCollection para que la UI se actualice sola con cada línea.
    public ObservableCollection<LineaLog> Lineas { get; } = new();

    public event Action? Cambio;

    public void Info(string mensaje)    => Agregar("INFO", mensaje);
    public void Exito(string mensaje)   => Agregar("OK",   mensaje);
    public void Error(string mensaje)   => Agregar("ERR",  mensaje);
    public void Peticion(string metodo, string ruta) => Agregar("HTTP", $"→ {metodo} {ruta}");
    public void Respuesta(int codigo, string detalle = "") =>
        Agregar(codigo is >= 200 and < 300 ? "OK" : "ERR",
                $"← {codigo}{(string.IsNullOrEmpty(detalle) ? "" : " · " + detalle)}");

    public void Limpiar()
    {
        EnHilo(() => Lineas.Clear());
        Cambio?.Invoke();
    }

    private void Agregar(string nivel, string mensaje)
    {
        var linea = new LineaLog(DateTime.Now, nivel, mensaje);
        // Las actualizaciones de UI deben ir en el hilo principal.
        EnHilo(() =>
        {
            Lineas.Add(linea);
            // Mantener la consola acotada: últimas 200 líneas.
            while (Lineas.Count > 200) Lineas.RemoveAt(0);
        });
        Cambio?.Invoke();
    }

    private static void EnHilo(Action accion)
    {
        if (MainThread.IsMainThread) accion();
        else MainThread.BeginInvokeOnMainThread(accion);
    }

    /// <summary>Vuelca toda la consola a texto (para copiar o compartir).</summary>
    public string ComoTexto()
    {
        var sb = new StringBuilder();
        foreach (var l in Lineas) sb.AppendLine(l.ToString());
        return sb.ToString();
    }
}

public sealed record LineaLog(DateTime Momento, string Nivel, string Mensaje)
{
    public string HoraTexto => Momento.ToString("HH:mm:ss.fff");
    public string Texto => $"[{HoraTexto}] {Nivel,-4} {Mensaje}";

    // Color según nivel, para que la consola se lea de un vistazo.
    public Color ColorNivel => Nivel switch
    {
        "OK"   => Colors.MediumSeaGreen,
        "ERR"  => Colors.IndianRed,
        "HTTP" => Colors.CornflowerBlue,
        _       => Colors.Gray
    };

    public override string ToString() => Texto;
}
