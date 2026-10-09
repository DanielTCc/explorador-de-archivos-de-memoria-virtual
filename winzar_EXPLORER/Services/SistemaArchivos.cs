
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using winzar_EXPLORER.Models;

namespace winzar_EXPLORER.Services;

public class SistemaArchivos
{
    public DirectorioVirtual Raiz { get; }

    public DirectorioVirtual DirectorioActual { get; private set; }

    public GestorBloques GestorBloques { get; }

    public string RutaPersistencia { get; }

    private readonly JsonSerializerOptions opcionesJson = new()
    {
        WriteIndented = true
    };

    public SistemaArchivos(int cantidadBloques = 100)
    {
        GestorBloques = new GestorBloques(cantidadBloques);

        Raiz = new DirectorioVirtual("Raíz");
        DirectorioActual = Raiz;

        string carpetaDatos = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WinzardExplorer");

        Directory.CreateDirectory(carpetaDatos);

        RutaPersistencia = Path.Combine(
            carpetaDatos,
            "filesystem.json");

        if (File.Exists(RutaPersistencia))
        {
            CargarEstado();
        }
    }

    public string ObtenerRutaActual()
    {
        if (DirectorioActual == Raiz)
            return "/";

        var nombres = new List<string>();
        var directorio = DirectorioActual;

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

        var nuevo = new DirectorioVirtual(
            nombre,
            DirectorioActual);

        DirectorioActual.Directorios.Add(nuevo);

        GuardarEstado();
    }

    public void EliminarDirectorio(string nombre)
    {
        var directorio = DirectorioActual.Directorios
            .FirstOrDefault(d => d.Nombre == nombre);

        if (directorio == null)
            throw new InvalidOperationException(
                "El directorio no existe.");

        if (directorio.Archivos.Count > 0 ||
            directorio.Directorios.Count > 0)
        {
            throw new InvalidOperationException(
                "El directorio no está vacío.");
        }

        DirectorioActual.Directorios.Remove(directorio);

        GuardarEstado();
    }

    public void CambiarDirectorio(string nombre)
    {
        if (nombre == "..")
        {
            if (DirectorioActual.Padre != null)
                DirectorioActual = DirectorioActual.Padre;

            return;
        }

        if (nombre == "/" ||
            nombre.Equals("Raíz", StringComparison.OrdinalIgnoreCase))
        {
            DirectorioActual = Raiz;
            return;
        }

        var destino = DirectorioActual.Directorios
            .FirstOrDefault(d =>
                d.Nombre.Equals(
                    nombre,
                    StringComparison.OrdinalIgnoreCase));

        if (destino == null)
            throw new InvalidOperationException(
                "No existe ese directorio.");

        DirectorioActual = destino;
    }

    public void CrearArchivo(string nombre, string contenido = "")
    {
        ValidarNombre(nombre);

        if (ExisteNombre(nombre))
            throw new InvalidOperationException(
                "Ya existe un archivo o directorio con ese nombre.");

        int bytes = Encoding.UTF8.GetByteCount(contenido);

        int cantidad = (int)Math.Ceiling(
            bytes / (double)GestorBloques.TamañoBloque);

        var bloques = GestorBloques.AsignarBloques(cantidad);

        var archivo = new ArchivoVirtual(nombre)
        {
            Contenido = contenido,
            Bloques = bloques
        };

        DirectorioActual.Archivos.Add(archivo);

        GuardarEstado();
    }

    public string LeerArchivo(string nombre)
    {
        return BuscarArchivo(nombre).Contenido;
    }

    public void EscribirArchivo(string nombre, string contenido)
    {
        var archivo = BuscarArchivo(nombre);

        int bytesNuevos = Encoding.UTF8.GetByteCount(contenido);

        int bloquesNecesarios = (int)Math.Ceiling(
            bytesNuevos / (double)GestorBloques.TamañoBloque);

        int bloquesActuales = archivo.Bloques.Count;

        if (bloquesNecesarios > bloquesActuales)
        {
            int adicionales = bloquesNecesarios - bloquesActuales;

            var nuevos = GestorBloques.AsignarBloques(adicionales);

            archivo.Bloques.AddRange(nuevos);
        }
        else if (bloquesNecesarios < bloquesActuales)
        {
            var sobrantes = archivo.Bloques
                .Skip(bloquesNecesarios)
                .ToList();

            GestorBloques.LiberarBloques(sobrantes);

            archivo.Bloques = archivo.Bloques
                .Take(bloquesNecesarios)
                .ToList();
        }

        archivo.Contenido = contenido;
        archivo.FechaModificacion = DateTime.Now;

        GuardarEstado();
    }

    public void EliminarArchivo(string nombre)
    {
        var archivo = BuscarArchivo(nombre);

        GestorBloques.LiberarBloques(archivo.Bloques);

        DirectorioActual.Archivos.Remove(archivo);

        GuardarEstado();
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

        CargarContenidoDirectorio(estado, Raiz);

        DirectorioActual = Raiz;
    }

    private DirectorioGuardado CrearCopiaDirectorio(
        DirectorioVirtual directorio)
    {
        var copia = new DirectorioGuardado
        {
            Nombre = directorio.Nombre
        };

        foreach (var archivo in directorio.Archivos)
        {
            copia.Archivos.Add(new ArchivoGuardado
            {
                Nombre = archivo.Nombre,
                Contenido = archivo.Contenido,
                FechaCreacion = archivo.FechaCreacion,
                FechaModificacion = archivo.FechaModificacion
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
        DirectorioVirtual destino)
    {
        foreach (var archivoGuardado in guardado.Archivos)
        {
            string contenido = archivoGuardado.Contenido ?? "";

            int bytes = Encoding.UTF8.GetByteCount(contenido);

            int cantidad = (int)Math.Ceiling(
                bytes / (double)GestorBloques.TamañoBloque);

            var bloques = GestorBloques.AsignarBloques(cantidad);

            var archivo = new ArchivoVirtual(archivoGuardado.Nombre)
            {
                Contenido = contenido,
                Bloques = bloques,
                FechaCreacion = archivoGuardado.FechaCreacion,
                FechaModificacion = archivoGuardado.FechaModificacion
            };

            destino.Archivos.Add(archivo);
        }

        foreach (var directorioGuardado in guardado.Directorios)
        {
            var subdirectorio = new DirectorioVirtual(
                directorioGuardado.Nombre,
                destino);

            destino.Directorios.Add(subdirectorio);

            CargarContenidoDirectorio(
                directorioGuardado,
                subdirectorio);
        }
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

    private static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException(
                "El nombre no puede estar vacío.");

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
    }

    private class ArchivoGuardado
    {
        public string Nombre { get; set; } = "";

        public string Contenido { get; set; } = "";

        public DateTime FechaCreacion { get; set; }

        public DateTime FechaModificacion { get; set; }
    }
}