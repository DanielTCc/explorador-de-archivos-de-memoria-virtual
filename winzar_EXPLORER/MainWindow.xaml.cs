
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using winzar_EXPLORER.Models;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    public partial class MainWindow : Window
    {
        private readonly SistemaArchivos sistema = new SistemaArchivos(100);

        public MainWindow()
        {
            InitializeComponent();
            ActualizarVista();
        }

        // Actualizar la ruta, la lista de archivos y el almacenamiento.
        private void ActualizarVista()
        {
            if (FindName("TxtRuta") is TextBox txtRuta)
            {
                txtRuta.Text = sistema.ObtenerRutaActual();
            }

            if (FindName("ListaArchivos") is ListView lista)
            {
                lista.Items.Clear();

                foreach (var directorio in sistema.DirectorioActual.Directorios)
                {
                    lista.Items.Add("📁  " + directorio.Nombre);
                }

                foreach (var archivo in sistema.DirectorioActual.Archivos)
                {
                    lista.Items.Add("📄  " + archivo.Nombre);
                }
            }

            if (FindName("TxtEstado") is TextBlock estado)
            {
                estado.Text =
                    $"{sistema.ListarDirectorio().Count} elementos | " +
                    $"Bloques ocupados: {sistema.GestorBloques.BloquesOcupados} | " +
                    $"Bloques libres: {sistema.GestorBloques.BloquesLibres}/" +
                    $"{sistema.GestorBloques.TotalBloques}";
            }
        }

        // Crear una carpeta.
        private void CrearCarpeta_Click(object sender, RoutedEventArgs e)
        {
            string nombre = Interaction.InputBox(
                "Nombre de la nueva carpeta:",
                "Crear carpeta");

            if (string.IsNullOrWhiteSpace(nombre))
                return;

            try
            {
                sistema.CrearDirectorio(nombre.Trim());
                ActualizarVista();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo crear la carpeta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Crear un archivo con contenido inicial.
        private void CrearArchivo_Click(object sender, RoutedEventArgs e)
        {
            string nombre = Interaction.InputBox(
                "Nombre del archivo, por ejemplo notas.txt:",
                "Crear archivo");

            if (string.IsNullOrWhiteSpace(nombre))
                return;

            string contenido = Interaction.InputBox(
                "Escribe el contenido inicial del archivo:",
                "Contenido del archivo");

            try
            {
                sistema.CrearArchivo(nombre.Trim(), contenido);
                ActualizarVista();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo crear el archivo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Abrir carpetas o mostrar el contenido de un archivo.
        // Se ejecuta al hacer doble clic en un elemento.
        private void Abrir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ListView lista || lista.SelectedItem == null)
            {
                if (FindName("ListaArchivos") is not ListView listaPrincipal ||
                    listaPrincipal.SelectedItem == null)
                {
                    return;
                }

                lista = listaPrincipal;
            }

            string seleccionado = lista.SelectedItem?.ToString() ?? string.Empty;

            string nombre;

            if (seleccionado.StartsWith("📁"))
            {
                nombre = seleccionado.Substring(3).Trim();
            }
            else if (seleccionado.StartsWith("📄"))
            {
                nombre = seleccionado.Substring(3).Trim();
            }
            else
            {
                nombre = seleccionado.Trim();
            }

            var directorio = sistema.DirectorioActual.Directorios
                .FirstOrDefault(d =>
                    d.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));

            if (directorio != null)
            {
                try
                {
                    sistema.CambiarDirectorio(directorio.Nombre);
                    ActualizarVista();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error al abrir carpeta");
                }

                return;
            }

            var archivo = sistema.DirectorioActual.Archivos
                .FirstOrDefault(a =>
                    a.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));

            if (archivo != null)
            {
                MessageBox.Show(
                    sistema.LeerArchivo(archivo.Nombre),
                    archivo.Nombre,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Volver al directorio anterior.
        private void Subir_Click(object sender, RoutedEventArgs e)
        {
            sistema.CambiarDirectorio("..");
            ActualizarVista();
        }

        // Volver directamente a la raíz.
        private void Raiz_Click(object sender, RoutedEventArgs e)
        {
            sistema.CambiarDirectorio("/");
            ActualizarVista();
        }

        // Consultar el estado del disco virtual.
        private void Almacenamiento_Click(object sender, RoutedEventArgs e)
        {
            int total = sistema.GestorBloques.TotalBloques;
            int ocupados = sistema.GestorBloques.BloquesOcupados;
            int libres = sistema.GestorBloques.BloquesLibres;
            int capacidad = total * GestorBloques.TamañoBloque;

            MessageBox.Show(
                $"Bloques totales: {total}\n" +
                $"Bloques ocupados: {ocupados}\n" +
                $"Bloques libres: {libres}\n" +
                $"Tamaño por bloque: {GestorBloques.TamañoBloque} bytes\n" +
                $"Capacidad total: {capacidad} bytes\n" +
                $"Espacio utilizado: {ocupados * GestorBloques.TamañoBloque} bytes",
                "Almacenamiento virtual",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Mostrar información de la aplicación.
        private void Informacion_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Winzard Explorer\n\n" +
                "Sistema de archivos virtual desarrollado en C# y WPF.\n\n" +
                "Funciones disponibles:\n" +
                "• Crear archivos y carpetas.\n" +
                "• Abrir directorios.\n" +
                "• Leer contenido de archivos.\n" +
                "• Eliminar archivos y carpetas vacías.\n" +
                "• Administrar bloques del disco virtual.",
                "Información del sistema",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        //importa  archivos desde fuera de el entorno virtual
        private void ImportarArchivo_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Seleccionar archivo para importar",
                Filter =
                    "Archivos de texto y código|*.txt;*.json;*.csv;*.xml;*.cs;*.html;*.css;*.js;*.py;*.md;*.log|" +
                    "Todos los archivos|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            bool? resultado = dialogo.ShowDialog(this);

            if (resultado != true)
                return;

            try
            {
                string ruta = dialogo.FileName;
                string nombre = System.IO.Path.GetFileName(ruta);

                // Leer el contenido del archivo externo.
                string contenido = System.IO.File.ReadAllText(ruta);

                // Guardarlo en el sistema de archivos virtual.
                sistema.CrearArchivo(nombre, contenido);

                ActualizarVista();

                MessageBox.Show(
                    $"Archivo importado correctamente.\n\n" +
                    $"Nombre: {nombre}\n" +
                    $"Tamaño: {System.Text.Encoding.UTF8.GetByteCount(contenido)} bytes\n" +
                    $"Ruta original: {ruta}",
                    "Importación completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error al importar el archivo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Eliminar el archivo o la carpeta seleccionada.
        private void Eliminar_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("ListaArchivos") is not ListView lista ||
                lista.SelectedItem == null)
            {
                MessageBox.Show(
                    "Selecciona un archivo o una carpeta primero.",
                    "Ningún elemento seleccionado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string seleccionado = lista.SelectedItem.ToString() ?? string.Empty;
            string nombre;

            if (seleccionado.StartsWith("📁") ||
                seleccionado.StartsWith("📄"))
            {
                nombre = seleccionado.Substring(3).Trim();
            }
            else
            {
                nombre = seleccionado.Trim();
            }

            MessageBoxResult respuesta = MessageBox.Show(
                $"¿Seguro que deseas eliminar '{nombre}'?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes)
                return;

            try
            {
                var directorio = sistema.DirectorioActual.Directorios
                    .FirstOrDefault(d =>
                        d.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));

                if (directorio != null)
                {
                    sistema.EliminarDirectorio(directorio.Nombre);
                }
                else
                {
                    sistema.EliminarArchivo(nombre);
                }

                ActualizarVista();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo eliminar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}