# ADR 0001 — Extraer Kohana Runtime de MainWindow

**Estado:** Aceptado · **Fecha:** 2026-07-22 · **Fase:** 1

## Contexto

`src/Nexo.App/MainWindow.xaml.cs` tiene **3,532 líneas y 119 métodos**, con **49 campos `readonly`**
de los cuales **31** se instancian con `new` en la propia declaración —incluidos los seis servicios
de interfaz (`new WhisperVoiceInputService()`, `new VoskWakeWordService()`,
`new WindowsTextToSpeechService()`, `new AiChatRouterService()`, `new WindowsAudioMixerService()`,
`new WindowsScreenCaptureService()`)— y ~11 más que se construyen en el constructor.
No existe contenedor de inyección de dependencias.
*(Cifras medidas el 2026-07-23 sobre el repositorio real; la auditoría estática original indicaba
4,007 líneas y 224 métodos sobre el ZIP de base. La conclusión no cambia.)*

Kohana Runtime, tal como lo describe la especificación de producto, **ya existe** — pero está
fusionado dentro de un `Window` de WPF.

## Problema

1. El **Adaptive Engine Registry es imposible**: no se puede elegir motor según hardware si el motor
   queda fijado al construir la ventana.
2. La capa App es **intesteable**: 0 pruebas sobre `MainWindow`.
3. Cada fase futura que toque voz, IA o Vision **engorda** el archivo.

## Decisión

Extraer la coordinación a `KohanaRuntime` (en `Nexo.Windows`) e introducir
`Microsoft.Extensions.DependencyInjection` (MIT) como composition root en `App.xaml.cs`.

**Prohibido hacerlo como reescritura masiva.** Se ejecuta en 7 pasos, cada uno compilable, con
pruebas y commit propio:

1. Pruebas de caracterización del comportamiento actual
2. Composition root + DI **sin cambiar comportamiento**
3. Extracción del coordinador de voz
4. Extracción del coordinador de navegación
5. Extracción de tareas, enfoque y rutinas
6. Extracción de IA y Vision
7. `MainWindow` queda como vista y eventos mínimos

## Consecuencias

**Positivas:** desbloquea Fases 2–8 · hace testeable la capa App · permite sustituir motores en
caliente · reduce deuda compuesta.

**Negativas:** es el cambio de mayor riesgo del plan · toca el archivo más grande · requiere
disciplina de commits pequeños.

**Mitigación:** caracterización antes de mover · ningún paso cambia conducta observable · cada paso
es revertible de forma independiente.

## Alternativas descartadas

- *No hacer nada*: bloquea toda la arquitectura adaptativa. Descartada.
- *Reescribir MainWindow de cero*: alto riesgo, sin red de seguridad, contradice "no reescrituras
  masivas". Descartada explícitamente por el propietario del producto.

## Addendum — paso 3 completado (2026-07-24, fase 1.3B3)

El paso 3 (*Extracción del coordinador de voz*) queda cerrado con un modelo de propiedad y
sincronización definitivo, que completa —sin contradecir— la decisión original:

- **Sincronización:** `VoiceCoordinator` posee los dos únicos `SemaphoreSlim` del subsistema
  (entrada de voz y wake word), privados, y los expone mediante *leases* opacos
  (`IVoiceInputScope` / `IWakeWordScope : IAsyncDisposable`). Un solo dominio de exclusión por
  servicio; orden de adquisición voz → wake word.
- **Propiedad y ciclo de vida:** `KohanaCompositionRoot` es el dueño y único liberador de Whisper,
  TTS y Vosk (los construye y los libera en su `Dispose`, en App.OnExit). `VoiceCoordinator` no
  libera esos servicios. `MainWindow` ya no los recibe ni los libera y accede al subsistema solo a
  través del coordinador.

Sigue vigente el principio del ADR: cada paso compilable, con pruebas y sin cambiar conducta
observable (verificado con smoke test manual en 1.3B2 y pendiente de repetir en 1.3B3).

## Addendum — cierre de la Fase 1.3 (2026-07-24, hotfix 1.3B3.1 + consolidación 1.3C)

Estado **final** de la extracción del runtime, tras corregir el cierre y consolidar la arquitectura:

- **Salida completa (1.3B3.1):** la parada del runtime de IA administrado se hace de forma asíncrona
  antes de `Application.Shutdown` (`MainWindow.RequestExitAsync`), no con sync-sobre-async en
  `App.OnExit`. `App.OnExit` no bloquea y alcanza `SingleInstanceCoordinator.Dispose()`, que libera
  el mutex; una nueva instancia puede convertirse en primaria. Sin `Environment.Exit`/`Process.Kill`/
  `.Wait()`/`.Result`/`GetResult()`.
- **Composición única (1.3C):** `KohanaCompositionRoot` es la única raíz de composición y el único
  dueño/liberador del subsistema de voz. `MainWindow` recibe **todos** sus servicios por constructor
  como dependencias **obligatorias** (lanza si faltan) y no construye ningún motor; no usa
  `IServiceProvider` ni Service Locator. `Nexo.Core` permanece sin dependencias de infraestructura.
- **Frontera de acceso:** la capa de aplicación accede a la voz **solo** a través de
  `VoiceCoordinator`; los tres motores y el contenedor son detalle del composition root (expuestos
  únicamente para verificación de composición en pruebas).

El paso 3 queda **cerrado**. Los pasos 4–7 del plan (navegación, tareas/enfoque/rutinas, IA/Vision,
vista mínima) siguen pendientes y no forman parte de esta consolidación.
