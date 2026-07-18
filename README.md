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
- Mezclador real de volumen general y por aplicación.
- Comandos locales para controlar Discord, Spotify, Zen y otras sesiones de audio.
- Voz local push-to-talk mediante Whisper, sin enviar el audio a una API.
- Activación opcional mediante `Nexo` u `Oye Nexo`, usando un detector Vosk local.
- Activación local opcional mediante “Nexo” u “Oye Nexo”.
- Reconocimiento de órdenes mediante Whisper.

La primera ejecución de voz descarga el modelo multilingüe `base` en `%LocalAppData%\Nexo\Models`. El modelo solo se carga en memoria mientras Nexo transcribe una orden.

La activación por frase es experimental y permanece apagada de forma predeterminada. Al habilitarla, Nexo descarga el modelo español pequeño de Vosk y mantiene un indicador visible mientras está atento. Para la primera versión se usa un flujo de dos pasos: di `Nexo`, espera la cápsula **Te escucho** y después pronuncia la orden.

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

- Permitir decir `Nexo` y la orden completa en una sola frase.
- Perfiles de voz rápido y equilibrado.
- Proveedores de IA.
- Herramientas de captura y OCR.
