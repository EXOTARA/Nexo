# Changelog

Todos los cambios importantes de Nexo se documentarán aquí.

## [Sin publicar]

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

### Cambiado

- La cápsula ahora aparece en la parte superior central.
- La interfaz principal ya no necesita desplazamiento vertical.
- CPU, RAM y GPU sustituyen al almacenamiento en la vista rápida.
- El reconocimiento clásico de Windows se reemplaza por transcripción local con Whisper.
- Las respuestas abiertas son más breves y dejan de convertir consultas no relacionadas en diagnósticos del sistema.
- El silencio final de la escucha automática se reduce para entregar antes la orden.
- La frase de activación puede decirse junto con la orden completa.

### Pendiente

- Notificaciones nativas de Windows para recordatorios aunque la barra esté oculta.
- Temporizadores, sesiones de enfoque y rutinas.
- OCR y copia de texto desde capturas.
- Difuminado manual de información sensible.
- Calibración guiada del micrófono.
- Diccionario personal de correcciones.
- Reducir todavía más el consumo del detector permanente.
- Respaldo semántico para convertir instrucciones ambiguas en acciones locales validadas.
