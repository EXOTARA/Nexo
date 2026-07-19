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
- Consultas reales con OpenAI, Ollama, LM Studio o servidores compatibles con la API de OpenAI.
- Contexto de métricas opcional y desactivado de forma predeterminada.
- Respuestas de IA mostradas progresivamente mientras se generan.
- Fecha y hora resueltas localmente sin gastar tokens ni esperar al modelo.

La primera ejecución de voz descarga el modelo multilingüe `base` en `%LocalAppData%\Nexo\Models`. Después de una transcripción, Whisper puede permanecer hasta cinco minutos en memoria para acelerar órdenes consecutivas y luego se libera.

La activación por frase es experimental y permanece apagada de forma predeterminada. Al habilitarla, Nexo descarga el modelo español pequeño de Vosk y mantiene un indicador visible mientras está atento. Un búfer previo conserva el inicio del audio para poder decir `Nexo, abre PowerShell` de corrido, aunque también puedes esperar la cápsula **Te escucho**.

Las órdenes conocidas se ejecutan localmente antes de consultar una IA. Las consultas abiertas pueden enviarse al proveedor configurado y se muestran conforme llegan. Nexo no guarda claves dentro del repositorio ni en `settings.json`: para OpenAI lee `OPENAI_API_KEY` desde las variables de entorno. Compartir CPU, RAM, GPU y el proceso principal con la IA es una opción separada y apagada por defecto; incluso al activarla, las métricas solo se adjuntan cuando la consulta trata del equipo.

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

- Calibración guiada de ruido y volumen del micrófono.
- Diccionario personal de correcciones de voz.
- Perfiles de voz rápido y equilibrado.
- Respaldo semántico de IA para órdenes ambiguas mediante acciones permitidas.
- Herramientas de captura y OCR.
