
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    // Muestra cada bloque del disco virtual y a qué archivo pertenece.
    public partial class VentanaAlmacenamiento : Window
    {
        // Un color por archivo para distinguir qué bloques usa cada uno.
        private static readonly Color[] Paleta =
        {
            Color.FromRgb(0x24, 0x57, 0xC5),
            Color.FromRgb(0x0E, 0x93, 0x84),
            Color.FromRgb(0xD9, 0x7A, 0x06),
            Color.FromRgb(0x9E, 0x3F, 0xC4),
            Color.FromRgb(0xC2, 0x3B, 0x5A),
            Color.FromRgb(0x4F, 0x7A, 0x28),
            Color.FromRgb(0x1F, 0x8F, 0xC7),
            Color.FromRgb(0x8A, 0x5A, 0x2B)
        };

        // Celda de cada bloque y archivo al que pertenece (null si está libre).
        private readonly List<(Border Celda, string? Ruta)> celdas = new();
        private readonly Dictionary<string, Border> filasLeyenda = new();

        // Archivo resaltado de forma fija (por clic o al abrir la ventana).
        private string? fijo;

        public VentanaAlmacenamiento(SistemaArchivos sistema, string? resaltar = null)
        {
            InitializeComponent();

            var gestor = sistema.GestorBloques;
            var mapa = sistema.ObtenerMapaBloques();

            var colores = mapa.Values
                .Distinct()
                .Select((ruta, i) => (ruta, color: Paleta[i % Paleta.Length]))
                .ToDictionary(x => x.ruta, x => x.color);

            double porcentaje = 100.0 * gestor.BloquesOcupados / gestor.TotalBloques;

            TxtResumen.Text =
                $"{gestor.TotalBloques} bloques de {GestorBloques.TamañoBloque} bytes = " +
                $"{gestor.TotalBloques * GestorBloques.TamañoBloque} bytes · asignación indexada (primer ajuste)";
            TxtTotal.Text = gestor.TotalBloques.ToString();
            TxtOcupados.Text = gestor.BloquesOcupados.ToString();
            TxtLibres.Text = gestor.BloquesLibres.ToString();
            TxtUso.Text = $"{porcentaje:0.#} %";
            BarraUso.Value = porcentaje;

            foreach (var bloque in gestor.Bloques)
            {
                mapa.TryGetValue(bloque.Id, out string? ruta);

                var celda = new Border
                {
                    Height = 34,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(ruta != null ? colores[ruta] : Color.FromRgb(0xEE, 0xF1, 0xF5)),
                    ToolTip = ruta != null
                        ? $"Bloque {bloque.Id} · {ruta}"
                        : $"Bloque {bloque.Id} · libre",
                    Child = new TextBlock
                    {
                        Text = bloque.Id.ToString(),
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = ruta != null ? Brushes.White : (Brush)FindResource("TextoTenue")
                    }
                };

                if (ruta != null)
                {
                    celda.Cursor = Cursors.Hand;
                    celda.MouseEnter += (_, _) => Resaltar(ruta);
                    celda.MouseLeave += (_, _) => Resaltar(fijo);
                    celda.MouseLeftButtonUp += (_, _) => Fijar(ruta);
                }

                celdas.Add((celda, ruta));
                GrillaBloques.Children.Add(celda);
            }

            if (colores.Count == 0)
            {
                PanelLeyenda.Children.Add(new TextBlock
                {
                    Text = "No hay bloques ocupados: el disco está vacío.",
                    Foreground = (Brush)FindResource("TextoSecundario"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 0, 0, 0)
                });
            }

            foreach (var (ruta, color) in colores)
            {
                var ids = mapa.Where(par => par.Value == ruta)
                    .Select(par => par.Key)
                    .OrderBy(id => id)
                    .ToList();

                var fila = new DockPanel();

                var muestra = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(0, 3, 10, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                DockPanel.SetDock(muestra, Dock.Left);
                fila.Children.Add(muestra);

                var texto = new StackPanel();
                texto.Children.Add(new TextBlock
                {
                    Text = ruta,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("Texto"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                texto.Children.Add(new TextBlock
                {
                    Text = $"{ids.Count} bloque(s): [{string.Join(", ", ids)}]",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextoSecundario"),
                    TextWrapping = TextWrapping.Wrap
                });
                fila.Children.Add(texto);

                var contenedor = new Border
                {
                    Child = fila,
                    Padding = new Thickness(8, 7, 8, 7),
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                contenedor.MouseEnter += (_, _) => Resaltar(ruta);
                contenedor.MouseLeave += (_, _) => Resaltar(fijo);
                contenedor.MouseLeftButtonUp += (_, _) => Fijar(ruta);

                filasLeyenda[ruta] = contenedor;
                PanelLeyenda.Children.Add(contenedor);
            }

            if (resaltar != null && colores.ContainsKey(resaltar))
                Fijar(resaltar);
        }

        private void Fijar(string ruta)
        {
            fijo = fijo == ruta ? null : ruta;
            Resaltar(fijo);
        }

        // Atenúa los bloques que no pertenecen al archivo indicado.
        private void Resaltar(string? ruta)
        {
            foreach (var (celda, dueño) in celdas)
            {
                celda.Opacity = ruta == null || dueño == ruta ? 1
                    : dueño == null ? 0.6
                    : 0.25;
            }

            foreach (var (clave, fila) in filasLeyenda)
            {
                fila.Background = clave == ruta
                    ? (Brush)FindResource("Hover")
                    : Brushes.Transparent;
            }
        }
    }
}
