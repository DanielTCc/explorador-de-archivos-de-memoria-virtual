
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using winzar_EXPLORER.Models;
using winzar_EXPLORER.Services;

namespace winzar_EXPLORER
{
    public partial class MainWindow : Window
    {
        private enum TipoAviso { Info, Exito, Error }

        private readonly SistemaArchivos sistema;

        private VentanaTerminal? terminal;

        // Texto buscado; vacío cuando se muestra el directorio actual.
        private string busqueda = "";

        // Evita que reconstruir el árbol dispare una navegación.
        private bool actualizandoArbol;

        // Carpetas que el usuario contrajo en el árbol.
        private readonly HashSet<DirectorioVirtual> contraidas = new();

        public MainWindow() : this(new SistemaArchivos(100))
        {
        }

        // Permite abrir la ventana sobre otro disco virtual (por ejemplo, en pruebas).
        public MainWindow(SistemaArchivos sistema)
        {
            this.sistema = sistema;

            InitializeComponent();
            ActualizarVista();
            Avisar("Listo. También puedes arrastrar archivos de texto a la ventana para importarlos.");

            // Avisar si el estado guardado estaba dañado y se inició vacío.
            if (sistema.AdvertenciaCarga != null)
            {
                Loaded += (_, _) => Dialogo.Avisar(
                    this, "Estado guardado no válido", sistema.AdvertenciaCarga, advertencia: true);
            }
        }

        // ------------------------------------------------------------ vista

        // Refresca migas, árbol, lista, panel de detalles y barra de estado.
        private void ActualizarVista(object? seleccionar = null)
        {
            seleccionar ??= ElementoSeleccionado();

            ConstruirMigas();
            ConstruirArbol();

            ListaArchivos.Items.Clear();

            if (busqueda.Length > 0)
            {
                foreach (var r in sistema.Buscar(busqueda))
                {
                    ListaArchivos.Items.Add(r.Elemento is DirectorioVirtual d
                        ? new ElementoLista(d, r.Ruta)
                        : new ElementoLista((ArchivoVirtual)r.Elemento, r.Ruta));
                }
            }
            else
            {
                foreach (var directorio in sistema.DirectorioActual.Directorios)
                    ListaArchivos.Items.Add(new ElementoLista(directorio));

                foreach (var archivo in sistema.DirectorioActual.Archivos)
                    ListaArchivos.Items.Add(new ElementoLista(archivo));
            }

            var fila = ListaArchivos.Items.Cast<ElementoLista>()
                .FirstOrDefault(e => ReferenceEquals(e.Objeto, seleccionar));

            if (fila != null)
            {
                ListaArchivos.SelectedItem = fila;
                ListaArchivos.ScrollIntoView(fila);
            }

            ActualizarEncabezado();
            ActualizarUsoDisco();
            ActualizarAcciones();
            ActualizarDetalles();
        }

        private void ActualizarEncabezado()
        {
            var actual = sistema.DirectorioActual;
            int carpetas = actual.Directorios.Count, archivos = actual.Archivos.Count;

            bool buscando = busqueda.Length > 0;
            PanelBusqueda.Visibility = buscando ? Visibility.Visible : Visibility.Collapsed;

            if (buscando)
            {
                TxtTituloCarpeta.Text = "Resultados de la búsqueda";
                TxtResumenCarpeta.Text = $"Buscando en todo el disco virtual";
                TxtBusqueda.Text = $"{ListaArchivos.Items.Count} resultado(s) para «{busqueda}»";
            }
            else
            {
                TxtTituloCarpeta.Text = actual == sistema.Raiz ? "Raíz" : actual.Nombre;
                TxtResumenCarpeta.Text = $"{Plural(carpetas, "carpeta", "carpetas")} · {Plural(archivos, "archivo", "archivos")}";
            }

            bool vacio = ListaArchivos.Items.Count == 0;
            PanelVacio.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;

            if (buscando)
            {
                TxtVacioIcono.Text = "";
                TxtVacioTitulo.Text = "Sin resultados";
                TxtVacioDetalle.Text = "Prueba con otra parte del nombre o con un patrón como *.txt";
            }
            else
            {
                TxtVacioIcono.Text = ElementoLista.GlifoCarpeta;
                TxtVacioTitulo.Text = "Esta carpeta está vacía";
                TxtVacioDetalle.Text = "Crea un archivo con Ctrl+N o arrastra aquí un archivo de texto.";
            }

            TxtEstado.Text =
                $"{Plural(ListaArchivos.Items.Count, "elemento", "elementos")} · " +
                $"{sistema.GestorBloques.BloquesLibres} bloques libres de {sistema.GestorBloques.TotalBloques}";
        }

        private void ActualizarUsoDisco()
        {
            var gestor = sistema.GestorBloques;
            double porcentaje = 100.0 * gestor.BloquesOcupados / gestor.TotalBloques;

            TxtUsoDisco.Text = $"{gestor.BloquesOcupados} de {gestor.TotalBloques} bloques en uso";
            BarraDisco.Value = porcentaje;
            BarraDisco.Foreground = (Brush)FindResource(porcentaje >= 85 ? "Peligro" : "Primario");
            TxtUsoBytes.Text =
                $"{ElementoLista.FormatoTamaño(gestor.BloquesOcupados * GestorBloques.TamañoBloque)} de " +
                $"{ElementoLista.FormatoTamaño(gestor.TotalBloques * GestorBloques.TamañoBloque)} ({porcentaje:0} %)";
        }

        private void ActualizarAcciones()
        {
            object? elemento = ElementoSeleccionado();

            BtnEditar.IsEnabled = elemento is ArchivoVirtual;
            BtnPropiedades.IsEnabled = elemento != null;
            BtnEliminar.IsEnabled = elemento != null;
            BtnSubir.IsEnabled = sistema.DirectorioActual.Padre != null || busqueda.Length > 0;
        }

        // Migas de pan: cada tramo de la ruta es un botón para volver a esa carpeta.
        private void ConstruirMigas()
        {
            PanelMigas.Children.Clear();

            var cadena = new List<DirectorioVirtual>();
            for (var d = sistema.DirectorioActual; d != null; d = d.Padre)
                cadena.Insert(0, d);

            for (int i = 0; i < cadena.Count; i++)
            {
                var directorio = cadena[i];
                bool ultimo = i == cadena.Count - 1;

                if (i > 0)
                {
                    PanelMigas.Children.Add(new TextBlock
                    {
                        Style = (Style)FindResource("Icono"),
                        Text = "",
                        FontSize = 9,
                        Margin = new Thickness(2, 0, 2, 0),
                        Foreground = (Brush)FindResource("TextoTenue")
                    });
                }

                var contenido = new StackPanel { Orientation = Orientation.Horizontal };

                if (i == 0)
                {
                    contenido.Children.Add(new TextBlock
                    {
                        Style = (Style)FindResource("Icono"),
                        Text = "",
                        FontSize = 13,
                        Margin = new Thickness(0, 0, 7, 0)
                    });
                }

                contenido.Children.Add(new TextBlock
                {
                    Text = i == 0 ? "Raíz" : directorio.Nombre,
                    FontWeight = ultimo ? FontWeights.SemiBold : FontWeights.Normal
                });

                var boton = new Button
                {
                    Style = (Style)FindResource("BotonPlano"),
                    Padding = new Thickness(8, 5, 8, 5),
                    Content = contenido,
                    Foreground = (Brush)FindResource(ultimo ? "Texto" : "TextoSecundario"),
                    ToolTip = sistema.ObtenerRuta(directorio)
                };
                boton.Click += (_, _) => Navegar(directorio);

                PanelMigas.Children.Add(boton);
            }
        }

        private void ConstruirArbol()
        {
            actualizandoArbol = true;
            ArbolCarpetas.Items.Clear();
            ArbolCarpetas.Items.Add(CrearNodo(sistema.Raiz));
            actualizandoArbol = false;
        }

        private TreeViewItem CrearNodo(DirectorioVirtual directorio)
        {
            bool esRaiz = directorio == sistema.Raiz;

            var encabezado = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            encabezado.Children.Add(new TextBlock
            {
                Style = (Style)FindResource("Icono"),
                Text = esRaiz ? "" : ElementoLista.GlifoCarpeta,
                Foreground = (Brush)FindResource(esRaiz ? "TextoSecundario" : "Carpeta"),
                Margin = new Thickness(0, 0, 8, 0)
            });
            encabezado.Children.Add(new TextBlock { Text = esRaiz ? "Raíz" : directorio.Nombre });

            var nodo = new TreeViewItem
            {
                Header = encabezado,
                Tag = directorio,
                IsExpanded = !contraidas.Contains(directorio),
                FontSize = 13
            };

            nodo.Expanded += (s, e) => { if (e.OriginalSource == nodo) contraidas.Remove(directorio); };
            nodo.Collapsed += (s, e) => { if (e.OriginalSource == nodo) contraidas.Add(directorio); };

            foreach (var hijo in directorio.Directorios)
                nodo.Items.Add(CrearNodo(hijo));

            if (directorio == sistema.DirectorioActual && busqueda.Length == 0)
                nodo.IsSelected = true;

            return nodo;
        }

        // ------------------------------------------------------------ panel de detalles

        private void ActualizarDetalles()
        {
            PanelDetalles.Children.Clear();

            switch (ElementoSeleccionado())
            {
                case ArchivoVirtual archivo:
                    DetallesArchivo(archivo);
                    break;

                case DirectorioVirtual directorio:
                    DetallesCarpeta(directorio, seleccionada: true);
                    break;

                default:
                    DetallesCarpeta(sistema.DirectorioActual, seleccionada: false);
                    break;
            }
        }

        private void DetallesArchivo(ArchivoVirtual archivo)
        {
            string ruta = RutaDe(archivo);

            AgregarEncabezadoDetalle(ElementoLista.GlifoArchivo, "Primario", "PrimarioSuave", archivo.Nombre, ruta);

            AgregarSeccion("INFORMACIÓN");
            AgregarFila("Tamaño", $"{archivo.Tamaño} bytes");
            AgregarFila("En disco", $"{archivo.Bloques.Count} × {GestorBloques.TamañoBloque} B = {archivo.Bloques.Count * GestorBloques.TamañoBloque} B");
            AgregarFila("Creado", archivo.FechaCreacion.ToString("dd/MM/yyyy HH:mm"));
            AgregarFila("Modificado", archivo.FechaModificacion.ToString("dd/MM/yyyy HH:mm"));

            AgregarSeccion("BLOQUES ASIGNADOS");
            var bloques = new WrapPanel();
            if (archivo.Bloques.Count == 0)
            {
                bloques.Children.Add(TextoSecundario("Sin bloques: el archivo está vacío."));
            }
            foreach (int id in archivo.Bloques)
                bloques.Children.Add(Chip(id.ToString(), "PrimarioSuave", "Primario", mono: true));
            PanelDetalles.Children.Add(bloques);

            AgregarSeccion("PERMISOS");
            var permisos = new WrapPanel();
            foreach (var (permiso, nombre) in new[] { (Permisos.Lectura, "Lectura"), (Permisos.Escritura, "Escritura"), (Permisos.Eliminacion, "Eliminación") })
            {
                bool tiene = archivo.Permisos.HasFlag(permiso);
                permisos.Children.Add(Chip((tiene ? "✓ " : "✕ ") + nombre,
                    tiene ? "ExitoSuave" : "PeligroSuave", tiene ? "Exito" : "Peligro"));
            }
            PanelDetalles.Children.Add(permisos);

            AgregarSeccion("VISTA PREVIA");
            string vista = !archivo.Permisos.HasFlag(Permisos.Lectura)
                ? "Sin permiso de lectura."
                : archivo.Contenido.Length == 0
                    ? "(archivo vacío)"
                    : archivo.Contenido.Length > 240 ? archivo.Contenido[..240] + "…" : archivo.Contenido;

            PanelDetalles.Children.Add(new Border
            {
                Background = (Brush)FindResource("Fondo"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                ClipToBounds = true,
                Child = new TextBlock
                {
                    Text = vista,
                    MaxHeight = 150,
                    FontFamily = (FontFamily)FindResource("Monoespaciada"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (Brush)FindResource("TextoNormal")
                }
            });

            bool escribible = archivo.Permisos.HasFlag(Permisos.Escritura);
            AgregarBotonDetalle(escribible ? "" : "", escribible ? "Editar contenido" : "Abrir (solo lectura)",
                true, (_, _) => AbrirArchivo(archivo));
            AgregarBotonDetalle("", "Ver sus bloques en el mapa", false, (_, _) => MostrarMapa(ruta));
        }

        private void DetallesCarpeta(DirectorioVirtual directorio, bool seleccionada)
        {
            bool esRaiz = directorio == sistema.Raiz;
            string ruta = sistema.ObtenerRuta(directorio);

            AgregarEncabezadoDetalle(esRaiz ? "" : ElementoLista.GlifoCarpeta,
                esRaiz ? "TextoSecundario" : "Carpeta", esRaiz ? "Hover" : "#FDF4E3",
                esRaiz ? "Raíz" : directorio.Nombre, seleccionada ? ruta : "Carpeta actual · " + ruta);

            var archivos = TodosLosArchivos(directorio).ToList();

            AgregarSeccion("INFORMACIÓN");
            AgregarFila("Carpetas", directorio.Directorios.Count.ToString());
            AgregarFila("Archivos", directorio.Archivos.Count.ToString());
            AgregarFila("Tamaño total", $"{archivos.Sum(a => a.Tamaño)} bytes");
            AgregarFila("Bloques usados", archivos.Sum(a => a.Bloques.Count).ToString());
            AgregarFila("Creada", directorio.FechaCreacion.ToString("dd/MM/yyyy HH:mm"));

            if (seleccionada)
            {
                AgregarBotonDetalle("", "Abrir carpeta", true, (_, _) => Navegar(directorio));
            }
            else
            {
                AgregarSeccion("SUGERENCIA");
                PanelDetalles.Children.Add(TextoSecundario(
                    "Selecciona un archivo para ver sus metadatos, los bloques que ocupa y una vista previa de su contenido."));
            }
        }

        private void AgregarEncabezadoDetalle(string glifo, string colorIcono, string colorFondo, string titulo, string subtitulo)
        {
            var fondo = colorFondo.StartsWith('#')
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorFondo))
                : (Brush)FindResource(colorFondo);

            PanelDetalles.Children.Add(new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(12),
                Background = fondo,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Style = (Style)FindResource("Icono"),
                    Text = glifo,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (Brush)FindResource(colorIcono)
                }
            });

            PanelDetalles.Children.Add(new TextBlock
            {
                Text = titulo,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Texto"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 2)
            });

            PanelDetalles.Children.Add(TextoSecundario(subtitulo));
        }

        private void AgregarSeccion(string titulo)
        {
            PanelDetalles.Children.Add(new TextBlock
            {
                Text = titulo,
                Style = (Style)FindResource("Etiqueta"),
                Margin = new Thickness(0, 18, 0, 8)
            });
        }

        private void AgregarFila(string etiqueta, string valor)
        {
            var fila = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var nombre = TextoSecundario(etiqueta);
            nombre.Width = 105;
            DockPanel.SetDock(nombre, Dock.Left);
            fila.Children.Add(nombre);
            fila.Children.Add(new TextBlock
            {
                Text = valor,
                FontSize = 12,
                Foreground = (Brush)FindResource("Texto"),
                TextWrapping = TextWrapping.Wrap
            });
            PanelDetalles.Children.Add(fila);
        }

        private void AgregarBotonDetalle(string glifo, string texto, bool primario, RoutedEventHandler accion)
        {
            var contenido = new StackPanel { Orientation = Orientation.Horizontal };
            contenido.Children.Add(new TextBlock
            {
                Style = (Style)FindResource("Icono"),
                Text = glifo,
                Margin = new Thickness(0, 0, 8, 0)
            });
            contenido.Children.Add(new TextBlock { Text = texto });

            var boton = new Button
            {
                Style = (Style)FindResource(primario ? "BotonPrimario" : "BotonBase"),
                Content = contenido,
                Margin = new Thickness(0, primario ? 20 : 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            boton.Click += accion;
            PanelDetalles.Children.Add(boton);
        }

        private TextBlock TextoSecundario(string texto) => new()
        {
            Text = texto,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextoSecundario"),
            TextWrapping = TextWrapping.Wrap
        };

        private Border Chip(string texto, string fondo, string color, bool mono = false)
        {
            var bloque = new TextBlock
            {
                Text = texto,
                FontSize = 12,
                Foreground = (Brush)FindResource(color)
            };

            if (mono)
                bloque.FontFamily = (FontFamily)FindResource("Monoespaciada");

            return new Border
            {
                Style = (Style)FindResource("Chip"),
                Background = (Brush)FindResource(fondo),
                Child = bloque
            };
        }

        // ------------------------------------------------------------ utilidades

        private object? ElementoSeleccionado()
        {
            return (ListaArchivos.SelectedItem as ElementoLista)?.Objeto;
        }

        private void Avisar(string mensaje, TipoAviso tipo = TipoAviso.Info)
        {
            var (glifo, color) = tipo switch
            {
                TipoAviso.Exito => ("", "Exito"),
                TipoAviso.Error => ("", "Peligro"),
                _ => ("", "TextoSecundario")
            };

            TxtNotificacionIcono.Text = glifo;
            TxtNotificacionIcono.Foreground = (Brush)FindResource(color);
            TxtNotificacion.Text = mensaje;
            TxtNotificacion.Foreground = (Brush)FindResource(tipo == TipoAviso.Info ? "TextoSecundario" : color);
        }

        private static bool EsErrorEsperado(Exception ex) =>
            ex is InvalidOperationException or ArgumentException or
                  UnauthorizedAccessException or IOException;

        private static string Plural(int n, string singular, string plural) =>
            $"{n} {(n == 1 ? singular : plural)}";

        private static string DescribirBloques(IReadOnlyCollection<int> bloques) =>
            bloques.Count == 0 ? "sin bloques asignados"
                : $"{Plural(bloques.Count, "bloque", "bloques")} [{string.Join(", ", bloques)}]";

        private string RutaDe(ArchivoVirtual archivo)
        {
            var padre = BuscarPadre(sistema.Raiz, archivo) ?? sistema.DirectorioActual;
            return SistemaArchivos.UnirRuta(sistema.ObtenerRuta(padre), archivo.Nombre);
        }

        private static DirectorioVirtual? BuscarPadre(DirectorioVirtual desde, object elemento)
        {
            if (desde.Archivos.Contains(elemento) || desde.Directorios.Contains(elemento))
                return desde;

            return desde.Directorios
                .Select(d => BuscarPadre(d, elemento))
                .FirstOrDefault(p => p != null);
        }

        private static IEnumerable<ArchivoVirtual> TodosLosArchivos(DirectorioVirtual directorio) =>
            directorio.Archivos.Concat(directorio.Directorios.SelectMany(TodosLosArchivos));

        // Cambia de carpeta (y opcionalmente selecciona un elemento dentro de ella).
        private void Navegar(DirectorioVirtual destino, object? seleccionar = null)
        {
            busqueda = "";
            if (CajaBusqueda.Text.Length > 0)
                CajaBusqueda.Text = "";

            sistema.CambiarDirectorio(sistema.ObtenerRuta(destino));
            ActualizarVista(seleccionar ?? new object());
        }

        // En la búsqueda los resultados pueden estar en otras carpetas: antes de
        // operar sobre uno, se navega a su carpeta para que sea el directorio actual.
        private void UbicarElemento(object elemento)
        {
            if (busqueda.Length == 0)
                return;

            var padre = BuscarPadre(sistema.Raiz, elemento);
            if (padre != null)
                Navegar(padre, elemento);
        }

        // ------------------------------------------------------------ operaciones

        private void CrearArchivo_Click(object sender, RoutedEventArgs e)
        {
            if (busqueda.Length > 0)
                Navegar(sistema.DirectorioActual);

            var editor = new EditorArchivo(ModoEditor.Nuevo, "", "", sistema.GestorBloques, 0,
                (nombre, contenido) =>
                {
                    try
                    {
                        sistema.CrearArchivo(nombre, contenido);
                        return null;
                    }
                    catch (Exception ex) when (EsErrorEsperado(ex))
                    {
                        return ex.Message;
                    }
                })
            { Owner = this };

            if (editor.ShowDialog() != true)
                return;

            var archivo = sistema.DirectorioActual.Archivos.Last();
            ActualizarVista(archivo);
            Avisar($"Archivo «{archivo.Nombre}» creado · {DescribirBloques(archivo.Bloques)}", TipoAviso.Exito);
        }

        private void CrearCarpeta_Click(object sender, RoutedEventArgs e)
        {
            if (busqueda.Length > 0)
                Navegar(sistema.DirectorioActual);

            string? nombre = Dialogo.PedirTexto(this, "Nueva carpeta",
                $"Se creará dentro de {sistema.ObtenerRutaActual()}", "Crear",
                texto =>
                {
                    try
                    {
                        sistema.CrearDirectorio(texto);
                        return null;
                    }
                    catch (Exception ex) when (EsErrorEsperado(ex))
                    {
                        return ex.Message;
                    }
                });

            if (nombre == null)
                return;

            var carpeta = sistema.DirectorioActual.Directorios.Last();
            ActualizarVista(carpeta);
            Avisar($"Carpeta «{carpeta.Nombre}» creada", TipoAviso.Exito);
        }

        private void EditarArchivo_Click(object sender, RoutedEventArgs e)
        {
            if (ElementoSeleccionado() is ArchivoVirtual archivo)
                AbrirArchivo(archivo);
        }

        // Abre el editor; si el archivo no admite escritura, lo abre en solo lectura.
        private void AbrirArchivo(ArchivoVirtual archivo)
        {
            UbicarElemento(archivo);

            if (!archivo.Permisos.HasFlag(Permisos.Lectura))
            {
                Avisar($"Permiso denegado: «{archivo.Nombre}» no tiene permiso de lectura. Puedes cambiarlo en Propiedades.", TipoAviso.Error);
                return;
            }

            bool escribible = archivo.Permisos.HasFlag(Permisos.Escritura);
            var bloquesAntes = new List<int>(archivo.Bloques);

            var editor = new EditorArchivo(
                escribible ? ModoEditor.Editar : ModoEditor.SoloLectura,
                archivo.Nombre,
                sistema.LeerArchivo(archivo.Nombre),
                sistema.GestorBloques,
                archivo.Bloques.Count,
                (nombre, contenido) =>
                {
                    try
                    {
                        sistema.EscribirArchivo(nombre, contenido);
                        return null;
                    }
                    catch (Exception ex) when (EsErrorEsperado(ex))
                    {
                        return ex.Message;
                    }
                })
            { Owner = this };

            if (editor.ShowDialog() != true)
                return;

            var asignados = archivo.Bloques.Except(bloquesAntes).ToList();
            var liberados = bloquesAntes.Except(archivo.Bloques).ToList();

            string cambios = (asignados.Count, liberados.Count) switch
            {
                (> 0, _) => $" · bloques asignados [{string.Join(", ", asignados)}]",
                (_, > 0) => $" · bloques liberados [{string.Join(", ", liberados)}]",
                _ => ""
            };

            ActualizarVista(archivo);
            Avisar($"«{archivo.Nombre}» guardado: {archivo.Tamaño} bytes en {DescribirBloques(archivo.Bloques)}{cambios}", TipoAviso.Exito);
        }

        // Doble clic o Entrar: abre la carpeta o el archivo seleccionado.
        private void Abrir_Click(object sender, RoutedEventArgs e)
        {
            if (e is MouseButtonEventArgs clic &&
                ItemsControl.ContainerFromElement(ListaArchivos, (DependencyObject)clic.OriginalSource) == null)
            {
                return;
            }

            switch (ElementoSeleccionado())
            {
                case DirectorioVirtual directorio:
                    Navegar(directorio);
                    break;

                case ArchivoVirtual archivo:
                    AbrirArchivo(archivo);
                    break;
            }
        }

        private void Subir_Click(object sender, RoutedEventArgs e)
        {
            if (busqueda.Length > 0)
            {
                CajaBusqueda.Text = "";
                return;
            }

            var actual = sistema.DirectorioActual;
            if (actual.Padre != null)
                Navegar(actual.Padre, actual);
        }

        private void Raiz_Click(object sender, RoutedEventArgs e) => Navegar(sistema.Raiz);

        private void Actualizar_Click(object sender, RoutedEventArgs e) => ActualizarVista();

        private void Almacenamiento_Click(object sender, RoutedEventArgs e) => MostrarMapa(null);

        private void VerEnMapa_Click(object sender, RoutedEventArgs e)
        {
            if (ElementoSeleccionado() is ArchivoVirtual archivo)
                MostrarMapa(RutaDe(archivo));
        }

        private void MostrarMapa(string? resaltar)
        {
            new VentanaAlmacenamiento(sistema, resaltar) { Owner = this }.ShowDialog();
        }

        // Metadatos y permisos del elemento seleccionado.
        private void Propiedades_Click(object sender, RoutedEventArgs e)
        {
            switch (ElementoSeleccionado())
            {
                case ArchivoVirtual archivo:
                    UbicarElemento(archivo);
                    var ventana = new VentanaPropiedades(sistema, archivo) { Owner = this };

                    if (ventana.ShowDialog() != true)
                        return;

                    try
                    {
                        sistema.CambiarPermisos(archivo.Nombre, ventana.PermisosElegidos);
                        ActualizarVista(archivo);
                        Avisar($"Permisos de «{archivo.Nombre}»: {archivo.Permisos.ComoTexto()}", TipoAviso.Exito);
                    }
                    catch (Exception ex) when (EsErrorEsperado(ex))
                    {
                        Avisar(ex.Message, TipoAviso.Error);
                    }
                    break;

                case DirectorioVirtual directorio:
                    var archivos = TodosLosArchivos(directorio).ToList();
                    Dialogo.Avisar(this, directorio.Nombre,
                        $"Ruta: {sistema.ObtenerRuta(directorio)}\n" +
                        $"Contenido: {Plural(directorio.Directorios.Count, "carpeta", "carpetas")} y " +
                        $"{Plural(directorio.Archivos.Count, "archivo", "archivos")}\n" +
                        $"Tamaño total: {archivos.Sum(a => a.Tamaño)} bytes en {archivos.Sum(a => a.Bloques.Count)} bloques\n" +
                        $"Creada: {directorio.FechaCreacion:dd/MM/yyyy HH:mm:ss}");
                    break;
            }
        }

        private void Eliminar_Click(object sender, RoutedEventArgs e)
        {
            object? elemento = ElementoSeleccionado();
            if (elemento == null)
                return;

            UbicarElemento(elemento);

            if (elemento is DirectorioVirtual carpeta)
            {
                if (carpeta.Archivos.Count > 0 || carpeta.Directorios.Count > 0)
                {
                    Avisar($"La carpeta «{carpeta.Nombre}» no está vacía. Elimina primero su contenido.", TipoAviso.Error);
                    return;
                }

                if (!Dialogo.Confirmar(this, $"¿Eliminar la carpeta «{carpeta.Nombre}»?",
                        "La carpeta está vacía y se quitará del árbol de directorios.", "Eliminar", peligro: true))
                    return;

                Ejecutar(() => sistema.EliminarDirectorio(carpeta.Nombre),
                    $"Carpeta «{carpeta.Nombre}» eliminada");
                return;
            }

            var archivo = (ArchivoVirtual)elemento;

            if (!archivo.Permisos.HasFlag(Permisos.Eliminacion))
            {
                Avisar($"Permiso denegado: «{archivo.Nombre}» no tiene permiso de eliminación.", TipoAviso.Error);
                return;
            }

            var bloques = new List<int>(archivo.Bloques);

            if (!Dialogo.Confirmar(this, $"¿Eliminar «{archivo.Nombre}»?",
                    $"Se liberarán {DescribirBloques(bloques)}. Esta acción no se puede deshacer.",
                    "Eliminar", peligro: true))
                return;

            Ejecutar(() => sistema.EliminarArchivo(archivo.Nombre),
                $"Archivo «{archivo.Nombre}» eliminado · " +
                (bloques.Count > 0 ? $"bloques liberados [{string.Join(", ", bloques)}]" : "no tenía bloques"));
        }

        private void Ejecutar(Action operacion, string mensajeExito)
        {
            try
            {
                operacion();
                ActualizarVista();
                Avisar(mensajeExito, TipoAviso.Exito);
            }
            catch (Exception ex) when (EsErrorEsperado(ex))
            {
                Avisar(ex.Message, TipoAviso.Error);
            }
        }

        // ------------------------------------------------------------ importar

        private void ImportarArchivo_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar archivos de texto",
                Filter =
                    "Archivos de texto y código|*.txt;*.json;*.csv;*.xml;*.cs;*.html;*.css;*.js;*.py;*.md;*.log|" +
                    "Todos los archivos|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialogo.ShowDialog(this) == true)
                Importar(dialogo.FileNames);
        }

        private void Ventana_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Ventana_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] rutas)
                Importar(rutas.Where(File.Exists));
        }

        // Copia archivos de texto del equipo al directorio actual del disco virtual.
        private void Importar(IEnumerable<string> rutas)
        {
            if (busqueda.Length > 0)
                Navegar(sistema.DirectorioActual);

            var importados = new List<ArchivoVirtual>();
            var errores = new List<string>();

            foreach (string ruta in rutas)
            {
                string nombre = Path.GetFileName(ruta);

                try
                {
                    string contenido = File.ReadAllText(ruta);

                    // El contenido se guarda como texto; un binario quedaría ilegible.
                    if (contenido.Contains('\0'))
                        throw new InvalidOperationException("solo se pueden importar archivos de texto.");

                    sistema.CrearArchivo(nombre, contenido);
                    importados.Add(sistema.ObtenerArchivo(nombre));
                }
                catch (Exception ex) when (EsErrorEsperado(ex))
                {
                    errores.Add($"{nombre}: {ex.Message}");
                }
            }

            ActualizarVista(importados.LastOrDefault());

            if (importados.Count == 1)
                Avisar($"«{importados[0].Nombre}» importado · {importados[0].Tamaño} bytes en {DescribirBloques(importados[0].Bloques)}", TipoAviso.Exito);
            else if (importados.Count > 1)
                Avisar($"{importados.Count} archivos importados", TipoAviso.Exito);

            if (errores.Count > 0)
            {
                Avisar($"No se pudieron importar {Plural(errores.Count, "archivo", "archivos")}", TipoAviso.Error);
                Dialogo.Avisar(this, "Algunos archivos no se importaron", string.Join("\n", errores), advertencia: true);
            }
        }

        // ------------------------------------------------------------ búsqueda, árbol y teclado

        private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e)
        {
            busqueda = CajaBusqueda.Text.Trim();
            ActualizarVista();
        }

        private void CerrarBusqueda_Click(object sender, RoutedEventArgs e)
        {
            CajaBusqueda.Text = "";
        }

        private void ArbolCarpetas_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (actualizandoArbol)
                return;

            if (e.NewValue is TreeViewItem { Tag: DirectorioVirtual directorio } &&
                (directorio != sistema.DirectorioActual || busqueda.Length > 0))
            {
                // Se navega después de que el árbol termine de procesar el clic.
                Dispatcher.BeginInvoke(() => Navegar(directorio));
            }
        }

        private void ListaArchivos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarAcciones();
            ActualizarDetalles();
        }

        private void ListaArchivos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                Abrir_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                Eliminar_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ListaArchivos_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            object? elemento = ElementoSeleccionado();
            var visible = elemento != null ? Visibility.Visible : Visibility.Collapsed;

            MenuAbrir.Visibility = visible;
            MenuPropiedades.Visibility = visible;
            MenuEliminar.Visibility = visible;
            MenuEditar.Visibility = elemento is ArchivoVirtual ? Visibility.Visible : Visibility.Collapsed;
            MenuMapa.Visibility = elemento is ArchivoVirtual ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool mayus = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool escribiendo = Keyboard.FocusedElement is TextBox;
            Key tecla = e.Key == Key.System ? e.SystemKey : e.Key;
            bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

            switch (tecla)
            {
                case Key.N when ctrl && mayus:
                    CrearCarpeta_Click(sender, e);
                    break;
                case Key.N when ctrl:
                    CrearArchivo_Click(sender, e);
                    break;
                case Key.F when ctrl:
                    CajaBusqueda.Focus();
                    CajaBusqueda.SelectAll();
                    break;
                case Key.T when ctrl:
                    Terminal_Click(sender, e);
                    break;
                case Key.E when ctrl && BtnEditar.IsEnabled:
                    EditarArchivo_Click(sender, e);
                    break;
                case Key.F5:
                    ActualizarVista();
                    break;
                case Key.F1:
                    Informacion_Click(sender, e);
                    break;
                case Key.Escape when busqueda.Length > 0:
                    CajaBusqueda.Text = "";
                    ListaArchivos.Focus();
                    break;
                case Key.Down when CajaBusqueda.IsKeyboardFocused && ListaArchivos.Items.Count > 0:
                    ListaArchivos.SelectedIndex = 0;
                    ((ListViewItem)ListaArchivos.ItemContainerGenerator.ContainerFromIndex(0))?.Focus();
                    break;
                case Key.Enter when alt && BtnPropiedades.IsEnabled:
                    Propiedades_Click(sender, e);
                    break;
                case Key.Home when alt:
                    Navegar(sistema.Raiz);
                    break;
                case Key.Up when alt:
                case Key.Back when !escribiendo:
                    Subir_Click(sender, e);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        // ------------------------------------------------------------ otras ventanas

        // Abrir la terminal de comandos (una sola a la vez).
        private void Terminal_Click(object sender, RoutedEventArgs e)
        {
            if (terminal == null || !terminal.IsLoaded)
            {
                terminal = new VentanaTerminal(sistema, () => ActualizarVista()) { Owner = this };
                terminal.Show();
            }
            else
            {
                terminal.Activate();
            }
        }

        private void Informacion_Click(object sender, RoutedEventArgs e)
        {
            Dialogo.Avisar(this, "Winzard Explorer",
                "Sistema de archivos virtual: 100 bloques de 64 bytes con asignación indexada, " +
                "permisos por archivo y persistencia en JSON.\n\n" +
                "Atajos de teclado\n" +
                "Ctrl+N  nuevo archivo  ·  Ctrl+Mayús+N  nueva carpeta\n" +
                "Entrar  abrir  ·  Ctrl+E  editar  ·  Supr  eliminar\n" +
                "Alt+Entrar  propiedades  ·  Retroceso  subir de nivel\n" +
                "Ctrl+F  buscar  ·  Esc  cerrar búsqueda  ·  F5  actualizar\n" +
                "Ctrl+T  terminal (escribe help para ver los comandos)");
        }
    }
}
