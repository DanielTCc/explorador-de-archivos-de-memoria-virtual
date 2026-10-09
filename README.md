# Winzard Explorer

**Explorador de archivos de memoria virtual desarrollado en C# y WPF** como proyecto académico para la asignatura de Arquitectura de Sistemas Operativos.

## 📌 Descripción

Winzard Explorer es una aplicación de escritorio que simula un sistema de archivos virtual. Permite organizar directorios y archivos dentro de un entorno controlado, administrar el espacio de almacenamiento mediante bloques y conservar los datos entre ejecuciones utilizando persistencia JSON.

El proyecto busca aplicar conceptos fundamentales de los sistemas operativos mediante una interfaz gráfica sencilla e intuitiva.

## ✨ Funcionalidades

* 📁 Crear y navegar entre directorios virtuales.
* 📄 Crear archivos virtuales y almacenar su contenido.
* 🗑️ Eliminar archivos y directorios.
* 📥 Importar archivos de texto desde el equipo al sistema virtual.
* 💾 Guardar y recuperar la estructura del sistema de archivos mediante JSON.
* 🧱 Administrar bloques de almacenamiento virtual.
* 📊 Consultar información del almacenamiento y el estado del sistema.
* 🖥️ Interfaz gráfica de escritorio desarrollada con WPF.

## 🛠️ Tecnologías utilizadas

* **Lenguaje:** C#
* **Interfaz gráfica:** Windows Presentation Foundation (WPF)
* **Plataforma:** .NET
* **Persistencia:** JSON
* **Entorno de desarrollo:** Visual Studio
* **Control de versiones:** Git y GitHub

## 📂 Estructura del proyecto

La organización principal del código se divide en las siguientes áreas:

* `Models/`: contiene las clases que representan los archivos, directorios y bloques virtuales.
* `Services/`: contiene la lógica del sistema de archivos y la administración del almacenamiento.
* `MainWindow.xaml`: define la interfaz gráfica.
* `MainWindow.xaml.cs`: controla las interacciones de la interfaz con el sistema de archivos.

## 🚀 Instalación y ejecución

1. Clona el repositorio:

   ```bash
   git clone https://github.com/DanielTCc/explorador-de-archivos-de-memoria-virtual.git
   ```

2. Entra en la carpeta del proyecto:

   ```bash
   cd explorador-de-archivos-de-memoria-virtual
   ```

3. Abre la solución `.sln` en Visual Studio.

4. Restaura las dependencias si es necesario.

5. Compila y ejecuta el proyecto desde Visual Studio.

**Requisito:** utilizar Windows y una instalación de Visual Studio con soporte para desarrollo de aplicaciones de escritorio con .NET y WPF.

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
