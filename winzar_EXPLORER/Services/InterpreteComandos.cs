using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using winzar_EXPLORER.Models;

namespace winzar_EXPLORER.Services;

public record ResultadoComando(
    string Salida,
    bool EsError = false,
    bool Salir = false,
    bool Limpiar = false);

// Traduce comandos de texto (estilo shell) a operaciones del sistema de archivos.
// No conoce la interfaz: solo recibe una línea y devuelve el texto a mostrar.
public class InterpreteComandos
{
    // Nombres de comando reconocidos (se usan para autocompletar).
    public static readonly string[] Comandos =
    {
        "append", "blocks", "cat", "cd", "chmod", "clear", "create", "delete", "df", "dir",
        "exit", "find", "help", "info", "ls", "mkdir", "pwd", "read", "rm", "rmdir",
        "stat", "touch", "write"
    };

    private readonly SistemaArchivos sistema;

    public InterpreteComandos(SistemaArchivos sistema)
    {
        this.sistema = sistema;
    }

    public string Prompt => sistema.ObtenerRutaActual() + " >";

    public ResultadoComando Ejecutar(string linea)
    {
        List<string> partes;

        try
        {
            partes = Dividir(linea);
        }
        catch (ArgumentException ex)
        {
            return Error("Error: " + ex.Message);
        }

        if (partes.Count == 0)
            return new ResultadoComando("");

        string comando = partes[0].ToLowerInvariant();
        var args = partes.Skip(1).ToList();

        try
        {
            return comando switch
            {
                "help" or "ayuda" => new ResultadoComando(Ayuda()),
                "pwd" => new ResultadoComando(sistema.ObtenerRutaActual()),
                "ls" or "dir" => Listar(args),
                "cd" => CambiarDirectorio(args),
                "mkdir" => CrearDirectorio(args),
                "rmdir" => EliminarDirectorio(args),
                "create" or "touch" => CrearArchivo(args),
                "write" => Escribir(args, agregar: false),
                "append" => Escribir(args, agregar: true),
                "read" or "cat" => Leer(args),
                "delete" or "rm" => EliminarArchivo(args),
                "info" or "stat" => Informacion(args),
                "chmod" => CambiarPermisos(args),
                "find" or "buscar" => Buscar(args),
                "df" => new ResultadoComando(EstadoAlmacenamiento()),
                "blocks" or "mapa" => new ResultadoComando(MapaBloques()),
                "clear" or "cls" => new ResultadoComando("", Limpiar: true),
                "exit" or "salir" => new ResultadoComando("Saliendo...", Salir: true),
                _ => Error($"Comando desconocido: \"{comando}\". Escribe help para ver la lista.")
            };
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                  ArgumentException or
                  UnauthorizedAccessException or
                  System.IO.IOException)
        {
            return Error("Error: " + ex.Message);
        }
    }

    private static ResultadoComando Error(string mensaje)
    {
        return new ResultadoComando(mensaje, EsError: true);
    }

    private static void ExigirArgumentos(List<string> args, int cantidad, string uso)
    {
        if (args.Count < cantidad)
            throw new ArgumentException("Uso: " + uso);
    }

    private ResultadoComando Listar(List<string> args)
    {
        bool detallado = args.Contains("-l");
        var directorio = sistema.DirectorioActual;

        if (directorio.Directorios.Count == 0 && directorio.Archivos.Count == 0)
            return new ResultadoComando("(directorio vacío)");

        var salida = new StringBuilder();

        if (!detallado)
        {
            foreach (var d in directorio.Directorios)
                salida.AppendLine(d.Nombre + "/");

            foreach (var a in directorio.Archivos)
                salida.AppendLine(a.Nombre);

            return new ResultadoComando(salida.ToString().TrimEnd());
        }

        salida.AppendLine("TIPO  PERM  TAMAÑO  BLOQUES  MODIFICADO        NOMBRE");

        foreach (var d in directorio.Directorios)
        {
            salida.AppendLine(
                $"dir   ---   {"-",6}  {"-",7}  {d.FechaCreacion:yyyy-MM-dd HH:mm}  {d.Nombre}/");
        }

        foreach (var a in directorio.Archivos)
        {
            salida.AppendLine(
                $"arch  {a.Permisos.ComoTexto()}   {a.Tamaño,6}  {a.Bloques.Count,7}  {a.FechaModificacion:yyyy-MM-dd HH:mm}  {a.Nombre}");
        }

        return new ResultadoComando(salida.ToString().TrimEnd());
    }

    private ResultadoComando CambiarDirectorio(List<string> args)
    {
        ExigirArgumentos(args, 1, "cd <directorio> | cd .. | cd /");

        sistema.CambiarDirectorio(args[0]);

        return new ResultadoComando("");
    }

    private ResultadoComando CrearDirectorio(List<string> args)
    {
        ExigirArgumentos(args, 1, "mkdir <nombre>");

        sistema.CrearDirectorio(args[0]);

        return new ResultadoComando($"Directorio \"{args[0]}\" creado.");
    }

    private ResultadoComando EliminarDirectorio(List<string> args)
    {
        ExigirArgumentos(args, 1, "rmdir <nombre>");

        sistema.EliminarDirectorio(args[0]);

        return new ResultadoComando($"Directorio \"{args[0]}\" eliminado.");
    }

    private ResultadoComando CrearArchivo(List<string> args)
    {
        ExigirArgumentos(args, 1, "create <nombre> [\"contenido\"]");

        string contenido = Texto(args.Skip(1));

        sistema.CrearArchivo(args[0], contenido);

        var archivo = sistema.ObtenerArchivo(args[0]);

        return new ResultadoComando(
            $"Archivo \"{archivo.Nombre}\" creado: {archivo.Tamaño} bytes, " +
            $"{DescribirBloques(archivo.Bloques)}.");
    }

    private ResultadoComando Escribir(List<string> args, bool agregar)
    {
        ExigirArgumentos(
            args, 2,
            agregar ? "append <nombre> \"texto\"" : "write <nombre> \"texto\"");

        string texto = Texto(args.Skip(1));

        var archivo = sistema.ObtenerArchivo(args[0]);
        var bloquesAntes = new List<int>(archivo.Bloques);

        if (agregar)
            sistema.AgregarAlArchivo(args[0], texto);
        else
            sistema.EscribirArchivo(args[0], texto);

        var asignados = archivo.Bloques.Except(bloquesAntes).ToList();
        var liberados = bloquesAntes.Except(archivo.Bloques).ToList();

        var salida = new StringBuilder(
            $"\"{archivo.Nombre}\": {archivo.Tamaño} bytes, " +
            $"{DescribirBloques(archivo.Bloques)}.");

        if (asignados.Count > 0)
            salida.Append($"\n  Bloques asignados: [{string.Join(", ", asignados)}]");

        if (liberados.Count > 0)
            salida.Append($"\n  Bloques liberados: [{string.Join(", ", liberados)}]");

        return new ResultadoComando(salida.ToString());
    }

    private ResultadoComando Leer(List<string> args)
    {
        ExigirArgumentos(args, 1, "read <nombre>");

        string contenido = sistema.LeerArchivo(args[0]);

        return new ResultadoComando(
            contenido.Length == 0 ? "(archivo vacío)" : contenido);
    }

    private ResultadoComando EliminarArchivo(List<string> args)
    {
        ExigirArgumentos(args, 1, "delete <nombre>");

        var bloques = new List<int>(sistema.ObtenerArchivo(args[0]).Bloques);

        sistema.EliminarArchivo(args[0]);

        return new ResultadoComando(
            $"Archivo \"{args[0]}\" eliminado. " +
            (bloques.Count > 0
                ? $"Bloques liberados: [{string.Join(", ", bloques)}]"
                : "No tenía bloques asignados."));
    }

    private ResultadoComando Informacion(List<string> args)
    {
        ExigirArgumentos(args, 1, "info <nombre>");

        var directorio = sistema.DirectorioActual.Directorios
            .FirstOrDefault(d => d.Nombre.Equals(
                args[0], StringComparison.OrdinalIgnoreCase));

        if (directorio != null)
        {
            return new ResultadoComando(
                $"Nombre:        {directorio.Nombre}\n" +
                $"Tipo:          Directorio\n" +
                $"Ruta:          {sistema.ObtenerRuta(directorio)}\n" +
                $"Contenido:     {directorio.Directorios.Count} carpeta(s), " +
                $"{directorio.Archivos.Count} archivo(s)\n" +
                $"Creado:        {directorio.FechaCreacion:yyyy-MM-dd HH:mm:ss}");
        }

        return new ResultadoComando(
            DescribirArchivo(sistema, sistema.ObtenerArchivo(args[0])));
    }

    // Texto con los metadatos de un archivo; lo usa también la interfaz gráfica.
    public static string DescribirArchivo(SistemaArchivos sistema, ArchivoVirtual archivo)
    {
        return
            $"Nombre:        {archivo.Nombre}\n" +
            $"Tipo:          Archivo\n" +
            $"Ruta:          {SistemaArchivos.UnirRuta(sistema.ObtenerRutaActual(), archivo.Nombre)}\n" +
            $"Tamaño:        {archivo.Tamaño} bytes\n" +
            $"Bloques:       {archivo.Bloques.Count} x {GestorBloques.TamañoBloque} B " +
            $"[{string.Join(", ", archivo.Bloques)}]\n" +
            $"Espacio usado: {archivo.Bloques.Count * GestorBloques.TamañoBloque} bytes\n" +
            $"Permisos:      {archivo.Permisos.ComoTexto()} (r = leer, w = escribir, d = eliminar)\n" +
            $"Creado:        {archivo.FechaCreacion:yyyy-MM-dd HH:mm:ss}\n" +
            $"Modificado:    {archivo.FechaModificacion:yyyy-MM-dd HH:mm:ss}";
    }

    private ResultadoComando CambiarPermisos(List<string> args)
    {
        ExigirArgumentos(args, 2, "chmod <rwd | +w | -w ...> <nombre>");

        var archivo = sistema.ObtenerArchivo(args[1]);

        Permisos nuevos = InterpretarPermisos(args[0], archivo.Permisos);

        sistema.CambiarPermisos(archivo.Nombre, nuevos);

        return new ResultadoComando(
            $"Permisos de \"{archivo.Nombre}\": {nuevos.ComoTexto()}");
    }

    // Acepta la forma completa ("rw-", "r--") o cambios ("+w", "-d", "+rw").
    public static Permisos InterpretarPermisos(string texto, Permisos actuales)
    {
        static Permisos Letra(char c) => c switch
        {
            'r' => Permisos.Lectura,
            'w' => Permisos.Escritura,
            'd' => Permisos.Eliminacion,
            _ => throw new ArgumentException(
                $"Permiso desconocido: '{c}'. Usa r, w o d.")
        };

        texto = texto.ToLowerInvariant();

        if (texto.Length > 1 && (texto[0] == '+' || texto[0] == '-'))
        {
            Permisos cambio = texto.Skip(1).Aggregate(
                Permisos.Ninguno, (p, c) => p | Letra(c));

            return texto[0] == '+' ? actuales | cambio : actuales & ~cambio;
        }

        if (texto.Length == 3)
        {
            var resultado = Permisos.Ninguno;
            string esperado = "rwd";

            for (int i = 0; i < 3; i++)
            {
                if (texto[i] == esperado[i])
                    resultado |= Letra(texto[i]);
                else if (texto[i] != '-')
                    throw new ArgumentException(
                        "Formato de permisos: rwd, r--, rw-, +w, -d...");
            }

            return resultado;
        }

        throw new ArgumentException(
            "Formato de permisos: rwd, r--, rw-, +w, -d...");
    }

    private ResultadoComando Buscar(List<string> args)
    {
        ExigirArgumentos(args, 1, "find <texto | *.txt>");

        var resultados = sistema.Buscar(args[0]);

        if (resultados.Count == 0)
            return new ResultadoComando("No se encontraron coincidencias.");

        return new ResultadoComando(string.Join("\n",
            resultados.Select(r =>
                r.Elemento is DirectorioVirtual ? r.Ruta + "/" : r.Ruta)));
    }

    private string EstadoAlmacenamiento()
    {
        var gestor = sistema.GestorBloques;

        return
            $"Bloques totales:  {gestor.TotalBloques} x {GestorBloques.TamañoBloque} B " +
            $"= {gestor.TotalBloques * GestorBloques.TamañoBloque} bytes\n" +
            $"Bloques ocupados: {gestor.BloquesOcupados}\n" +
            $"Bloques libres:   {gestor.BloquesLibres}\n" +
            $"Uso del disco:    {100.0 * gestor.BloquesOcupados / gestor.TotalBloques:0.#} %";
    }

    // Mapa de bits del disco: # ocupado, . libre (10 bloques por fila).
    private string MapaBloques()
    {
        var gestor = sistema.GestorBloques;
        var mapa = sistema.ObtenerMapaBloques();
        var salida = new StringBuilder();

        for (int i = 0; i < gestor.TotalBloques; i += 10)
        {
            salida.Append($"{i,3}  ");

            for (int j = i; j < Math.Min(i + 10, gestor.TotalBloques); j++)
                salida.Append(gestor.Bloques[j].Ocupado ? "# " : ". ");

            salida.AppendLine();
        }

        salida.AppendLine("# = ocupado   . = libre");

        foreach (var grupo in mapa.GroupBy(par => par.Value))
        {
            salida.AppendLine(
                $"{grupo.Key}: [{string.Join(", ", grupo.Select(par => par.Key).OrderBy(id => id))}]");
        }

        return salida.ToString().TrimEnd();
    }

    private static string DescribirBloques(List<int> bloques)
    {
        return bloques.Count == 0
            ? "sin bloques asignados"
            : $"{bloques.Count} bloque(s) [{string.Join(", ", bloques)}]";
    }

    // Une los argumentos de texto y convierte "\n" en salto de línea.
    private static string Texto(IEnumerable<string> partes)
    {
        return string.Join(" ", partes).Replace("\\n", "\n");
    }

    // Divide la línea por espacios respetando el texto entre comillas.
    public static List<string> Dividir(string linea)
    {
        var partes = new List<string>();
        var actual = new StringBuilder();
        bool enComillas = false;
        bool hayParte = false;

        foreach (char c in linea)
        {
            if (c == '"')
            {
                enComillas = !enComillas;
                hayParte = true;
            }
            else if (char.IsWhiteSpace(c) && !enComillas)
            {
                if (hayParte)
                {
                    partes.Add(actual.ToString());
                    actual.Clear();
                    hayParte = false;
                }
            }
            else
            {
                actual.Append(c);
                hayParte = true;
            }
        }

        if (enComillas)
            throw new ArgumentException("Faltan las comillas de cierre.");

        if (hayParte)
            partes.Add(actual.ToString());

        return partes;
    }

    private static string Ayuda()
    {
        return
            "Comandos disponibles:\n" +
            "  ls [-l]                 Listar el directorio (-l: detallado)\n" +
            "  cd <ruta>               Cambiar de directorio (.., /, a/b)\n" +
            "  pwd                     Mostrar el directorio actual\n" +
            "  mkdir <nombre>          Crear directorio\n" +
            "  rmdir <nombre>          Eliminar directorio vacío\n" +
            "  create <nombre> [txt]   Crear archivo (alias: touch)\n" +
            "  write <nombre> \"txt\"    Reemplazar el contenido\n" +
            "  append <nombre> \"txt\"   Agregar al final\n" +
            "  read <nombre>           Leer archivo (alias: cat)\n" +
            "  delete <nombre>         Eliminar archivo (alias: rm)\n" +
            "  info <nombre>           Metadatos del archivo o directorio\n" +
            "  chmod <rwd|+w|-w> <n>   Cambiar permisos\n" +
            "  find <texto|*.txt>      Buscar en todo el disco\n" +
            "  df                      Espacio del disco virtual\n" +
            "  blocks                  Mapa de bloques\n" +
            "  clear                   Limpiar la terminal\n" +
            "  exit                    Salir";
    }
}
