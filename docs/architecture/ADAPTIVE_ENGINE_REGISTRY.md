# Adaptive Engine Registry y modos de rendimiento

> Documento de arquitectura de la Fase 2.2. Describe el registro de motores reales, el modelo de
> dominio, la política de recomendación y sus límites deliberados. Esta versión **no** selecciona
> ni cambia ningún motor automáticamente — es un sistema de transparencia, no de automatización.

## Objetivo y límite deliberado

Kohana ya sabe, desde la Fase 2.1, qué tan capaz es el hardware del equipo
(`HardwareCapabilityProfile`). Este sprint responde una pregunta distinta: dado ese hardware y un
modo de rendimiento elegido por el usuario, ¿qué le convendría a Kohana usar para reconocimiento de
voz, palabra de activación, síntesis de voz y modelos de lenguaje — y qué usa **realmente** hoy?

La respuesta se queda deliberadamente en el nivel de la recomendación. `AdaptiveEnginePolicy` nunca
reemplaza un motor, nunca descarga un modelo, nunca reinicia un servicio y nunca cambia el modelo
de Ollama. Cambiar el modo de rendimiento solo persiste la preferencia y recalcula qué se
recomendaría — la aplicación real de esas recomendaciones es una decisión explícitamente diferida a
un sprint futuro (ver "Relación con Voice Lab" más abajo).

## Estados: seis conceptos que no colapsan en uno

El requisito central de este sprint es no confundir seis ideas relacionadas pero independientes:

| Estado | Significado |
|---|---|
| **Registrado** | El motor aparece en el catálogo de `IAdaptiveEngineRegistry.GetDescriptors()`. |
| **Disponible** | El motor está instalado/listo para usarse ahora mismo (`EngineRuntimeState.IsAvailable`). |
| **Compatible** | El hardware detectado cumple al menos el requisito mínimo del motor (calculado por la política, no almacenado). |
| **Recomendado** | La política lo eligió como la mejor opción compatible para el modo activo. |
| **Configurado** | El usuario lo seleccionó en preferencias (`EngineRuntimeState.IsConfigured`). |
| **Activo** | Está en uso en este momento, cuando se puede saber (`EngineRuntimeState.IsActive`). |

Un motor puede estar en cualquier combinación de estos seis simultáneamente: disponible pero no
configurado, configurado pero temporalmente inactivo, recomendado pero no disponible (el usuario
aún no lo instaló), o activo sin ser el recomendado (el usuario eligió otra cosa a propósito). La
interfaz nunca muestra "Activo" cuando en realidad solo se trata de una recomendación — cada
`EngineRecommendation` expone `IsRecommendationOnly`, verdadero cuando el motor activo no coincide
con el recomendado (o cuando no hay ninguno activo), y `SystemView` renderiza la etiqueta "Solo
recomendación" exactamente en ese caso.

`EngineRuntimeState` modela "Disponible", "Configurado" y "Activo" como tres `bool?` independientes
en vez de un único enum de estado — un enum mutuamente excluyente no podría representar
"disponible y configurado pero no activo" como una combinación válida.

## Categorías y motores reales

Categorías mínimas (`EngineCategory`): `SpeechToText`, `WakeWord`, `TextToSpeech`,
`LocalLanguageModel`. No existe una categoría de Visión: la captura de pantalla existente
(`IScreenCaptureService`) no es un motor de visión diferenciable — es una utilidad de captura que
envía imágenes a la IA configurada, no un modelo local con requisitos propios.

Motores registrados por `WindowsAdaptiveEngineRegistry`, cada uno verificado leyendo el código real
que lo implementa (no supuesto):

- **Whisper** (`SpeechToText`) — modelo `GgmlType.Base` hardcodeado en `WhisperVoiceInputService`,
  nunca configurable. `Nexo.Windows.csproj` referencia `Whisper.net.Runtime` (CPU), no una variante
  con soporte de GPU — por eso el costo de GPU se declara `Low`, nunca se afirma aceleración.
- **Vosk** (`WakeWord`) — modelo pequeño en español fijo (`vosk-model-small-es-0.42`).
- **SAPI de Windows** (`TextToSpeech`) — `IVoiceOutputService` no expone qué voz seleccionó ni si
  está hablando ahora mismo; esos campos se dejan como `Unknown` en el estado en tiempo de
  ejecución en vez de inventarse.
- **OpenAI, Ollama, LM Studio, servidor compatible con OpenAI** (`LocalLanguageModel`) — los cuatro
  valores reales de `AiProviderKind` que `AiChatRouterService.Resolve` enruta hoy. El servidor
  "compatible" declara `IsLocal: null` porque su localidad depende de la URL que el usuario
  configure, algo que no se puede saber de forma estática.

**Motores explícitamente no registrados** porque no existen en el runtime actual, verificado con
`grep` sobre `src/` (cero referencias reales, más allá de menciones en documentación de visión de
producto): openWakeWord, Silero VAD, Kokoro, Piper, `Windows.Media.Ocr`, cualquier motor visual
distinto de la captura de pantalla. Pueden aparecer en documentación de producto futura, pero
`GetDescriptors()` nunca los expone como disponibles.

## Disponibilidad: de dónde viene cada dato

`WindowsAdaptiveEngineRegistry.CaptureRuntimeStates(preferences, ollamaRuntimeSnapshot)` construye
el estado real, siempre desde una fuente rastreable:

- Whisper/Vosk: `VoiceCoordinator.IsVoiceInputReady` / `IsWakeWordReady` / `IsVoiceInputListening` /
  `IsWakeWordListening` — propiedades que el coordinador ya exponía públicamente antes de este
  sprint; el registro las lee, no las modifica ni cambia su comportamiento.
- SAPI: `Configurado = true` siempre (es el único motor y no tiene alternancia por usuario);
  `Disponible = true` porque si `WindowsTextToSpeechService` no se hubiera podido construir, la
  aplicación no habría arrancado; `Activo = Unknown` porque no hay señal real que consultar.
- Proveedores de IA: `Configurado` = `preferences.AiProvider` coincide con ese proveedor. **Nunca**
  se marca "Activo" solo por estar configurado.
- Ollama, específicamente: `Activo` solo se marca verdadero cuando existe un
  `OllamaRuntimeSnapshot` real (de `ManagedOllamaSupervisor` / `OllamaRuntimeService`, que hacen una
  consulta HTTP real a `/api/tags`) **y** `snapshot.IsRunning` es verdadero **y** Ollama está
  configurado. Sin snapshot, `Activo` queda `Unknown` — nunca se asume que seleccionar Ollama
  signifique que ya está corriendo.

## Política de recomendación

`AdaptiveEnginePolicy.Evaluate(hardwareProfile, mode, descriptors, runtimeStates, evaluatedAt)` es
pura: la fecha de evaluación es un parámetro explícito (no `DateTimeOffset.Now` interno), así que la
misma entrada produce exactamente el mismo `AdaptiveEnginePlan`, byte a byte, siempre — verificado
por prueba.

**Compatibilidad** (por motor, en cada categoría): se compara el requisito mínimo del motor contra
un presupuesto de costo derivado del tier de hardware:

| Tier | Presupuesto máximo |
|---|---|
| Basic | Low |
| Standard | Moderate |
| Accelerated | High |
| HighPerformance | High |

Un motor es compatible si **ninguna** de sus cuatro dimensiones de costo (CPU/RAM/GPU/energía) en
el requisito **mínimo** excede el presupuesto. Un costo `Unknown` nunca cuenta como excedido — se
excluye de la comparación, igual que en la política de hardware de la Fase 2.1: dato desconocido no
equivale a incapacidad.

**Selección dentro de un modo** (entre los motores compatibles de una categoría):

- **Ahorro** — el de menor costo total (suma de las cuatro dimensiones del requisito recomendado,
  tratando `Unknown` como un valor neutro intermedio solo para efectos de puntuación, nunca para
  determinar compatibilidad).
- **Máximo** — el de mayor costo total entre los compatibles. Nunca elige un motor incompatible,
  sin importar qué tan "máximo" sea el modo.
- **Equilibrado** — el más cercano a un costo moderado (todas las dimensiones en `Moderate`).
- **Automático** — igual que Equilibrado si la confianza general del hardware es `Known`; igual que
  Ahorro (conservador) si es `Estimated` o `Unknown`. Así, un perfil de hardware con baja confianza
  nunca produce una recomendación optimista sin base.

**Salida por categoría** (`EngineRecommendation`): motor configurado, activo, recomendado,
alternativas compatibles (con sus razones), motores incompatibles (con sus razones), razones
positivas, advertencias (motor recomendado no disponible, requiere descarga, requiere reinicio) y
`IsRecommendationOnly`.

**Salida a nivel de plan** (`AdaptiveEnginePlan`): resumen, advertencias generales (cuando la
confianza del hardware no es `Known`), lista de cambios posibles hoy sin reiniciar (hoy, solo
cambiar de proveedor de IA califica — ya es posible desde Configuración sin reinicio) y lista de
cambios que requerirían descarga, reinicio o implementación futura (cambiar de motor de voz, por
ejemplo, requeriría reiniciar Kohana porque hoy no hay mecanismo de selección en tiempo de
ejecución para Whisper/Vosk/SAPI).

## Datos desconocidos

Igual que en la Fase 2.1: un costo de motor desconocido no bloquea su compatibilidad, una confianza
de hardware baja produce recomendaciones conservadoras (nunca "no sé, así que asumo lo mejor"), y
cada motor cuyo estado no se pudo determinar queda explícitamente `Unknown` en vez de mostrarse como
"no disponible" o "inactivo" por defecto.

## Persistencia

`ShellPreferences.HardwarePerformanceMode` (enum, default `Automatic`), agregado en el esquema v17
siguiendo el mismo patrón de migración versionada + `Enum.IsDefined` que ya usan `WakeWordSensitivity`
y `AiProvider`: un bloque `if (SchemaVersion < 17)` establece el valor por defecto una sola vez para
archivos antiguos, y un guard incondicional al final de `Normalize()` resetea cualquier valor fuera
de rango a `Automatic`. Serializado como el entero subyacente del enum, igual que el resto de
enums en `ShellPreferences` (sin `JsonStringEnumConverter`, consistente con el resto del archivo).
Cambiar el modo dispara guardado inmediato y un recálculo del plan; no se reescribe el archivo si
no hay cambios reales más allá de lo que `Normalize()` ya hace en cada guardado.

## Privacidad

- No se registra información personal ni identificadores de hardware.
- No se envía telemetría: la evaluación del plan es local y en memoria.
- `DiagnosticsWindow` nunca muestra claves de API, variables de entorno de credenciales, ni rutas
  privadas — solo nombre de motor, categoría, requisitos cualitativos y estado.
- El plan no incluye contenido de conversaciones ni datos de modelos descargados.

## Limitaciones conocidas

- El costo de GPU de los proveedores de IA locales (Ollama, LM Studio) es `Unknown` a propósito:
  Kohana no puede saber si la instalación externa del usuario usa GPU o no.
- SAPI no expone si está hablando en este momento; ese campo queda `Unknown` en vez de adivinarse.
- La distinción local/remota del servidor "compatible con OpenAI" depende de la URL configurada por
  el usuario y no se puede fijar en el descriptor estático.
- Los presupuestos de costo por tier son fijos para este sprint; no se ajustan por tipo de carga de
  trabajo (eso, si se necesita, es una decisión de un sprint futuro).

## Relación con Voice Lab

"Voice Lab" (o cualquier mecanismo futuro de selección/aplicación real de motores) no se inició en
este sprint. El `AdaptiveEnginePlan` que produce este código es la entrada que un futuro Voice Lab
consumiría para ofrecer cambios reales — pero ese sprint todavía no existe. `AdaptiveEnginePolicy`
no sabe nada sobre cómo aplicar un cambio, solo sobre qué recomendar.

## Qué no se automatiza todavía

- Ningún motor se descarga, instala o sustituye automáticamente.
- Ningún modelo de Ollama cambia por seleccionar un modo.
- Whisper, Vosk y SAPI no tienen mecanismo de selección en tiempo de ejecución (`AllowsRuntimeSelection: false`
  en sus descriptores) — cambiarlos, si algún día se permite, requeriría reiniciar Kohana.
- Cambiar el modo de rendimiento nunca dispara `EnsureRunningAsync`, ni construye ni reinicia
  ningún servicio de voz o IA — solo recalcula y persiste.
