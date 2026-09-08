using Breb.Dice.Grpc;
using Grpc.Core;

namespace Breb.Dice;

/// <summary>
/// La cara gRPC del directorio.
///
/// Fijese que NO hay atributos de ruta, ni verbos HTTP, ni codigos de estado.
/// El contrato vive en dice.proto y el compilador genera esta clase base.
/// Si alguien cambia el .proto y no actualiza la implementacion, EL PROYECTO
/// NO COMPILA — el contrato deja de ser documentacion y pasa a ser codigo.
///
/// Esa es la diferencia de fondo con REST: en REST el contrato (OpenAPI) se
/// escribe aparte y nada obliga a que coincida con la implementacion.
/// </summary>
public sealed class DirectorioGrpcService : Directorio.DirectorioBase
{
    private readonly DirectorioLlaves _directorio;

    public DirectorioGrpcService(DirectorioLlaves directorio) => _directorio = directorio;

    public override Task<ResolverResponse> Resolver(ResolverRequest request, ServerCallContext context)
    {
        var r = _directorio.Resolver(request.Llave);

        return Task.FromResult(r is null
            ? new ResolverResponse { Encontrada = false }
            : new ResolverResponse
            {
                Encontrada = true,
                CuentaId = r.CuentaId,
                Entidad = r.Entidad,
                TipoLlave = r.TipoLlave
            });
    }

    /// <summary>
    /// Resolver varias llaves en UNA llamada.
    /// Sobre HTTP/1.1 esto obligaria a N peticiones (una por llave) o a
    /// inventar un endpoint especial de lote. Aqui es parte del contrato.
    /// </summary>
    public override Task<ResolverLoteResponse> ResolverLote(
        ResolverLoteRequest request, ServerCallContext context)
    {
        var respuesta = new ResolverLoteResponse();

        foreach (var llave in request.Llaves)
        {
            var r = _directorio.Resolver(llave);
            respuesta.Resultados.Add(r is null
                ? new ResolverResponse { Encontrada = false }
                : new ResolverResponse
                {
                    Encontrada = true,
                    CuentaId = r.CuentaId,
                    Entidad = r.Entidad,
                    TipoLlave = r.TipoLlave
                });
        }

        return Task.FromResult(respuesta);
    }
}
