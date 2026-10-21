
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    // Terminal de comandos (mkdir, cd, create, write, read, ls, delete, exit...).
    // Trabaja sobre el mismo sistema de archivos que la ventana principal.
    public partial class VentanaTerminal : Window
    {
        private readonly SistemaArchivos sistema;
        private readonly InterpreteComandos interprete;

        // Se llama después de cada comando para refrescar la ventana principal.
        private readonly Action alCambiar;

        private readonly List<string> historial = new();
        private int posicionHistorial;

        public VentanaTerminal(SistemaArchivos sistema, Action alCambiar)
        {
            InitializeComponent();

            this.sistema = sistema;
            interprete = new InterpreteComandos(sistema);
            this.alCambiar = alCambiar;

            TxtSalida.Text =
                "Winzard Explorer - terminal del sistema de archivos virtual\n" +
                "Escribe help para ver los comandos.\n\n";

            ActualizarPrompt();

            Loaded += (_, _) => TxtComando.Focus();
        }

        private void ActualizarPrompt()
        {
            TxtPrompt.Text = interprete.Prompt;
        }

        private void TxtComando_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    EjecutarComando();
                    break;

                case Key.Tab:
                    Autocompletar();
                    break;

                case Key.L when Keyboard.Modifiers == ModifierKeys.Control:
                    TxtSalida.Clear();
                    break;

                case Key.Escape:
                    TxtComando.Clear();
                    break;

                // Flechas arriba/abajo recorren los comandos anteriores.
                case Key.Up when historial.Count > 0:
                    posicionHistorial = Math.Max(0, posicionHistorial - 1);
                    MostrarHistorial();
                    break;

                case Key.Down when historial.Count > 0:
                    posicionHistorial = Math.Min(historial.Count, posicionHistorial + 1);
                    MostrarHistorial();
                    break;

                default:
                    return;
            }

            e.Handled = true;
        }

        private void MostrarHistorial()
        {
            TxtComando.Text = posicionHistorial < historial.Count
                ? historial[posicionHistorial]
                : string.Empty;

            TxtComando.CaretIndex = TxtComando.Text.Length;
        }

        // Completa el comando o el nombre de archivo/carpeta que se está escribiendo.
        private void Autocompletar()
        {
            string texto = TxtComando.Text;
            int inicio = texto.LastIndexOf(' ') + 1;
            string parcial = texto[inicio..];
            bool esComando = inicio == 0;

            IEnumerable<string> opciones;

            if (esComando)
            {
                opciones = InterpreteComandos.Comandos;
            }
            else
            {
                string comando = texto.Split(' ')[0].ToLowerInvariant();
                var directorio = sistema.DirectorioActual;
                var carpetas = directorio.Directorios.Select(d => d.Nombre);

                opciones = comando is "cd" or "rmdir"
                    ? carpetas
                    : carpetas.Concat(directorio.Archivos.Select(a => a.Nombre));
            }

            var coincidencias = opciones
                .Where(o => o.StartsWith(parcial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o)
                .ToList();

            if (coincidencias.Count == 0)
                return;

            string completado;

            if (coincidencias.Count == 1)
            {
                completado = coincidencias[0] + (esComando ? " " : "");
            }
            else
            {
                // Varias opciones: se completa el prefijo común y se listan.
                completado = coincidencias.Aggregate((a, b) =>
                    new string(a.TakeWhile((c, i) => i < b.Length &&
                        char.ToLowerInvariant(c) == char.ToLowerInvariant(b[i])).ToArray()));

                if (completado.Length <= parcial.Length)
                {
                    TxtSalida.AppendText(string.Join("    ", coincidencias) + "\n\n");
                    TxtSalida.ScrollToEnd();
                    completado = parcial;
                }
            }

            TxtComando.Text = texto[..inicio] + completado;
            TxtComando.CaretIndex = TxtComando.Text.Length;
        }

        private void EjecutarComando()
        {
            string linea = TxtComando.Text;
            TxtComando.Clear();

            if (!string.IsNullOrWhiteSpace(linea))
                historial.Add(linea);

            posicionHistorial = historial.Count;

            string prompt = interprete.Prompt;
            var resultado = interprete.Ejecutar(linea);

            if (resultado.Limpiar)
            {
                TxtSalida.Clear();
            }
            else
            {
                TxtSalida.AppendText($"{prompt} {linea}\n");

                if (resultado.Salida.Length > 0)
                    TxtSalida.AppendText(resultado.Salida + "\n");

                TxtSalida.AppendText("\n");
                TxtSalida.ScrollToEnd();
            }

            ActualizarPrompt();
            alCambiar();

            // Como en una consola, exit termina el programa.
            if (resultado.Salir)
                Application.Current.Shutdown();
        }
    }
}
