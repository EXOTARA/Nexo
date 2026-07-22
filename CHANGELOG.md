## Voice Reliability v1

- “Hey Nexo” acepta variantes frecuentes de reconocimiento como `ey`, `ei`, `ahí` y `neso`.
- El traspaso Vosk → Whisper conserva audio previo y una ventana posterior separada.
- La orden puede durar hasta 20 segundos y solo termina después de 1.5 segundos de silencio.
- Una ventana protegida de 2.5 segundos evita que la cola de la frase de activación corte la consulta.
- Detección de voz con confirmación temporal, umbral más sensible e histéresis.
- El micrófono manual cambia a pulsar para iniciar y pulsar para terminar.
- Prompt de Whisper ampliado para preguntas visuales y conversación natural.
- Métricas privadas de captura en `voice-capture.log`, sin audio ni transcripciones.

## Resource Governor v1 + Silent Voice Look

- Estados Normal, Busy y Game basados en pantalla completa y uso de CPU/GPU/RAM.
- Modo Juego pausa wake word, Vision, IA y cápsulas transitorias sin bloquear comandos locales.
- Modo Busy protege Vision y runtimes locales; conserva proveedores remotos ya configurados.
- Indicador de estado y controles de rendimiento adaptativo en Personalización.
- Nueva frase configurable “Hey Nexo”.
- “Hey Nexo, ¿qué es esto?” adquiere contexto visual de forma silenciosa y temporal.
- Registro en `resource-governor.log` y nuevas pruebas de política.

# Changelog

Todos los cambios importantes de Nexo se documentarán aquí.

## [Sin publicar]

- Nuevo **Look Mode** con `Ctrl + Shift + Espacio`: Nexo observa en memoria la ventana activa y abre contexto visual temporal sin selector manual.
- El contexto visual permanece hasta dos minutos para hacer preguntas consecutivas y adjunta aplicación, título y dimensiones visibles.
- El compositor del asistente se simplifica a un campo principal con acciones visuales compactas para Mirar, voz y enviar.
- La paleta deja de reemplazar físicamente lo escrito: `Backspace` vuelve a comportarse de forma natural y `Tab` acepta la sugerencia.
- Las entradas recientes incompletas se normalizan hacia su acción canónica, por ejemplo `desca` → `abre descargas`.
- `Esc` limpia primero la paleta y una segunda pulsación la cierra.
- La barra lateral contraída usa botones realmente compactos para conservar esquinas redondeadas y evitar selecciones recortadas.
- Nuevo slider visual propio y migración del workspace a un ancho legible entre 680 y 820 px.

- Nuevo **Nexo Shell v4** con Inicio tipo bento y workspace separado de la paleta.
- La barra lateral ahora empuja el contenido al expandirse y deja de cubrir el módulo activo.
- Navegación con iconos vectoriales consistentes y una entrada Inicio dedicada.
- Encabezado contextual por módulo y acceso compacto a `Ctrl + Espacio`.
- Inicio con pendientes, enfoque, contexto rápido y actividad reciente de la sesión.
- Migración visual a un ancho de workspace entre 600 y 760 px.
- Nueva base visual **Obsidian Bloom** con tipografía e iconos más legibles.
- Barra lateral expandible por clic, sin reducir el espacio permanente de trabajo.
- Paleta sin animación de escala para conservar texto nítido al aparecer.
- Entrada inteligente: comandos claros se ejecutan y prompts naturales se envían completos.
- `Ctrl + Enter` fuerza una consulta y `Shift + Enter` permite prompts multilínea.
- Scrollbars compactas, tooltips oscuros y estados de foco consistentes.
- Ancho mínimo protegido para evitar recortes en la nueva navegación.
- La copia privada de Ollama se inicia automáticamente al abrir Nexo.
- El runtime local se supervisa y se recupera si deja de responder.
- Las consultas abiertas reintentan el arranque del motor antes de fallar.
- Ollama se mantiene activo al ocultar Nexo y se detiene al salir completamente.
- Registro local de diagnóstico en `%LocalAppData%\Nexo\Logs\ollama-runtime.log`.

## 0.9.0-beta - Primera edición distribuible

- Versión y metadatos centralizados para los ensamblados.
- Ejecutable `Nexo.exe` con icono propio y manifiesto Per-Monitor V2.
- Publicación autocontenida para Windows x64 sin depender de Electron.
- ZIP portable con archivo SHA256.
- Instalador por usuario mediante Inno Setup, sin requerir administrador.
- Desinstalación limpia con opción de conservar o eliminar datos locales.
- CI de GitHub para compilación, pruebas y artefactos portables.
- Workflow de releases para etiquetas `v*`.
- Comprobación manual de actualizaciones desde el centro de diagnóstico.
- Lista de verificación para probar la beta en una instalación limpia.


### Agregado

- Barra lateral modular para Windows.
- Atajo global `Alt + A`.
- Navegación entre IA, Audio, Captura, Sistema y Ajustes.
- Temas reutilizables y personalización persistente.
- Métricas reales de CPU, RAM, GPU, VRAM y almacenamiento.
- Modo Peek mediante `Alt + Shift + A`.
- Proceso con mayor consumo de memoria.
- Intérprete inicial de comandos naturales locales.
- Compatibilidad inicial con los prefijos `Nexo` y `Exo`.
- Cápsula flotante para estados, confirmaciones y errores.
- Apertura de terminales en la carpeta del usuario.
- Historial de conversación opcional y privado.
- Límite predeterminado de ocho mensajes temporales.
- Mezclador de audio real por aplicación.
- Comandos locales de volumen y silencio.
- Entrada de voz push-to-talk con Whisper local.
- Descarga y almacenamiento local del modelo multilingüe `base`.
- Activación experimental con las frases `Nexo` y `Oye Nexo`.
- Detector local Vosk con vocabulario limitado y sin claves externas.
- Grabación automática de la orden después de la activación, con final por silencio.
- Indicador visible mientras la activación está atenta.
- Capa de proveedores de IA compatible con OpenAI, Ollama y LM Studio.
- Prueba de conexión y listado de modelos desde Personalización.
- Consultas abiertas mediante el historial reciente de la conversación.
- Opción explícita para compartir métricas resumidas con la IA.
- Lectura de claves mediante variables de entorno, sin guardarlas en la configuración.
- Streaming de respuestas para mostrar texto mientras el modelo lo genera.
- Respuestas locales para fecha y hora, sin consultar al proveedor de IA.
- Política de contexto que adjunta métricas solo cuando la pregunta trata del equipo.
- Caché temporal de Whisper para acelerar órdenes de voz consecutivas.
- Selector de micrófono compartido por Whisper y la frase de activación.
- Búfer previo para conservar el inicio de órdenes dichas junto con `Nexo`.
- Confirmación por voz cuando la grabación tiene poca claridad.
- Umbral de silencio adaptable al ruido del micrófono.
- Nexo Vision bajo demanda con selector de ventanas y monitores.
- Vista previa obligatoria antes de compartir una captura.
- Envío de imágenes a proveedores multimodales mediante contenido compatible con OpenAI.
- Exclusión inicial de gestores de contraseñas y ventanas de seguridad.
- Reducción automática de capturas grandes para limitar memoria, latencia y tamaño de solicitud.
- Orden local `Nexo, mira esto` para abrir el flujo visual.
- Proveedor nativo de Ollama mediante `/api/chat` y `/api/tags`.
- Extracción estructurada de código, archivo, línea, mensaje y comando visibles antes de explicar un error.
- Contrato de respuesta visual con causa, corrección exacta y forma de comprobarla.
- Detección y reintento de respuestas visuales genéricas.
- Respaldo directo cuando un modelo genera razonamiento pero deja vacío el contenido final.
- Módulo **Hoy** para administrar tareas pendientes, completadas y vencidas.
- Creación y edición manual de tareas con notas, prioridad, fecha y hora.
- Recordatorios locales mediante cápsulas mientras Nexo está ejecutándose.
- Persistencia privada de tareas en `%LocalAppData%\Nexo\tasks.json`.
- Órdenes locales para crear, consultar, completar y eliminar actividades sin llamar al LLM.
- Módulo **Enfoque** con temporizadores persistentes y actualización en tiempo real.
- Presets de 25 y 50 minutos, descansos de 5 y 10 minutos y duración personalizada.
- Pausa, reanudación, cancelación y recuperación de sesiones al volver a abrir Nexo.
- Resumen diario de sesiones completadas y minutos enfocados.
- Órdenes locales para iniciar y controlar temporizadores sin usar el LLM.
- Persistencia privada del estado de enfoque en `%LocalAppData%\Nexo\focus.json`.
- Icono de Nexo en la bandeja del sistema con acciones para abrir, mostrar Peek y salir completamente.
- Ejecución en segundo plano al ocultar o cerrar la barra.
- Inicio opcional con Windows mediante el registro del usuario y el argumento `--background`.
- Coordinación de instancia única para evitar dos procesos y abrir la instancia existente.
- Notificaciones de Windows para tareas y sesiones de enfoque terminadas.
- Sonidos configurables para recordatorios y temporizadores.
- Revisión inmediata de tareas, enfoque y métricas al reanudar Windows después de una suspensión.
- Asistente de configuración inicial para micrófono, Ollama, privacidad y segundo plano.
- Administración local de modelos de Ollama: listar, descargar, seleccionar y eliminar.
- Centro de diagnóstico para voz, IA, Vision, bandeja, inicio y archivos locales.
- Copia segura del diagnóstico sin conversaciones, capturas ni claves.
- Recuperación de archivos JSON dañados mediante respaldos `.corrupt-*`.
- Escritura atómica de configuración e historial para reducir corrupción ante cierres inesperados.

### Cambiado

- La cápsula ahora aparece en la parte superior central.
- La interfaz principal ya no necesita desplazamiento vertical.
- CPU, RAM y GPU sustituyen al almacenamiento en la vista rápida.
- El reconocimiento clásico de Windows se reemplaza por transcripción local con Whisper.
- Las respuestas abiertas son más breves y dejan de convertir consultas no relacionadas en diagnósticos del sistema.
- El silencio final de la escucha automática se reduce para entregar antes la orden.
- La frase de activación puede decirse junto con la orden completa.

### Pendiente

- OCR y copia de texto desde capturas.
- Difuminado manual de información sensible.
- Calibración guiada del micrófono.
- Diccionario personal de correcciones.
- Reducir todavía más el consumo del detector permanente.
- Respaldo semántico para convertir instrucciones ambiguas en acciones locales validadas.


## 0.7.0-alpha - Rutinas y motor de acciones seguras

- Editor local de rutinas con frases de activación.
- Rutinas predeterminadas para programación, estudio y descanso.
- Motor de acciones permitidas con validación, riesgo y resultados por paso.
- Vista previa y confirmación para acciones sensibles.
- Ejecución tolerante a fallos: un paso fallido no cancela los demás.
- Persistencia local en `%LocalAppData%\Nexo\routines.json`.
- Comandos por texto o voz para abrir, listar y ejecutar rutinas.
## 0.8.0-alpha - Integración con Windows

- Bandeja del sistema y funcionamiento real en segundo plano.
- Inicio opcional con Windows en modo oculto.
- Instancia única con activación de la ventana existente.
- Notificaciones y sonidos configurables para recordatorios y enfoque.
- Recuperación tras suspensión y reanudación del equipo.
- Salida completa disponible desde el menú de la bandeja.

