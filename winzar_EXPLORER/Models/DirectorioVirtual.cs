using System;
using System.Collections.Generic;
using System.Text;


namespace winzar_EXPLORER.Models;

public class DirectorioVirtual
{
    public string Nombre { get; set; }

    public DirectorioVirtual? Padre { get; set; }

    public List<ArchivoVirtual> Archivos { get; set; } = new();

    public List<DirectorioVirtual> Directorios { get; set; } = new();

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DirectorioVirtual(string nombre, DirectorioVirtual? padre = null)
    {
        Nombre = nombre;
        Padre = padre;
    }
}