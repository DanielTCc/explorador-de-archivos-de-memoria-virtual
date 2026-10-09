using System;
using System.Collections.Generic;
using System.Text;


namespace winzar_EXPLORER.Models;

public class ArchivoVirtual
{
    public string Nombre { get; set; }

    public string Contenido { get; set; }

    public int Tamaño => System.Text.Encoding.UTF8.GetByteCount(Contenido);

    public List<int> Bloques { get; set; } = new();

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime FechaModificacion { get; set; } = DateTime.Now;

    public ArchivoVirtual(string nombre)
    {
        Nombre = nombre;
        Contenido = string.Empty;
    }
}