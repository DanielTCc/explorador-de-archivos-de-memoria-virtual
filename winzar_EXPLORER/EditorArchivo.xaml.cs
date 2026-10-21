
using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    public enum ModoEditor
    {
        Nuevo,
        Editar,
        SoloLectura
    }

    // Ventana para crear un archivo, escribir en él o ver su contenido.
    public partial class EditorArchivo : Window
    {
        private readonly ModoEditor modo;
        private readonly GestorBloques gestor;

        // Bloques que el archivo podría ocupar: los suyos más los libres.
        private readonly int bloquesDisponibles;

        // Guarda (nombre, contenido) y devuelve un mensaje de error, o null si todo salió bien.
        private readonly Func<string, string, string?>? guardar;

        private readonly string contenidoOriginal;
        private bool guardado;

        public EditorArchivo(
            ModoEditor modo,
            string nombre,
            string contenido,
            GestorBloques gestor,
            int bloquesDelArchivo,
            Func<string, string, string?>? guardar = null)
        {
            InitializeComponent();

            this.modo = modo;
            this.gestor = gestor;
            this.guardar = guardar;
            contenidoOriginal = contenido;
            bloquesDisponibles = gestor.BloquesLibres + bloquesDelArchivo;

            TxtContenido.Text = contenido;

            switch (modo)
            {
                case ModoEditor.Nuevo:
                    Title = "Nuevo archivo";
                    TxtTitulo.Text = "Nuevo archivo";
                    TxtSubtitulo.Text = "Escribe el nombre y, si quieres, un contenido inicial.";
                    PanelNombre.Visibility = Visibility.Visible;
                    TxtNombre.Text = nombre;
                    TxtBotonGuardar.Text = "Crear archivo";
                    break;

                case ModoEditor.Editar:
                    Title = "Editar " + nombre;
                    TxtTitulo.Text = nombre;
                    TxtSubtitulo.Text = "Ctrl+S para guardar · Esc para cerrar";
                    break;

                case ModoEditor.SoloLectura:
                    Title = nombre;
                    TxtTitulo.Text = nombre;
                    TxtSubtitulo.Text = "El archivo no tiene permiso de escritura.";
                    EtiquetaModo.Visibility = Visibility.Visible;
                    TxtContenido.IsReadOnly = true;
                    BtnGuardar.Visibility = Visibility.Collapsed;
                    BtnCancelar.Content = "Cerrar";
                    break;
            }

            ActualizarUso();

            Loaded += (_, _) =>
            {
                if (modo == ModoEditor.Nuevo)
                {
                    TxtNombre.Focus();
                    TxtNombre.SelectAll();
                }
                else
                {
                    TxtContenido.Focus();
                    TxtContenido.CaretIndex = TxtContenido.Text.Length;
                }
            };
        }

        public string Contenido => TxtContenido.Text;

        private bool HayCambios =>
            modo != ModoEditor.SoloLectura &&
            (TxtContenido.Text != contenidoOriginal ||
             (modo == ModoEditor.Nuevo && TxtNombre.Text.Length > 0));

        private void TxtContenido_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarUso();
            TxtError.Visibility = Visibility.Collapsed;

            if (modo == ModoEditor.Editar)
                Title = (HayCambios ? "• " : "") + TxtTitulo.Text;
        }

        private void TxtNombre_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
        }

        // Muestra cuánto espacio ocupará el contenido antes de guardarlo.
        private void ActualizarUso()
        {
            if (gestor == null)
                return;

            int bytes = Encoding.UTF8.GetByteCount(TxtContenido.Text);
            int bloques = gestor.CalcularBloquesNecesarios(bytes);
            bool cabe = bloques <= bloquesDisponibles;

            TxtUso.Text = modo == ModoEditor.SoloLectura
                ? $"{bytes} bytes · {bloques} bloque(s) de {GestorBloques.TamañoBloque} B"
                : $"{bytes} bytes · {bloques} de {bloquesDisponibles} bloques disponibles" +
                  (cabe ? "" : " · no hay espacio suficiente");

            TxtUso.Foreground = (Brush)FindResource(cabe ? "TextoSecundario" : "Peligro");
            BarraUso.Value = bloquesDisponibles == 0 ? 1 : Math.Min(1, bloques / (double)bloquesDisponibles);
            BarraUso.Foreground = (Brush)FindResource(cabe ? "Primario" : "Peligro");
        }

        private void Guardar()
        {
            if (modo == ModoEditor.SoloLectura)
                return;

            string nombre = modo == ModoEditor.Nuevo ? TxtNombre.Text.Trim() : TxtTitulo.Text;
            string? error = guardar?.Invoke(nombre, TxtContenido.Text);

            if (error != null)
            {
                TxtError.Text = error;
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            guardado = true;
            DialogResult = true;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e) => Guardar();

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Guardar();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && modo == ModoEditor.Nuevo && TxtNombre.IsKeyboardFocused)
            {
                TxtContenido.Focus();
                e.Handled = true;
            }
        }

        // Evita perder lo escrito por cerrar la ventana sin querer.
        private void Ventana_Closing(object? sender, CancelEventArgs e)
        {
            if (guardado || !HayCambios)
                return;

            if (!Dialogo.Confirmar(this, "¿Descartar los cambios?",
                    "Lo que escribiste no se ha guardado.", "Descartar", peligro: true))
            {
                e.Cancel = true;
            }
        }
    }
}
