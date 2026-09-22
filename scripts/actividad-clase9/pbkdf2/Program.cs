using System.Diagnostics;
using System.Security.Cryptography;

// Costo de PBKDF2-SHA256 en .NET 8 (la misma llamada que DirectorioUsuarios),
// por numero de iteraciones. Mediana de 7 despues de un calentamiento.
var sal = RandomNumberGenerator.GetBytes(16);
Console.WriteLine($"Procesadores logicos: {Environment.ProcessorCount}");
foreach (var it in new[] { 1_000, 100_000, 600_000 })
{
    Rfc2898DeriveBytes.Pbkdf2("Operadora-2026", sal, it, HashAlgorithmName.SHA256, 32);
    var t = new List<double>();
    for (int i = 0; i < 7; i++)
    {
        var sw = Stopwatch.StartNew();
        Rfc2898DeriveBytes.Pbkdf2("Operadora-2026", sal, it, HashAlgorithmName.SHA256, 32);
        t.Add(sw.Elapsed.TotalMilliseconds);
    }
    t.Sort();
    var med = t[3];
    Console.WriteLine($"{it,8:N0} iteraciones: {med,8:F2} ms  ->  {1000 / med,8:F1} intentos/s por nucleo");
}
