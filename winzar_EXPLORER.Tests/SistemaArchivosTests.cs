using System.IO;
using winzar_EXPLORER.Models;
using winzar_EXPLORER.Services;
using Xunit;

namespace winzar_EXPLORER.Tests;

// Casos de prueba TC-01 a TC-10 del documento técnico y casos adicionales.
// Cada prueba usa un disco pequeño y un JSON temporal propio.
public class SistemaArchivosTests : IDisposable
{
    private readonly string carpeta;
    private readonly string ruta;

    public SistemaArchivosTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "winzard-pruebas-" + Guid.NewGuid());
        Directory.CreateDirectory(carpeta);
        ruta = Path.Combine(carpeta, "fs.json");
    }

    public void Dispose()
    {
        Directory.Delete(carpeta, true);
    }

    // 5 bloques de 64 bytes = 320 bytes de disco.
    private SistemaArchivos NuevoSistema(int bloques = 5) => new(bloques, ruta);

    [Fact]
    public void TC01_CrearArchivo_ApareceEnElDirectorio()
    {
        var fs = NuevoSistema();

        fs.CrearArchivo("tarea.txt");

        Assert.Contains(fs.DirectorioActual.Archivos, a => a.Nombre == "tarea.txt");
        Assert.Equal(0, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void TC02_TC03_EscribirYLeer_ConservaElContenido()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("tarea.txt");

        fs.EscribirArchivo("tarea.txt", "Sistema Operativo");

        Assert.Equal("Sistema Operativo", fs.LeerArchivo("tarea.txt"));
        Assert.Equal(1, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void Escribir_AsignaYLiberaBloquesSegunElTamaño()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("a.txt");

        fs.EscribirArchivo("a.txt", new string('x', 130)); // ceil(130/64) = 3
        Assert.Equal(3, fs.ObtenerArchivo("a.txt").Bloques.Count);

        fs.EscribirArchivo("a.txt", "corto");
        Assert.Single(fs.ObtenerArchivo("a.txt").Bloques);
        Assert.Equal(1, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void Append_AgregaAlFinal()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("log.txt", "uno");

        fs.AgregarAlArchivo("log.txt", " dos");

        Assert.Equal("uno dos", fs.LeerArchivo("log.txt"));
    }

    [Fact]
    public void TC04_EliminarArchivo_LiberaSusBloques()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("a.txt", new string('x', 100));
        Assert.Equal(2, fs.GestorBloques.BloquesOcupados);

        fs.EliminarArchivo("a.txt");

        Assert.Empty(fs.DirectorioActual.Archivos);
        Assert.Equal(0, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void TC05_TC06_CrearDirectorioYNavegar()
    {
        var fs = NuevoSistema();

        fs.CrearDirectorio("documentos");
        fs.CambiarDirectorio("documentos");
        Assert.Equal("/documentos", fs.ObtenerRutaActual());

        fs.CambiarDirectorio("..");
        Assert.Equal("/", fs.ObtenerRutaActual());
    }

    [Fact]
    public void CambiarDirectorio_AceptaRutasDeVariosNiveles()
    {
        var fs = NuevoSistema();
        fs.CrearDirectorio("a");
        fs.CambiarDirectorio("a");
        fs.CrearDirectorio("b");
        fs.CambiarDirectorio("/");

        fs.CambiarDirectorio("a/b");
        Assert.Equal("/a/b", fs.ObtenerRutaActual());

        fs.CambiarDirectorio("../..");
        Assert.Equal("/", fs.ObtenerRutaActual());

        fs.CambiarDirectorio("/a/b");
        Assert.Equal("/a/b", fs.ObtenerRutaActual());
    }

    [Fact]
    public void TC07_EliminarDirectorioNoVacio_EsRechazado()
    {
        var fs = NuevoSistema();
        fs.CrearDirectorio("docs");
        fs.CambiarDirectorio("docs");
        fs.CrearArchivo("x.txt");
        fs.CambiarDirectorio("..");

        Assert.Throws<InvalidOperationException>(() => fs.EliminarDirectorio("docs"));
        Assert.Single(fs.DirectorioActual.Directorios);
    }

    [Fact]
    public void TC08_NombreDuplicado_EsRechazado()
    {
        var fs = NuevoSistema();
        fs.CrearDirectorio("documentos");

        Assert.Throws<InvalidOperationException>(() => fs.CrearArchivo("DOCUMENTOS"));
        Assert.Throws<InvalidOperationException>(() => fs.CrearDirectorio("documentos"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void NombresInvalidos_SonRechazados(string nombre)
    {
        var fs = NuevoSistema();

        Assert.Throws<ArgumentException>(() => fs.CrearDirectorio(nombre));
    }

    [Fact]
    public void TC09_RutaInexistente_NoCambiaElDirectorio()
    {
        var fs = NuevoSistema();
        fs.CrearDirectorio("a");

        Assert.Throws<InvalidOperationException>(() => fs.CambiarDirectorio("a/noexiste"));
        Assert.Equal("/", fs.ObtenerRutaActual());
    }

    [Fact]
    public void TC10_SinEspacio_SeRechazaSinModificarDatos()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("a.txt", "hola");

        Assert.Throws<InvalidOperationException>(
            () => fs.EscribirArchivo("a.txt", new string('x', 400)));
        Assert.Throws<InvalidOperationException>(
            () => fs.CrearArchivo("b.txt", new string('x', 400)));

        Assert.Equal("hola", fs.LeerArchivo("a.txt"));
        Assert.Single(fs.DirectorioActual.Archivos);
        Assert.Equal(1, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void Permisos_SeRespetanEnLecturaEscrituraYEliminacion()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("p.txt", "secreto");

        fs.CambiarPermisos("p.txt", Permisos.Ninguno);

        Assert.Throws<UnauthorizedAccessException>(() => fs.LeerArchivo("p.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => fs.EscribirArchivo("p.txt", "x"));
        Assert.Throws<UnauthorizedAccessException>(() => fs.EliminarArchivo("p.txt"));

        fs.CambiarPermisos("p.txt", Permisos.Lectura);
        Assert.Equal("secreto", fs.LeerArchivo("p.txt"));
    }

    [Fact]
    public void Buscar_EncuentraEnTodoElArbol()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("tarea.txt");
        fs.CrearDirectorio("docs");
        fs.CambiarDirectorio("docs");
        fs.CrearArchivo("tarea2.md");
        fs.CrearArchivo("notas.txt");

        var porTexto = fs.Buscar("tarea").Select(r => r.Ruta).ToList();
        var porPatron = fs.Buscar("*.txt").Select(r => r.Ruta).ToList();

        Assert.Equal(new[] { "/tarea.txt", "/docs/tarea2.md" }, porTexto);
        Assert.Equal(new[] { "/tarea.txt", "/docs/notas.txt" }, porPatron);
    }

    [Fact]
    public void Persistencia_ConservaArbolContenidoPermisosYBloques()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("hueco.txt", "x");          // ocupa el bloque 0
        fs.CrearDirectorio("docs");
        fs.CambiarDirectorio("docs");
        fs.CrearArchivo("b.txt", new string('b', 130)); // bloques 1, 2, 3
        fs.CambiarPermisos("b.txt", Permisos.Lectura);
        fs.CambiarDirectorio("/");
        fs.EliminarArchivo("hueco.txt");             // deja libre el bloque 0

        var recargado = NuevoSistema();

        var b = recargado.Raiz.Directorios.Single().Archivos.Single();
        Assert.Equal(new string('b', 130), b.Contenido);
        Assert.Equal(Permisos.Lectura, b.Permisos);
        Assert.Equal(new[] { 1, 2, 3 }, b.Bloques);
        Assert.Null(recargado.AdvertenciaCarga);
    }

    [Fact]
    public void Persistencia_JsonCorrupto_IniciaVacioYRespaldaElArchivo()
    {
        File.WriteAllText(ruta, "{ esto no es json");

        var fs = NuevoSistema();

        Assert.NotNull(fs.AdvertenciaCarga);
        Assert.Empty(fs.Raiz.Archivos);
        Assert.Single(Directory.GetFiles(carpeta, "fs.json.corrupto-*"));
    }

    [Fact]
    public void Persistencia_JsonDelFormatoAnterior_SeCarga()
    {
        File.WriteAllText(ruta,
            "{\"Nombre\":\"Raíz\",\"Archivos\":[{\"Nombre\":\"v.txt\",\"Contenido\":\"hola\"}],\"Directorios\":[]}");

        var fs = NuevoSistema();

        Assert.Null(fs.AdvertenciaCarga);
        Assert.Equal(Permisos.Todos, fs.Raiz.Archivos.Single().Permisos);
        Assert.Equal(1, fs.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void ErrorAlGuardar_RevierteElCambio()
    {
        var fs = NuevoSistema();
        fs.CrearArchivo("x.txt", "hola");

        // Una carpeta con el nombre del temporal provoca un error de E/S al guardar.
        Directory.CreateDirectory(fs.RutaPersistencia + ".tmp");

        Assert.Throws<IOException>(() => fs.CrearArchivo("y.txt", "abc"));
        Assert.Throws<IOException>(() => fs.EscribirArchivo("x.txt", new string('z', 200)));
        Assert.Throws<IOException>(() => fs.EliminarArchivo("x.txt"));

        Assert.Single(fs.Raiz.Archivos);
        Assert.Equal("hola", fs.LeerArchivo("x.txt"));
        Assert.Equal(1, fs.GestorBloques.BloquesOcupados);
    }
}
