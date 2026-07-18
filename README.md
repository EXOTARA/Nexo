# Nexo

Nexo es un asistente ligero y modular para Windows que permite consultar, entender y controlar el equipo sin abandonar lo que estás haciendo.

## Estado

Nexo está actualmente en fase `0.3-alpha`. La aplicación ya cuenta con una interfaz funcional, métricas del sistema y una primera versión del motor de comandos locales.

Actualmente incluye:

- Barra lateral modular para Windows.
- Atajo global `Alt + A`.
- Vista rápida con CPU, RAM y GPU.
- Modo Peek mediante `Alt + Shift + A`.
- Personalización de posición, ancho, transparencia y color.
- Navegación entre IA, Audio, Captura y Sistema.
- Interpretación inicial de comandos locales.
- Apertura de PowerShell, CMD y Terminal en la carpeta del usuario.
- Cápsulas flotantes para mostrar confirmaciones y errores.
- Historial de conversación privado y opcional.

El control real del volumen por aplicación está en desarrollo.

## Objetivo

Nexo busca ser la forma más rápida de pedir, entender y controlar algo en Windows sin abandonar la aplicación que estás usando.

## Tecnología

- C#
- WPF
- .NET 10
- Windows 11

## Desarrollo

El proyecto se desarrolla mediante ramas y Pull Requests. Cada bloque nuevo incluye pruebas antes de integrarse en `main`.

## Próximos pasos

- Mezclador de audio por aplicación.
- Comandos reales de volumen.
- Reconocimiento de voz.
- Proveedores de IA.
- Herramientas de captura y OCR.