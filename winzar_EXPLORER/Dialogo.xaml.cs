
using System;
using System.Windows;
using System.Windows.Media;

namespace winzar_EXPLORER
{
    // Diálogo con el estilo de la aplicación: pide un texto, confirma o avisa.
    // Reemplaza a InputBox y MessageBox, y muestra los errores dentro del propio diálogo.
    public partial class Dialogo : Window
    {
        public const string IconoInfo = "";
        public const string IconoPregunta = "";
        public const string IconoAdvertencia = "";
        public const string IconoCarpeta = "";

        // Recibe el texto escrito y devuelve un mensaje de error, o null si es válido.
        private Func<string, string?>? validar;

        private Dialogo(Window? dueño, string titulo, string mensaje, string textoBoton, string icono)
        {
            InitializeComponent();

            if (dueño != null && dueño.IsLoaded)
                Owner = dueño;
            else
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Title = titulo;
            TxtTitulo.Text = titulo;
            TxtMensaje.Text = mensaje;
            TxtMensaje.Visibility = string.IsNullOrEmpty(mensaje) ? Visibility.Collapsed : Visibility.Visible;
            BtnAceptar.Content = textoBoton;
            TxtIcono.Text = icono;
        }

        private void UsarColoresDePeligro()
        {
            FondoIcono.Background = (Brush)FindResource("PeligroSuave");
            TxtIcono.Foreground = (Brush)FindResource("Peligro");
            BtnAceptar.Style = (Style)FindResource("BotonPeligroLleno");
        }

        public static string? PedirTexto(
            Window? dueño,
            string titulo,
            string mensaje,
            string textoBoton,
            Func<string, string?> validar,
            string valorInicial = "",
            string icono = IconoCarpeta)
        {
            var dialogo = new Dialogo(dueño, titulo, mensaje, textoBoton, icono)
            {
                validar = validar
            };

            dialogo.TxtEntrada.Visibility = Visibility.Visible;
            dialogo.TxtEntrada.Text = valorInicial;
            dialogo.Loaded += (_, _) =>
            {
                dialogo.TxtEntrada.Focus();
                dialogo.TxtEntrada.SelectAll();
            };

            return dialogo.ShowDialog() == true ? dialogo.TxtEntrada.Text.Trim() : null;
        }

        public static bool Confirmar(
            Window? dueño,
            string titulo,
            string mensaje,
            string textoBoton,
            bool peligro = false)
        {
            var dialogo = new Dialogo(dueño, titulo, mensaje, textoBoton,
                peligro ? IconoAdvertencia : IconoPregunta);

            if (peligro)
                dialogo.UsarColoresDePeligro();

            return dialogo.ShowDialog() == true;
        }

        public static void Avisar(
            Window? dueño,
            string titulo,
            string mensaje,
            bool advertencia = false)
        {
            var dialogo = new Dialogo(dueño, titulo, mensaje, "Entendido",
                advertencia ? IconoAdvertencia : IconoInfo);

            dialogo.BtnCancelar.Visibility = Visibility.Collapsed;

            if (advertencia)
            {
                dialogo.FondoIcono.Background = (Brush)dialogo.FindResource("PeligroSuave");
                dialogo.TxtIcono.Foreground = (Brush)dialogo.FindResource("Peligro");
            }

            dialogo.ShowDialog();
        }

        private void TxtEntrada_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
        }

        private void Aceptar_Click(object sender, RoutedEventArgs e)
        {
            if (validar != null)
            {
                string? error = validar(TxtEntrada.Text.Trim());

                if (error != null)
                {
                    TxtError.Text = error;
                    TxtError.Visibility = Visibility.Visible;
                    TxtEntrada.Focus();
                    TxtEntrada.SelectAll();
                    return;
                }
            }

            DialogResult = true;
        }
    }
}
