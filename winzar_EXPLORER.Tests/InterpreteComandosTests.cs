using System.IO;
using winzar_EXPLORER.Models;
using winzar_EXPLORER.Services;
using Xunit;

namespace winzar_EXPLORER.Tests;

public class InterpreteComandosTests : IDisposable
{
    private readonly string carpeta;
    private readonly SistemaArchivos sistema;
    private readonly InterpreteComandos interprete;

    public InterpreteComandosTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "winzard-pruebas-" + Guid.NewGuid());
        Directory.CreateDirectory(carpeta);
        sistema = new SistemaArchivos(100, Path.Combine(carpeta, "fs.json"));
        interprete = new InterpreteComandos(sistema);
    }

    public void Dispose()
    {
        Directory.Delete(carpeta, true);
    }

    // Secuencia exacta de la demostración práctica de la rúbrica.
    [Fact]
    public void DemostracionDeLaRubrica()
    {
        Assert.False(interprete.Ejecutar("mkdir documentos").EsError);
        Assert.False(interprete.Ejecutar("cd documentos").EsError);
        Assert.Equal("/documentos >", interprete.Prompt);
        Assert.False(interprete.Ejecutar("create tarea.txt").EsError);

        var escribir = interprete.Ejecutar("write tarea.txt \"Sistema Operativo\"");
        Assert.False(escribir.EsError);
        Assert.Contains("Bloques asignados: [0]", escribir.Salida);

        Assert.Equal("Sistema Operativo", interprete.Ejecutar("read tarea.txt").Salida);
        Assert.Equal("tarea.txt", interprete.Ejecutar("ls").Salida);

        var eliminar = interprete.Ejecutar("delete tarea.txt");
        Assert.Contains("Bloques liberados: [0]", eliminar.Salida);

        Assert.Equal("(directorio vacío)", interprete.Ejecutar("ls").Salida);
        Assert.True(interprete.Ejecutar("exit").Salir);
        Assert.Equal(0, sistema.GestorBloques.BloquesOcupados);
    }

    [Fact]
    public void Dividir_RespetaComillas()
    {
        Assert.Equal(
            new[] { "write", "a.txt", "hola mundo" },
            InterpreteComandos.Dividir("write a.txt \"hola mundo\""));
    }

    [Fact]
    public void ComandoDesconocido_DevuelveError()
    {
        var resultado = interprete.Ejecutar("formatear");

        Assert.True(resultado.EsError);
        Assert.Contains("Comando desconocido", resultado.Salida);
    }

    [Fact]
    public void Errores_SeMuestranSinRomperLaTerminal()
    {
        Assert.True(interprete.Ejecutar("read noexiste.txt").EsError);
        Assert.True(interprete.Ejecutar("cd noexiste").EsError);
        Assert.True(interprete.Ejecutar("write").EsError);
        Assert.True(interprete.Ejecutar("write a.txt \"sin cerrar").EsError);
    }

    [Fact]
    public void Chmod_CambiaPermisos()
    {
        interprete.Ejecutar("create a.txt hola");

        interprete.Ejecutar("chmod -w a.txt");
        Assert.Equal(Permisos.Lectura | Permisos.Eliminacion, sistema.ObtenerArchivo("a.txt").Permisos);
        Assert.True(interprete.Ejecutar("write a.txt otro").EsError);

        interprete.Ejecutar("chmod r-- a.txt");
        Assert.Equal(Permisos.Lectura, sistema.ObtenerArchivo("a.txt").Permisos);
    }

    [Fact]
    public void LsDetallado_MuestraMetadatos()
    {
        interprete.Ejecutar("create a.txt \"Sistema Operativo\"");

        string salida = interprete.Ejecutar("ls -l").Salida;

        Assert.Contains("rwd", salida);
        Assert.Contains("17", salida);
        Assert.Contains("a.txt", salida);
    }

    [Fact]
    public void Blocks_MuestraElMapaDeBloques()
    {
        interprete.Ejecutar("create a.txt \"Sistema Operativo\"");

        string salida = interprete.Ejecutar("blocks").Salida;

        Assert.StartsWith("  0  # . .", salida);
        Assert.Contains("/a.txt: [0]", salida);
    }
}
