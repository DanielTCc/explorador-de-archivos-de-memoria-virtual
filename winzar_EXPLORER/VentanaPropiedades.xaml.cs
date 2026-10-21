
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using winzar_EXPLORER.Models;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    // Metadatos de un archivo y edición de sus permisos.
    public partial class VentanaPropiedades : Window
    {
        public Permisos PermisosElegidos
        {
            get
            {
                var permisos = Permisos.Ninguno;

                if (ChkLectura.IsChecked == true)
                    permisos |= Permisos.Lectura;

                if (ChkEscritura.IsChecked == true)
                    permisos |= Permisos.Escritura;

                if (ChkEliminacion.IsChecked == true)
                    permisos |= Permisos.Eliminacion;

                return permisos;
            }
        }

        public VentanaPropiedades(SistemaArchivos sistema, ArchivoVirtual archivo)
        {
            InitializeComponent();

            Title = "Propiedades de " + archivo.Nombre;
            TxtTitulo.Text = archivo.Nombre;
            TxtRuta.Text = SistemaArchivos.UnirRuta(sistema.ObtenerRutaActual(), archivo.Nombre);

            TxtTamaño.Text = $"{archivo.Tamaño} bytes";
            TxtEspacio.Text =
                $"{archivo.Bloques.Count} × {GestorBloques.TamañoBloque} B = " +
                $"{archivo.Bloques.Count * GestorBloques.TamañoBloque} bytes";
            TxtCreado.Text = archivo.FechaCreacion.ToString("dd/MM/yyyy HH:mm:ss");
            TxtModificado.Text = archivo.FechaModificacion.ToString("dd/MM/yyyy HH:mm:ss");

            if (archivo.Bloques.Count == 0)
            {
                PanelBloques.Children.Add(new TextBlock
                {
                    Text = "Ninguno (archivo vacío)",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextoSecundario"),
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            foreach (int id in archivo.Bloques)
            {
                PanelBloques.Children.Add(new Border
                {
                    Style = (Style)FindResource("Chip"),
                    Child = new TextBlock
                    {
                        Text = id.ToString(),
                        FontSize = 12,
                        FontFamily = (FontFamily)FindResource("Monoespaciada"),
                        Foreground = (Brush)FindResource("Primario")
                    }
                });
            }

            ChkLectura.IsChecked = archivo.Permisos.HasFlag(Permisos.Lectura);
            ChkEscritura.IsChecked = archivo.Permisos.HasFlag(Permisos.Escritura);
            ChkEliminacion.IsChecked = archivo.Permisos.HasFlag(Permisos.Eliminacion);
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
