
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using winzar_EXPLORER.Models;

namespace winzar_EXPLORER.Services;

public class SistemaArchivos
{
    public DirectorioVirtual Raiz { get; }

    public DirectorioVirtual DirectorioActual { get; private set; }

    public GestorBloques GestorBloques { get; }

    public string RutaPersistencia { get; }

    // Mensaje para el usuario cuando el estado guardado no se pudo cargar.
    public string? AdvertenciaCarga { get; private set; }

    private readonly JsonSerializerOptions opcionesJson = new()
    {
        WriteIndented = true
    };

    public SistemaArchivos(
        int cantidadBloques = 100,
        string? rutaPersistencia = null)
    {
        GestorBloques = new GestorBloques(cantidadBloques);

        Raiz = new DirectorioVirtual("Raíz");
        DirectorioActual = Raiz;

        if (rutaPersistencia == null)
        {
            string carpetaDatos = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "WinzardExplorer");

            rutaPersistencia = Path.Combine(
                carpetaDatos,
                "filesystem.json");
        }

        RutaPersistencia = Path.GetFullPath(rutaPersistencia);

        Directory.CreateDirectory(
            Path.GetDirectoryName(RutaPersistencia)!);

        if (File.Exists(RutaPersistencia))
        {
            try
            {
                CargarEstado();
            }
            catch (Exception ex) when (
                ex is JsonException or
                      InvalidDataException or
                      InvalidOperationException or
                      ArgumentException or
                      IOException)
            {
                // Un JSON dañado no debe impedir que la aplicación inicie.
                ReiniciarEstado();
                AdvertenciaCarga = RespaldarArchivoDañado(ex);
            }
        }
    }

    public string ObtenerRutaActual()
    {
        return ObtenerRuta(DirectorioActual);
    }

    public string ObtenerRuta(DirectorioVirtual? directorio)
    {
        if (directorio == null || directorio == Raiz)
            return "/";

        var nombres = new List<string>();

        while (directorio != null && directorio != Raiz)
        {
            nombres.Add(directorio.Nombre);
            directorio = directorio.Padre;
        }

        nombres.Reverse();

        return "/" + string.Join("/", nombres);
    }

    public List<object> ListarDirectorio()
    {
        var elementos = new List<object>();

        elementos.AddRange(DirectorioActual.Directorios);
        elementos.AddRange(DirectorioActual.Archivos);

        return elementos;
    }

    public void CrearDirectorio(string nombre)
    {
        ValidarNombre(nombre);

        if (ExisteNombre(nombre))
            throw new InvalidOperationException(
                "Ya existe un archivo o directorio con ese nombre.");

        var actual = DirectorioActual;

        var nuevo = new DirectorioVirtual(
            nombre,
            actual);

        actual.Directorios.Add(nuevo);

        GuardarODeshacer(() => actual.Directorios.Remove(nuevo));
    }

    public void EliminarDirectorio(string nombre)
    {
        var actual = DirectorioActual;

        var directorio = actual.Directorios
            .FirstOrDefault(d =>
                d.Nombre.Equals(
                    nombre,
                    StringComparison.OrdinalIgnoreCase));

        if (directorio == null)
            throw new InvalidOperationException(
                "El directorio no existe.");

        if (directorio.Archivos.Count > 0 ||
            directorio.Directorios.Count > 0)
        {
            throw new InvalidOperationException(
                "El directorio no está vacío.");
        }

        int posicion = actual.Directorios.IndexOf(directorio);

        actual.Directorios.RemoveAt(posicion);

        GuardarODeshacer(() => actual.Directorios.Insert(posicion, directorio));
    }

    // Acepta un nombre, "..", "/" o rutas como "documentos/2026" o "/a/b".
    // Si algún tramo no existe, el directorio actual no cambia.
    public void CambiarDirectorio(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            throw new InvalidOperationException(
                "Indica el directorio de destino.");

        var destino = ruta.StartsWith('/') ? Raiz : DirectorioActual;

        foreach (string parte in ruta.Split(
                     '/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (parte == ".")
                continue;

            if (parte == "..")
            {
                destino = destino.Padre ?? destino;
                continue;
            }

            destino = destino.Directorios
                .FirstOrDefault(d =>
                    d.Nombre.Equals(
                        parte,
                        StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No existe el directorio \"{parte}\".");
        }

        DirectorioActual = destino;
    }

    public void CrearArchivo(string nombre, string contenido = "")
    {
        ValidarNombre(nombre);

        if (ExisteNombre(nombre))
            throw new InvalidOperationException(
                "Ya existe un archivo o directorio con ese nombre.");

        int cantidad = CalcularBloques(contenido);

        var bloques = GestorBloques.AsignarBloques(cantidad);

        var archivo = new ArchivoVirtual(nombre)
        {
            Contenido = contenido,
            Bloques = bloques
        };

        var actual = DirectorioActual;

        actual.Archivos.Add(archivo);

        GuardarODeshacer(() =>
        {
            actual.Archivos.Remove(archivo);
            GestorBloques.LiberarBloques(bloques);
        });
    }

    public string LeerArchivo(string nombre)
    {
        var archivo = BuscarArchivo(nombre);

        ComprobarPermiso(archivo, Permisos.Lectura, "lectura");

        return archivo.Contenido;
    }

    public ArchivoVirtual ObtenerArchivo(string nombre)
    {
        return BuscarArchivo(nombre);
    }

    public void EscribirArchivo(string nombre, string contenido)
    {
        var archivo = BuscarArchivo(nombre);

        ComprobarPermiso(archivo, Permisos.Escritura, "escritura");

        string contenidoAnterior = archivo.Contenido;
        var bloquesAnteriores = new List<int>(archivo.Bloques);
        DateTime fechaAnterior = archivo.FechaModificacion;

        int bloquesNecesarios = CalcularBloques(contenido);

        int bloquesActuales = archivo.Bloques.Count;

        var nuevos = new List<int>();
        var sobrantes = new List<int>();

        if (bloquesNecesarios > bloquesActuales)
        {
            int adicionales = bloquesNecesarios - bloquesActuales;

            // Si no hay espacio se lanza la excepción antes de modificar el archivo.
            nuevos = GestorBloques.AsignarBloques(adicionales);

            archivo.Bloques.AddRange(nuevos);
        }
        else if (bloquesNecesarios < bloquesActuales)
        {
            sobrantes = archivo.Bloques
                .Skip(bloquesNecesarios)
                .ToList();

            GestorBloques.LiberarBloques(sobrantes);

            archivo.Bloques = archivo.Bloques
                .Take(bloquesNecesarios)
                .ToList();
        }

        archivo.Contenido = contenido;
        archivo.FechaModificacion = DateTime.Now;

        GuardarODeshacer(() =>
        {
            GestorBloques.LiberarBloques(nuevos);
            GestorBloques.ReservarBloques(sobrantes);

            archivo.Bloques = bloquesAnteriores;
            archivo.Contenido = contenidoAnterior;
            archivo.FechaModificacion = fechaAnterior;
        });
    }

    // Agrega texto al final del archivo (equivalente a append).
    public void AgregarAlArchivo(string nombre, string texto)
    {
        var archivo = BuscarArchivo(nombre);

        EscribirArchivo(nombre, archivo.Contenido + texto);
    }

    public void CambiarPermisos(string nombre, Permisos permisos)
    {
        var archivo = BuscarArchivo(nombre);

        Permisos anteriores = archivo.Permisos;

        archivo.Permisos = permisos & Permisos.Todos;

        GuardarODeshacer(() => archivo.Permisos = anteriores);
    }

    // Busca en todo el árbol los elementos cuyo nombre contiene el texto.
    // Admite comodines: "*.txt", "tarea?".
    public List<ResultadoBusqueda> Buscar(string patron)
    {
        if (string.IsNullOrWhiteSpace(patron))
            throw new ArgumentException("Indica qué buscar.");

        Func<string, bool> coincide;

        if (patron.Contains('*') || patron.Contains('?'))
        {
            var expresion = new Regex(
                "^" + Regex.Escape(patron)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase);

            coincide = expresion.IsMatch;
        }
        else
        {
            coincide = n => n.Contains(
                patron,
                StringComparison.OrdinalIgnoreCase);
        }

        var resultados = new List<ResultadoBusqueda>();

        BuscarEn(Raiz, coincide, resultados);

        return resultados;
    }

    // Relaciona cada bloque ocupado con la ruta del archivo que lo usa.
    public Dictionary<int, string> ObtenerMapaBloques()
    {
        var mapa = new Dictionary<int, string>();

        void Recorrer(DirectorioVirtual directorio)
        {
            foreach (var archivo in directorio.Archivos)
            {
                string ruta = UnirRuta(ObtenerRuta(directorio), archivo.Nombre);

                foreach (int id in archivo.Bloques)
                    mapa[id] = ruta;
            }

            foreach (var subdirectorio in directorio.Directorios)
                Recorrer(subdirectorio);
        }

        Recorrer(Raiz);

        return mapa;
    }

    public void EliminarArchivo(string nombre)
    {
        var archivo = BuscarArchivo(nombre);

        ComprobarPermiso(archivo, Permisos.Eliminacion, "eliminación");

        var actual = DirectorioActual;

        int posicion = actual.Archivos.IndexOf(archivo);

        GestorBloques.LiberarBloques(archivo.Bloques);

        actual.Archivos.RemoveAt(posicion);

        GuardarODeshacer(() =>
        {
            GestorBloques.ReservarBloques(archivo.Bloques);
            actual.Archivos.Insert(posicion, archivo);
        });
    }

    // Guarda la estructura y los contenidos en un archivo JSON.
    public void GuardarEstado()
    {
        var estado = CrearCopiaDirectorio(Raiz);

        string json = JsonSerializer.Serialize(
            estado,
            opcionesJson);

        string temporal = RutaPersistencia + ".tmp";

        File.WriteAllText(temporal, json, Encoding.UTF8);

        // Sustituye el estado anterior después de escribir el nuevo.
        File.Move(temporal, RutaPersistencia, true);
    }

    // Si no se puede guardar, revierte el cambio en memoria para que
    // la simulación no quede distinta de lo que hay en disco.
    private void GuardarODeshacer(Action deshacer)
    {
        try
        {
            GuardarEstado();
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            deshacer();

            throw new IOException(
                "No se pudo guardar el estado en disco, " +
                "por lo que la operación se canceló.\n\n" + ex.Message,
                ex);
        }
    }

    // Recupera la estructura y reconstruye la asignación de bloques.
    private void CargarEstado()
    {
        string json = File.ReadAllText(RutaPersistencia, Encoding.UTF8);

        var estado = JsonSerializer.Deserialize<DirectorioGuardado>(
            json,
            opcionesJson);

        if (estado == null)
            throw new InvalidDataException(
                "El archivo de persistencia está vacío o no es válido.");

        var pendientes = new List<(ArchivoVirtual Archivo, List<int> Bloques)>();

        if (estado.FechaCreacion != default)
            Raiz.FechaCreacion = estado.FechaCreacion;

        CargarContenidoDirectorio(estado, Raiz, pendientes);

        // Primero se respetan los bloques guardados; así un archivo sin
        // bloques válidos no ocupa los que pertenecen a otro.
        var sinBloques = new List<ArchivoVirtual>();

        foreach (var (archivo, bloquesGuardados) in pendientes)
        {
            int necesarios = CalcularBloques(archivo.Contenido);

            if (bloquesGuardados.Count == necesarios &&
                GestorBloques.ReservarBloques(bloquesGuardados))
            {
                archivo.Bloques = new List<int>(bloquesGuardados);
            }
            else
            {
                sinBloques.Add(archivo);
            }
        }

        foreach (var archivo in sinBloques)
        {
            archivo.Bloques = GestorBloques.AsignarBloques(
                CalcularBloques(archivo.Contenido));
        }

        DirectorioActual = Raiz;
    }

    private void ReiniciarEstado()
    {
        Raiz.Archivos.Clear();
        Raiz.Directorios.Clear();
        GestorBloques.LiberarTodos();
        DirectorioActual = Raiz;
    }

    // Aparta el JSON dañado para no sobrescribirlo en el próximo guardado.
    private string RespaldarArchivoDañado(Exception error)
    {
        string mensaje =
            "No se pudo cargar el sistema de archivos guardado " +
            "y se inició uno vacío.\n\nMotivo: " + error.Message;

        try
        {
            string respaldo =
                RutaPersistencia + $".corrupto-{DateTime.Now:yyyyMMdd-HHmmss}";

            File.Move(RutaPersistencia, respaldo, true);

            return mensaje + "\n\nCopia del archivo original: " + respaldo;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return mensaje;
        }
    }

    private DirectorioGuardado CrearCopiaDirectorio(
        DirectorioVirtual directorio)
    {
        var copia = new DirectorioGuardado
        {
            Nombre = directorio.Nombre,
            FechaCreacion = directorio.FechaCreacion
        };

        foreach (var archivo in directorio.Archivos)
        {
            copia.Archivos.Add(new ArchivoGuardado
            {
                Nombre = archivo.Nombre,
                Contenido = archivo.Contenido,
                Bloques = new List<int>(archivo.Bloques),
                FechaCreacion = archivo.FechaCreacion,
                FechaModificacion = archivo.FechaModificacion,
                Permisos = archivo.Permisos
            });
        }

        foreach (var subdirectorio in directorio.Directorios)
        {
            copia.Directorios.Add(
                CrearCopiaDirectorio(subdirectorio));
        }

        return copia;
    }

    private void CargarContenidoDirectorio(
        DirectorioGuardado guardado,
        DirectorioVirtual destino,
        List<(ArchivoVirtual Archivo, List<int> Bloques)> pendientes)
    {
        foreach (var archivoGuardado in guardado.Archivos ?? new())
        {
            ValidarNombre(archivoGuardado.Nombre);

            var archivo = new ArchivoVirtual(archivoGuardado.Nombre)
            {
                Contenido = archivoGuardado.Contenido ?? "",
                FechaCreacion = archivoGuardado.FechaCreacion,
                FechaModificacion = archivoGuardado.FechaModificacion,
                Permisos = archivoGuardado.Permisos & Permisos.Todos
            };

            destino.Archivos.Add(archivo);

            pendientes.Add((archivo, archivoGuardado.Bloques ?? new()));
        }

        foreach (var directorioGuardado in guardado.Directorios ?? new())
        {
            ValidarNombre(directorioGuardado.Nombre);

            var subdirectorio = new DirectorioVirtual(
                directorioGuardado.Nombre,
                destino);

            if (directorioGuardado.FechaCreacion != default)
                subdirectorio.FechaCreacion = directorioGuardado.FechaCreacion;

            destino.Directorios.Add(subdirectorio);

            CargarContenidoDirectorio(
                directorioGuardado,
                subdirectorio,
                pendientes);
        }
    }

    private void BuscarEn(
        DirectorioVirtual directorio,
        Func<string, bool> coincide,
        List<ResultadoBusqueda> resultados)
    {
        string ruta = ObtenerRuta(directorio);

        foreach (var subdirectorio in directorio.Directorios)
        {
            if (coincide(subdirectorio.Nombre))
                resultados.Add(new ResultadoBusqueda(
                    UnirRuta(ruta, subdirectorio.Nombre), subdirectorio));
        }

        foreach (var archivo in directorio.Archivos)
        {
            if (coincide(archivo.Nombre))
                resultados.Add(new ResultadoBusqueda(
                    UnirRuta(ruta, archivo.Nombre), archivo));
        }

        foreach (var subdirectorio in directorio.Directorios)
            BuscarEn(subdirectorio, coincide, resultados);
    }

    public static string UnirRuta(string directorio, string nombre)
    {
        return directorio == "/" ? "/" + nombre : directorio + "/" + nombre;
    }

    private static void ComprobarPermiso(
        ArchivoVirtual archivo,
        Permisos necesario,
        string descripcion)
    {
        if (!archivo.Permisos.HasFlag(necesario))
            throw new UnauthorizedAccessException(
                $"Permiso denegado: \"{archivo.Nombre}\" no tiene permiso de {descripcion}.");
    }

    private int CalcularBloques(string contenido)
    {
        return GestorBloques.CalcularBloquesNecesarios(
            Encoding.UTF8.GetByteCount(contenido));
    }

    private ArchivoVirtual BuscarArchivo(string nombre)
    {
        var archivo = DirectorioActual.Archivos
            .FirstOrDefault(a =>
                a.Nombre.Equals(
                    nombre,
                    StringComparison.OrdinalIgnoreCase));

        if (archivo == null)
            throw new InvalidOperationException(
                "No existe ese archivo.");

        return archivo;
    }

    private bool ExisteNombre(string nombre)
    {
        return DirectorioActual.Archivos.Any(
                   a => a.Nombre.Equals(
                       nombre,
                       StringComparison.OrdinalIgnoreCase))
               ||
               DirectorioActual.Directorios.Any(
                   d => d.Nombre.Equals(
                       nombre,
                       StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException(
                "El nombre no puede estar vacío.");

        // "." y ".." están reservados para la navegación.
        if (nombre == "." || nombre == "..")
            throw new ArgumentException(
                "Los nombres \".\" y \"..\" están reservados.");

        if (nombre.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0 ||
            nombre.Contains('/') ||
            nombre.Contains('\\'))
        {
            throw new ArgumentException(
                "El nombre contiene caracteres no válidos.");
        }
    }

    // Modelos exclusivos para serializar el estado.
    // Evitan ciclos de referencias entre directorios y sus padres.
    private class DirectorioGuardado
    {
        public string Nombre { get; set; } = "Raíz";

        public List<ArchivoGuardado> Archivos { get; set; } = new();

        public List<DirectorioGuardado> Directorios { get; set; } = new();

        public DateTime FechaCreacion { get; set; }
    }

    private class ArchivoGuardado
    {
        public string Nombre { get; set; } = "";

        public string Contenido { get; set; } = "";

        // Bloques asignados; se conservan para que el mapa de bloques
        // sea el mismo al reiniciar la aplicación.
        public List<int> Bloques { get; set; } = new();

        public DateTime FechaCreacion { get; set; }

        public DateTime FechaModificacion { get; set; }

        // Si falta en un JSON antiguo, el archivo conserva todos los permisos.
        public Permisos Permisos { get; set; } = Permisos.Todos;
    }
}

public record ResultadoBusqueda(string Ruta, object Elemento);
