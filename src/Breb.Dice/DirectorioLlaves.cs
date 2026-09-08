namespace Breb.Dice;

/// <summary>
/// El directorio en memoria. Es deliberadamente trivial: si tuviera una base
/// de datos detras, el costo de la consulta taparia lo que queremos medir.
///
/// La comparacion REST vs gRPC solo es honesta si AMBOS ejecutan exactamente
/// la misma logica. Lo unico que cambia entre los dos es el transporte y la
/// serializacion — que es justamente lo que estamos comparando.
/// </summary>
public sealed class DirectorioLlaves
{
    private readonly Dictionary<string, Registro> _porLlave = new(StringComparer.OrdinalIgnoreCase);

    public DirectorioLlaves()
    {
        // 10 000 llaves sinteticas: 3001000000..3001009999
        // Suficiente para que la busqueda sea real sin dominar la medicion.
        var entidades = new[] { "BANCOLOMBIA", "DAVIVIENDA", "NEQUI", "BBVA", "NUBANK" };
        for (var i = 0; i < 10_000; i++)
        {
            var llave = (3001000000L + i).ToString();
            _porLlave[llave] = new Registro(
                CuentaId: Guid.NewGuid().ToString(),
                Entidad: entidades[i % entidades.Length],
                TipoLlave: "CELULAR");
        }
    }

    public Registro? Resolver(string llave)
        => _porLlave.TryGetValue(llave, out var r) ? r : null;

    public int Total => _porLlave.Count;

    public sealed record Registro(string CuentaId, string Entidad, string TipoLlave);
}
