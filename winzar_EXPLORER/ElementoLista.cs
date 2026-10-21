using System.Windows;
using System.Windows.Media;
using winzar_EXPLORER.Models;

namespace winzar_EXPLORER
{
    // Fila de la lista principal: textos ya formateados más el objeto real.
    public class ElementoLista
    {
        public const string GlifoCarpeta = "";
        public const string GlifoArchivo = "";

        public string Icono { get; }
        public Brush ColorIcono { get; }
        public string Nombre { get; }
        public string Tamaño { get; }
        public string Bloques { get; }
        public string Permisos { get; }
        public string Fecha { get; }
        public object Objeto { get; }

        // Ruta completa; solo se muestra en los resultados de búsqueda.
        public string Ruta { get; }
        public Visibility VisibilidadRuta => Ruta.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        public ElementoLista(DirectorioVirtual directorio, string ruta = "")
        {
            Icono = GlifoCarpeta;
            ColorIcono = (Brush)Application.Current.FindResource("Carpeta");
            Nombre = directorio.Nombre;
            int elementos = directorio.Directorios.Count + directorio.Archivos.Count;
            Tamaño = elementos == 1 ? "1 elemento" : $"{elementos} elementos";
            Bloques = "—";
            Permisos = "";
            Fecha = directorio.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
            Objeto = directorio;
            Ruta = ruta;
        }

        public ElementoLista(ArchivoVirtual archivo, string ruta = "")
        {
            Icono = GlifoArchivo;
            ColorIcono = (Brush)Application.Current.FindResource("Primario");
            Nombre = archivo.Nombre;
            Tamaño = FormatoTamaño(archivo.Tamaño);
            Bloques = archivo.Bloques.Count == 0
                ? "0"
                : $"{archivo.Bloques.Count}  [{string.Join(", ", archivo.Bloques)}]";
            Permisos = archivo.Permisos.ComoTexto();
            Fecha = archivo.FechaModificacion.ToString("dd/MM/yyyy HH:mm");
            Objeto = archivo;
            Ruta = ruta;
        }

        public static string FormatoTamaño(int bytes)
        {
            return bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:0.#} KB";
        }
    }
}
