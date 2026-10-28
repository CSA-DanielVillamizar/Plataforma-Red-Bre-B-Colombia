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

    default:
        Console.WriteLine($"orden desconocida: {orden}");
        return 1;
}

return 0;
