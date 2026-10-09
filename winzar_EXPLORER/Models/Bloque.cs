using System;
using System.Collections.Generic;
using System.Text;


namespace winzar_EXPLORER.Models;

public class Bloque
{
    public int Id { get; set; }

    public bool Ocupado { get; set; }

    public Bloque(int id)
    {
        Id = id;
        Ocupado = false;
    }
}