using Breb.App.Modelos;
using Breb.App.Servicios;

// Arnés de la cola local. Se corre en DOS procesos distintos a propósito:
//
//   dotnet run -- encolar <carpeta>     ← proceso 1: encola y se muere
//   dotnet run -- leer     <carpeta>    ← proceso 2: lee lo que dejó el otro
//
// Si el segundo proceso ve lo que encoló el primero, la cola sobrevive a que
// la app se cierre. Eso es el criterio del E4, comprobado sin abrir la app.

if (args.Length < 2)
{
    Console.WriteLine("uso: encolar|leer|confirmar|intento|corromper <carpeta> [llave]");
    return 1;
}

var orden = args[0];
var cola = new ColaOperaciones(args[1]);
var cuenta = Guid.Parse("11111111-1111-1111-1111-111111111111");

switch (orden)
{
    case "encolar":
    {
        var op = new OperacionPendiente(Guid.NewGuid(), cuenta, 100m, DateTime.UtcNow);
        await cola.EncolarAsync(op);
        Console.WriteLine($"encolada   {op.Llave}");
        Console.WriteLine($"archivo    {cola.Ruta}");
        break;
    }

    case "leer":
    {
        var pendientes = await cola.PendientesAsync();
        Console.WriteLine($"pendientes {pendientes.Count}");
        foreach (var o in pendientes)
            Console.WriteLine($"  {o.Llave}  {o.MontoUVB,8:N2} UVB  intentos={o.Intentos}  " +
                              $"creada={o.CreadaEn:HH:mm:ss}");
        break;
    }

    case "confirmar":
    {
        await cola.ConfirmarAsync(Guid.Parse(args[2]));
        Console.WriteLine($"confirmada {args[2]}");
        break;
    }

    case "intento":
    {
        await cola.RegistrarIntentoAsync(Guid.Parse(args[2]));
        Console.WriteLine($"intento registrado para {args[2]}");
        break;
    }

    // Escribe basura en el archivo para comprobar qué hace la cola con un
    // archivo corrupto: debe devolver cola vacia, no tumbar la app.
    case "corromper":
    {
        var ruta = cola.Ruta;
        await File.WriteAllTextAsync(ruta, "[{\"Llave\":\"esto no es un guid");
        Console.WriteLine($"corrompido {ruta}");
        break;
    }

    // Encola N operaciones en paralelo, para ver si el candado aguanta.
    case "concurrente":
    {
        var n = args.Length > 2 ? int.Parse(args[2]) : 20;
        var tareas = Enumerable.Range(0, n).Select(_ =>
            cola.EncolarAsync(new OperacionPendiente(Guid.NewGuid(), cuenta, 10m, DateTime.UtcNow)));
        await Task.WhenAll(tareas);
        Console.WriteLine($"encoladas {n} en paralelo");
        break;
    }

    // Clase 19 · ¿qué lanza la sesión cuando no hay token o ya venció?
    //
    // No es una pregunta de estilo. Si lanzara InvalidOperationException
    // caería en el catch general de TransferenciaViewModel, que DESCARTA la
    // operación — y entonces reabrir la app después de cerrarla borraría la
    // transferencia pendiente en el primer reintento, porque al reabrir
    // todavía no hay sesión. Tiene que ser un tipo propio, y eso se mide.
    case "sesion":
    {
        var fallos = 0;

        void Caso(string titulo, Action accion)
        {
            try
            {
                accion();
                Console.WriteLine($"  ✗ {titulo}: no lanzó nada");
                fallos++;
            }
            catch (SesionNoDisponibleException ex)
            {
                Console.WriteLine($"  ✓ {titulo}: SesionNoDisponibleException · \"{ex.Message}\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {titulo}: lanzó {ex.GetType().Name} — el catch general lo DESCARTARÍA");
                fallos++;
            }
        }

        var sesion = new SesionUsuario();

        Console.WriteLine("1 · recién creada, sin entrar");
        Console.WriteLine($"    EstaAutenticado = {sesion.EstaAutenticado}   (esperado False)");
        Caso("TokenVigente()", () => sesion.TokenVigente());

        Console.WriteLine("2 · con un token YA VENCIDO");
        sesion.Guardar("ana.operadora",
            new RespuestaToken("jwt-de-mentira", DateTimeOffset.UtcNow.AddMinutes(-1),
                               "Bearer", "operador", 15));
        Console.WriteLine($"    EstaAutenticado = {sesion.EstaAutenticado}   (esperado False)");
        Console.WriteLine($"    Expiro          = {sesion.Expiro}   (esperado True)");
        Caso("TokenVigente()", () => sesion.TokenVigente());

        Console.WriteLine("3 · con un token vigente");
        sesion.Guardar("ana.operadora",
            new RespuestaToken("jwt-de-mentira", DateTimeOffset.UtcNow.AddMinutes(15),
                               "Bearer", "operador", 15));
        Console.WriteLine($"    EstaAutenticado = {sesion.EstaAutenticado}   (esperado True)");
        Console.WriteLine($"    PuedeTransferir = {sesion.PuedeTransferir}   (esperado True)");
        if (!sesion.EstaAutenticado || !sesion.PuedeTransferir) fallos++;

        Console.WriteLine("4 · rol consulta: puede leer, no puede mover plata");
        sesion.Guardar("luis.consulta",
            new RespuestaToken("jwt-de-mentira", DateTimeOffset.UtcNow.AddMinutes(15),
                               "Bearer", "consulta", 15));
        Console.WriteLine($"    PuedeTransferir = {sesion.PuedeTransferir}   (esperado False)");
        if (sesion.PuedeTransferir) fallos++;

        Console.WriteLine("5 · cerrar sesión");
        sesion.CerrarSesion();
        Console.WriteLine($"    EstaAutenticado = {sesion.EstaAutenticado}   (esperado False)");
        Console.WriteLine($"    Expiro          = {sesion.Expiro}   (esperado False: no es que venció, es que no hay)");
        if (sesion.EstaAutenticado || sesion.Expiro) fallos++;

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "TODO BIEN" : $"{fallos} FALLO(S)");
        return fallos == 0 ? 0 : 1;
    }

    default:
        Console.WriteLine($"orden desconocida: {orden}");
        return 1;
}

return 0;
