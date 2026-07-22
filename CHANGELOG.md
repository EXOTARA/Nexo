# Changelog

Todos los cambios públicos de Kohana se documentan aquí. Las entradas anteriores a `0.9.2-beta` conservan el nombre Nexo como registro histórico.

## 0.9.3-beta — Sakura Shell + Chat Refresh

### Agregado

- Pantalla inicial de chat con acciones rápidas para PowerShell, pendientes, enfoque y Vision.
- Composer de conversación rediseñado con placeholder, acceso visual, voz y envío.
- Set vectorial Kohana Core Modules para navegación consistente.
- Acento azul frío secundario para equilibrar la identidad sakura.

### Cambiado

- Paleta Sakura Premium más neutral y con menos rosa dominante.
- Logo público reemplazado por una marca floral lineal inspirada en el sistema de Figma.
- Navegación lateral unificada con trazos funcionales de 1.65 px.
- Burbujas de conversación más amplias y legibles.
- Los nombres internos de streaming ahora usan Kohana, conservando aliases temporales de Nexo.
- El catálogo de voz acepta Kohana, Oye Kohana, Hey Kohana y aliases heredados de Nexo.

### Corregido

- Prueba de comandos de audio que todavía esperaba únicamente el alias Nexo.
- Contraste insuficiente en iconos, bordes y textos secundarios.

## 0.9.2-beta — Kohana Brand Foundation

### Agregado

- Identidad pública centralizada mediante `ProductIdentity`.
- Nombre, lema y mensajes visibles de Kohana en Hub, Capsule, Peek, onboarding y bandeja.
- Diseño Sakura Fluent con nueva paleta grafito y acento rosa sakura.
- Logo floral vectorial y recursos PNG/ICO para aplicación e instalador.
- Ejecutable y artefactos públicos `Kohana.exe` y `Kohana-*-portable.zip`.
- Wake words `Kohana`, `Oye Kohana` y `Hey Kohana`.
- Compatibilidad temporal con las frases anteriores de Nexo.
- Migración conservadora de `%LocalAppData%\Nexo` a `%LocalAppData%\Kohana`.
- Scripts, instalador, CI, release workflow y documentación actualizados.

### Cambiado

- Esquema de preferencias actualizado a 14.
- El color predeterminado migra del morado anterior al acento sakura.
- Los valores antiguos de wake word migran a `Oye Kohana`.
- El inicio con Windows registra Kohana y limpia la entrada heredada al activarse.
- La instancia única usa identificadores de Kohana.
- Buscar actualizaciones conserva temporalmente `EXOTARA/Nexo` como repositorio estable.

### Compatibilidad

- Los namespaces y la solución siguen llamándose `Nexo.*` y `Nexo.slnx` durante esta etapa.
- La carpeta anterior de datos no se elimina ni se sobrescribe.
- El Resource Governor ignora tanto `Kohana.exe` como `Nexo.exe` en pantalla completa.

## Mejoras integradas antes del cambio de marca

### Voice Reliability v1

- Variantes frecuentes para la frase de activación.
- Traspaso Vosk → Whisper con audio previo y ventana posterior separada.
- Orden de hasta 20 segundos y final después de 1.5 segundos de silencio.
- Protección para que la cola de la frase de activación no corte la consulta.
- Micrófono manual con pulsar para iniciar y pulsar para terminar.
- Métricas privadas en `voice-capture.log`, sin audio ni transcripciones.

### Resource Governor v1 y Silent Voice Look

- Estados Normal, Busy y Game basados en pantalla completa y CPU/GPU/RAM.
- Modo Juego protege wake word, Vision, IA y cápsulas transitorias.
- Exclusión de SnippingTool y ScreenClippingHost para evitar falsos positivos.
- Look Mode con contexto visual temporal y silencioso.

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

