using System;

namespace winzar_EXPLORER.Models;

// Permisos básicos de un archivo, inspirados en el esquema rwx de Unix.
// Se combinan como banderas: Lectura | Escritura = "rw-".
[Flags]
public enum Permisos
{
    Ninguno = 0,
    Lectura = 1,
    Escritura = 2,
    Eliminacion = 4,
    Todos = Lectura | Escritura | Eliminacion
}

public static class PermisosExtensiones
{
    // Representación corta: r = leer, w = escribir, d = eliminar.
    public static string ComoTexto(this Permisos permisos)
    {
        return
            (permisos.HasFlag(Permisos.Lectura) ? "r" : "-") +
            (permisos.HasFlag(Permisos.Escritura) ? "w" : "-") +
            (permisos.HasFlag(Permisos.Eliminacion) ? "d" : "-");
    }
}
