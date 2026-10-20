using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Carga, color del mensaje y los fallos de red. Las tres pantallas lo heredan
/// para no copiar el mismo try/catch. Los botones se apagan con CanExecute
/// mientras Cargando es verdadero.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    [ObservableProperty]
    private string estado = string.Empty;

    /// <summary>"neutro", "ok" o "error". La vista solo cambia el color.</summary>
    [ObservableProperty]
    private string tono = "neutro";

    [ObservableProperty]
    private bool cargando;

    partial void OnCargandoChanged(bool value) => AlCambiarCargando();

    protected virtual void AlCambiarCargando() { }

    protected bool NoEstaCargando() => !Cargando;

    protected void MarcarError(string mensaje)
    {
        Tono = "error";
        Estado = mensaje;
    }

    protected async Task EjecutarAsync(Func<Task> accion)
    {
        if (Cargando) return;
        Cargando = true;
        Tono = "neutro";
        try
        {
            await accion();
        }
        catch (ExcepcionDeApi ex)
        {
            await AlRechazarAsync(ex);
            MarcarError(MensajesDeApi.Para(ex));
        }
        catch (ExcepcionAlmacenSeguro ex)
        {
            MarcarError(ex.Message);
        }
        catch (TaskCanceledException)
        {
            MarcarError(MensajesDeApi.Timeout);
        }
        catch (HttpRequestException ex)
        {
            MarcarError(MensajesDeApi.SinRed + " " + ex.Message);
        }
        catch (Exception ex)
        {
            MarcarError("Ocurrió un error inesperado: " + ex.Message);
        }
        finally
        {
            Cargando = false;
        }
    }

    /// <summary>401 fuera del login: la sesión guardada ya no sirve.</summary>
    protected virtual Task AlRechazarAsync(ExcepcionDeApi ex) => Task.CompletedTask;

    public static string FormatoUvb(decimal valor) =>
        valor.ToString("N2", Cultura) + " UVB";
}
