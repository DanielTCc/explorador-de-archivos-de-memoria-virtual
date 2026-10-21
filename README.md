# Winzard Explorer

**Explorador de archivos de memoria virtual desarrollado en C# y WPF** como proyecto académico para la asignatura de Arquitectura de Sistemas Operativos.

## 📌 Descripción

Winzard Explorer es una aplicación de escritorio que simula un sistema de archivos virtual. Permite organizar directorios y archivos dentro de un entorno controlado, administrar el espacio de almacenamiento mediante bloques y conservar los datos entre ejecuciones utilizando persistencia JSON.

El proyecto busca aplicar conceptos fundamentales de los sistemas operativos mediante una interfaz gráfica sencilla e intuitiva.

## ✨ Funcionalidades

* 📁 Crear, eliminar y navegar entre directorios virtuales (rutas como `a/b`, `..`, `/`).
* 📄 Crear archivos, leerlos, escribir o agregar contenido y eliminarlos.
* ℹ️ Ver los metadatos de cada archivo: tamaño, bloques, fechas y permisos.
* 🔒 Permisos básicos por archivo: lectura (r), escritura (w) y eliminación (d).
* 🔍 Buscar archivos y carpetas en todo el disco (admite `*` y `?`).
* 🧱 Disco virtual de 100 bloques de 64 bytes con asignación y liberación de bloques y un mapa visual.
* ⌨️ Terminal de comandos integrada (`mkdir`, `cd`, `create`, `write`, `read`, `ls`, `delete`, `exit`...).
* 📥 Importar archivos de texto desde el equipo al sistema virtual.
* 💾 Guardar y recuperar el sistema de archivos (incluidos los bloques) mediante JSON.
* 🖥️ Interfaz gráfica de escritorio desarrollada con WPF.

## ⌨️ Terminal

Desde el menú lateral **Terminal** se pueden usar comandos. Escribe `help` para ver la lista completa:

```text
/ > mkdir documentos
/ > cd documentos
/documentos > create tarea.txt
/documentos > write tarea.txt "Sistema Operativo"
"tarea.txt": 17 bytes, 1 bloque(s) [0].
  Bloques asignados: [0]
/documentos > read tarea.txt
Sistema Operativo
/documentos > delete tarea.txt
Archivo "tarea.txt" eliminado. Bloques liberados: [0]
```

Otros comandos: `ls -l`, `append`, `info`, `chmod`, `find`, `df`, `blocks`, `pwd`, `clear`.

## 🛠️ Tecnologías utilizadas

* **Lenguaje:** C#
* **Interfaz gráfica:** Windows Presentation Foundation (WPF)
* **Plataforma:** .NET
* **Persistencia:** JSON
* **Entorno de desarrollo:** Visual Studio
* **Control de versiones:** Git y GitHub

## 📂 Estructura del proyecto

La organización principal del código se divide en las siguientes áreas:

* `Models/`: clases que representan los archivos, directorios, bloques y permisos.
* `Services/`: lógica del sistema de archivos (`SistemaArchivos`), administración de bloques (`GestorBloques`) e intérprete de comandos (`InterpreteComandos`).
* `MainWindow.xaml`: ventana principal con la lista detallada de archivos.
* `VentanaTerminal`, `EditorArchivo`, `VentanaPropiedades` y `VentanaAlmacenamiento`: terminal, editor, propiedades/permisos y mapa de bloques.
* `winzar_EXPLORER.Tests/`: pruebas automatizadas con xUnit.

## 🚀 Instalación y ejecución

1. Clona el repositorio:

   ```bash
   git clone https://github.com/DanielTCc/explorador-de-archivos-de-memoria-virtual.git
   ```

2. Entra en la carpeta del proyecto:

   ```bash
   cd explorador-de-archivos-de-memoria-virtual
   ```

3. Abre la solución `winzar_EXPLORER.slnx` en Visual Studio.

4. Restaura las dependencias si es necesario.

5. Compila y ejecuta el proyecto desde Visual Studio.

**Requisito:** utilizar Windows y una instalación de Visual Studio con soporte para desarrollo de aplicaciones de escritorio con .NET y WPF.

## 🧪 Pruebas

Las pruebas automatizadas cubren las operaciones de archivos y directorios, la asignación de bloques, los permisos, la búsqueda, la persistencia, el manejo de errores y la secuencia de la demostración. Usan discos y archivos temporales, sin tocar tus datos:

```bash
dotnet test winzar_EXPLORER.slnx
```

## 💾 Persistencia de datos

La aplicación guarda el estado del sistema de archivos en un archivo JSON ubicado en el directorio local de datos de la aplicación del usuario:

```text
%LocalAppData%\WinzardExplorer\filesystem.json
```

Este archivo permite recuperar los directorios y archivos virtuales al volver a iniciar la aplicación.

## 🎓 Propósito académico

Este proyecto permite poner en práctica conceptos relacionados con:

* Sistemas de archivos y directorios.
* Gestión de bloques de almacenamiento.
* Operaciones básicas sobre archivos.
* Separación entre interfaz gráfica, modelos y lógica de negocio.
* Persistencia de información.
* Desarrollo de aplicaciones de escritorio con C#.

## 📌 Estado del proyecto

**En desarrollo.** Las funcionalidades y la arquitectura pueden ampliarse durante el desarrollo académico.

## 👨‍💻 Autor

**Daniel Tijerino**

GitHub: [@DanielTCc](https://github.com/DanielTCc)

## 📄 Licencia

Proyecto académico. No se ha especificado una licencia de distribución.
