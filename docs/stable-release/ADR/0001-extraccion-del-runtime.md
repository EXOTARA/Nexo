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
