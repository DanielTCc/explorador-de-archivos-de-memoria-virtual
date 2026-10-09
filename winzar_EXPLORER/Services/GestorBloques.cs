
using System;
using System.Collections.Generic;
using System.Linq;
using winzar_EXPLORER.Models;

namespace winzar_EXPLORER.Services;

public class GestorBloques
{
    public const int TamañoBloque = 64;

    public List<Bloque> Bloques { get; private set; }

    public int TotalBloques => Bloques.Count;

    public int BloquesOcupados => Bloques.Count(b => b.Ocupado);

    public int BloquesLibres => TotalBloques - BloquesOcupados;

    public GestorBloques(int cantidadBloques = 100)
    {
        if (cantidadBloques <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(cantidadBloques),
                "El disco debe tener al menos un bloque.");

        Bloques = new List<Bloque>();

        for (int i = 0; i < cantidadBloques; i++)
        {
            Bloques.Add(new Bloque(i));
        }
    }

    public List<int> AsignarBloques(int cantidad)
    {
        if (cantidad < 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad));

        if (cantidad == 0)
            return new List<int>();

        var libres = Bloques
            .Where(b => !b.Ocupado)
            .Take(cantidad)
            .ToList();

        if (libres.Count < cantidad)
        {
            throw new InvalidOperationException(
                "No hay suficiente espacio en el disco virtual.");
        }

        foreach (var bloque in libres)
        {
            bloque.Ocupado = true;
        }

        return libres.Select(b => b.Id).ToList();
    }

    public void LiberarBloques(List<int> ids)
    {
        foreach (int id in ids)
        {
            if (id < 0 || id >= Bloques.Count)
                continue;

            Bloques[id].Ocupado = false;
        }
    }
}