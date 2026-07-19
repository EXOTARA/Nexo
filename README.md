# Nexo

Nexo es un asistente ligero y modular para Windows que permite consultar, entender y controlar el equipo sin abandonar lo que estás haciendo.

## Estado

Nexo está actualmente en fase `0.4-alpha`. La aplicación ya cuenta con una interfaz funcional, métricas del sistema y una primera versión del motor de comandos locales.

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
- Nexo Vision bajo demanda para capturar una ventana o monitor, revisar una vista previa y hacer preguntas sobre la imagen.
- Compatibilidad visual con modelos multimodales mediante el formato compatible con OpenAI.
- Integración nativa con Ollama mediante `/api/chat`, con streaming, `keep_alive` y control explícito del modo de pensamiento.
- Diagnóstico visual técnico en dos etapas: primero extrae evidencia estructurada y después propone una corrección verificable.
- Reintento automático cuando una respuesta visual es demasiado genérica o evade el problema.
- Módulo **Hoy** con tareas, fechas, prioridades y recordatorios locales.
- Guardado privado de actividades en `%LocalAppData%\Nexo\tasks.json`.
- Órdenes locales para crear, consultar, completar y eliminar tareas sin usar la IA.
- Módulo **Enfoque** con temporizadores, sesiones de estudio, pausas y progreso diario.
- Persistencia local del temporizador activo en `%LocalAppData%\Nexo\focus.json`.
- Control por texto o voz para iniciar, pausar, continuar, consultar y cancelar sesiones.
- Icono permanente en la bandeja del sistema con acceso rápido a Nexo, Peek y salida completa.
- Segundo plano real: cerrar puede ocultar la barra sin detener tareas, voz ni temporizadores.
- Inicio opcional con Windows en modo oculto.
- Protección contra múltiples instancias y reapertura de la instancia existente.
- Notificaciones de Windows y sonidos configurables para recordatorios y sesiones terminadas.
- Recuperación automática de recordatorios, temporizadores y métricas después de suspender el equipo.
- Configuración inicial guiada para voz, Ollama, privacidad e integración con Windows.
- Administrador de modelos locales de Ollama con descarga, selección y eliminación.
- Centro de diagnóstico local con estado de micrófono, Whisper, wake word, IA, Vision y datos.
- Respaldo automático de archivos de datos dañados y limpieza de temporales.

La primera ejecución de voz descarga el modelo multilingüe `base` en `%LocalAppData%\Nexo\Models`. Después de una transcripción, Whisper puede permanecer hasta cinco minutos en memoria para acelerar órdenes consecutivas y luego se libera.

La activación por frase es experimental y permanece apagada de forma predeterminada. Al habilitarla, Nexo descarga el modelo español pequeño de Vosk y mantiene un indicador visible mientras está atento. Un búfer previo conserva el inicio del audio para poder decir `Nexo, abre PowerShell` de corrido, aunque también puedes esperar la cápsula **Te escucho**.

Nexo Vision solo captura cuando el usuario pulsa **Mirar** o usa una orden como `Nexo, mira esto`. Antes de compartir la imagen se elige una ventana o monitor y se revisa una vista previa. Los gestores de contraseñas y ventanas de seguridad se excluyen de la lista, la imagen no entra al historial y se elimina de la sesión después de una respuesta correcta o al descartarla.

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
- Selección manual de una región para ampliar errores pequeños.
- OCR y copia de texto desde capturas.
- Difuminado manual de zonas sensibles.
- Instalador de Windows, actualizaciones y publicación de la primera beta.


## Rutinas y automatización segura

Nexo incluye rutinas locales guardadas en `%LocalAppData%\Nexo\routines.json`.
Cada rutina se compone exclusivamente de acciones permitidas: abrir aplicaciones o carpetas,
abrir PowerShell en una ubicación, controlar audio, iniciar enfoque o crear tareas.
Las acciones continúan de forma independiente cuando un paso falla y los pasos sensibles
muestran una vista previa antes de ejecutarse.

Ejemplos: `modo programación`, `modo estudio`, `modo descanso`, `abre rutinas`.
## Integración con Windows

Nexo puede permanecer activo en la bandeja aunque la barra esté oculta. Desde el menú del icono puedes abrir Nexo, mostrar Peek o salir completamente. El inicio con Windows es opcional y usa el modo `--background`, por lo que la aplicación conserva recordatorios, temporizadores, atajos y la frase de activación sin mostrar la barra al iniciar sesión.

Las notificaciones y sonidos se controlan desde **Personalización → Integración con Windows**. Al reanudar el equipo después de una suspensión, Nexo vuelve a comprobar inmediatamente recordatorios y sesiones de enfoque.

