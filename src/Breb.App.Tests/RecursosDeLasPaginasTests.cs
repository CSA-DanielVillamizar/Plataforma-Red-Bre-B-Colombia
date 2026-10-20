using System.Text.RegularExpressions;

namespace Breb.App.Tests;

/// <summary>
/// El emulador revienta en InitializeComponent si una página usa un
/// StaticResource que no está cargado. Estas dos pruebas no necesitan
/// Android: una mira que la clave exista, la otra que las páginas no se
/// construyan antes de que App cargue App.xaml.
/// </summary>
public class RecursosDeLasPaginasTests
{
    private static readonly Regex RecursoUsado = new(
        @"\{StaticResource\s+(?<clave>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    private static readonly Regex ClaveDefinida = new(
        @"x:Key=""(?<clave>[^""]+)""",
        RegexOptions.Compiled);

    [Fact]
    public void Cada_StaticResource_usado_existe_en_los_diccionarios()
    {
        var app = DirectorioApp();
        var definidas = ClavesDefinidas(app);
        var faltan = new List<string>();

        foreach (var archivo in XamlDeLaApp(app))
        {
            var texto = File.ReadAllText(archivo);
            foreach (Match uso in RecursoUsado.Matches(texto))
            {
                var clave = uso.Groups["clave"].Value;
                if (!definidas.Contains(clave))
                    faltan.Add($"{Path.GetRelativePath(app, archivo)} → {clave}");
            }
        }

        Assert.True(faltan.Count == 0, "StaticResource sin clave: " + string.Join("; ", faltan));
        Assert.Contains("Headline", definidas);
    }

    [Fact]
    public void Las_paginas_no_se_crean_antes_de_cargar_los_recursos()
    {
        var codigo = File.ReadAllText(Path.Combine(DirectorioApp(), "App.xaml.cs"));
        var constructor = CuerpoDelConstructor(codigo);

        Assert.Contains("InitializeComponent()", constructor);
        Assert.DoesNotContain("AppShell", constructor);
        Assert.DoesNotContain("LoginPage", constructor);
        Assert.DoesNotContain("SaldoPage", constructor);
        Assert.DoesNotContain("TransferenciaPage", constructor);
        Assert.DoesNotContain("GetRequiredService", constructor);
        Assert.Contains("GetRequiredService<AppShell>", codigo);
    }

    [Fact]
    public void El_shell_no_ofrece_pestañas()
    {
        var xaml = File.ReadAllText(Path.Combine(DirectorioApp(), "AppShell.xaml"));
        Assert.DoesNotContain("TabBar", xaml);
        Assert.DoesNotContain("<Tab ", xaml);
        Assert.Contains("Route=\"login\"", xaml);
        Assert.DoesNotContain("Route=\"saldo\"", xaml);
        Assert.DoesNotContain("Route=\"transferencia\"", xaml);
    }

    private static string CuerpoDelConstructor(string codigo)
    {
        var inicio = codigo.IndexOf("public App(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "No aparece el constructor de App.");

        var abre = codigo.IndexOf('{', inicio);
        var profundidad = 0;
        for (var i = abre; i < codigo.Length; i++)
        {
            if (codigo[i] == '{') profundidad++;
            else if (codigo[i] == '}')
            {
                profundidad--;
                if (profundidad == 0)
                    return codigo[inicio..(i + 1)];
            }
        }

        throw new InvalidOperationException("El constructor de App no cierra.");
    }

    private static HashSet<string> ClavesDefinidas(string app)
    {
        var claves = new HashSet<string>(StringComparer.Ordinal);
        foreach (var archivo in XamlDeLaApp(app))
        {
            foreach (Match definida in ClaveDefinida.Matches(File.ReadAllText(archivo)))
                claves.Add(definida.Groups["clave"].Value);
        }

        return claves;
    }

    private static IEnumerable<string> XamlDeLaApp(string app) =>
        Directory.GetFiles(app, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string DirectorioApp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "src", "Breb.App", "App.xaml.cs");
            if (File.Exists(candidato))
                return Path.GetDirectoryName(candidato)!;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró src/Breb.App desde la carpeta de la prueba.");
    }
}
