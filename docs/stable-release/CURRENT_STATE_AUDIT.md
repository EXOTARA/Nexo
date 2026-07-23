# Auditoría del estado actual — Kohana 0.9.5-beta

> Fecha: 2026-07-22 · Base auditada: `Kohana-0_9_5-foundation-base.zip`
> Método: inspección estática del árbol completo (222 `.cs`, 22 `.xaml`, 16 `.md`).
> **No** incluye compilación ni ejecución: la auditoría se hizo en un entorno Linux sin SDK de .NET.
> Los datos de build, tests y tiempos deben completarse desde Windows (ver `IMPLEMENTATION_LOG.md`).

## 1. Delta 0.9.4 → 0.9.5

Cambio quirúrgico y bien acotado: **6 archivos nuevos, 31 modificados, 0 eliminados**.

Nuevos: `WakeWordAliasPolicy.cs`, `WakeWordMatchKind.cs`, `WakeWordMatchResult.cs`,
`WakeWordRecognitionObservedEventArgs.cs`, `WakeWordAliasPolicyTests.cs`,
`docs/VOICE_RELIABILITY_V3_RUNTIME_V1.md`.

Contenido del sprint: se eliminó `Nexo` de la gramática Kohana (compatibilidad heredada solo si se
selecciona explícitamente), se añadió pronunciación mexicana (`cojana`, `kojana`, `oye cojana`,
`ey cojana`), aliases personales (máx. 8, solo texto normalizado, nunca audio), log en
`Logs\wake-word-recognition.log`, y un panel de estado de Runtime en la vista Sistema.

**Valoración:** dirección correcta y disciplina de cambio ejemplar. Pero la estrategia de fondo no
cambió: sigue siendo *acumular excepciones fonéticas sobre un ASR de vocabulario abierto*. Es
exactamente lo que `PRODUCT_VISION` §E prohíbe asumir como solución definitiva.

## 2. Hallazgo crítico — God Object

```
src/Nexo.App/MainWindow.xaml.cs   4,007 líneas · 224 métodos · ~28 servicios instanciados
```

Todos los servicios concretos se fijan como inicializadores de campo:

```csharp
private readonly IVoiceInputService  _voiceInputService  = new WhisperVoiceInputService();
private readonly IVoiceOutputService _voiceOutputService = new WindowsTextToSpeechService();
private readonly IWakeWordService    _wakeWordService    = new VoskWakeWordService();
private readonly IAiChatService      _aiChatService      = new AiChatRouterService();
```

Consecuencias objetivas (no estéticas):

1. **Bloquea el Adaptive Engine Registry.** No se puede seleccionar motor según hardware si el motor
   queda fijado al construir el `Window`. Sin resolver esto, la arquitectura adaptativa es imposible.
2. **Kohana Runtime ya existe pero está fusionado con la vista.** No hay que crearlo: hay que
   *extraerlo*.
3. **La capa App es intesteable.** 196 tests cubren `Nexo.Core`; `MainWindow` tiene 0.
4. **Deuda compuesta.** Cada fase futura que toque voz, IA o Vision engorda este archivo.

Este es el bloqueador raíz. Todo lo demás depende de resolverlo primero.

## 3. Estado por subsistema

| Subsistema | Estado | Evidencia / nota |
|---|---|---|
| `Nexo.Core` | **Sólido** | Cero `PackageReference`. Lógica pura, 196 tests. **Invariante a proteger.** |
| Migración de settings | **Bueno** | Esquema v16 incremental; escritura atómica (`.tmp` + `File.Move`). Falta `.bak` de recuperación. |
| `ResourceGovernorPolicy` | **Estable** | Normal/Busy/Game; GPU 88 / CPU 92 / RAM 92. Es el eje **carga**. |
| Perfil de hardware | **AUSENTE** | 0 coincidencias de `HardwareCapability`/`CapabilityProfile`. El eje **capacidad** no existe. |
| Wake word | **Fallback heredado** | Vosk 0.3.38 + gramática. `IWakeWordService` desacoplada → sustituible sin romper arquitectura. |
| VAD / fin de turno | **AUSENTE como componente** | Umbrales manuales embebidos en `WindowsVoiceInputService.cs` (1,070 líneas). |
| TTS | **Fallback de emergencia** | `System.Speech` (SAPI5). Sin streaming, sin barge-in, sin AEC. |
| STT | **Aceptable** | Whisper.net 1.9.1, local. |
| Vision | **Bueno** | Captura en memoria, `VisionPrivacyPolicy` filtra por proceso y por título. |
| OCR local | **AUSENTE** | Bloqueado por TFM (ver §4). |
| UI Automation | **AUSENTE** | 0 referencias a `AutomationElement`. Acciones sobre controles reales hoy imposibles. |
| Permisos | **Parcial** | `AutomationPermissionPolicy` con `Safe/Reversible/Sensitive/Blocked`. Buena base, pero solo aplica a rutinas — no hay sistema general de planes. |
| Memoria transparente | **AUSENTE** | Las 22 coincidencias de "Memory" son métricas de RAM. |
| Skills / Subagentes | **AUSENTES** | 0 coincidencias. |
| Actualizador | **Básico** | `IUpdateService` + `ReleaseVersion`. Sin delta, sin rollback. |
| CI | **Bueno** | `windows-latest`: restore → build → test → publish → verify. Ya puede producir métricas reales. |
| Accesibilidad | **AUSENTE** | **0** `AutomationProperties` en los 22 XAML. Es criterio de salida. |
| Marca "Nexo" visible | **Sin fugas** | Solo claves de estilo internas (`NexoSliderStyle`). Riesgo ≈ 0. No tocar. |

## 4. Bloqueador técnico de TFM

```
TFM actual:   net10.0-windows
TFM objetivo: net10.0-windows10.0.26100.0
```

El TFM actual **no da acceso a WinRT**. Bloquea:
- `Windows.Media.Ocr` — paso 4 del pipeline de Vision.
- APIs modernas de audio necesarias para evaluar AEC/Voice Isolation.

La decisión B (Windows 11 24H2+) resuelve esto y permite fijar build 26100 sin concesiones.
Ver `ADR/0003`.

## 5. Deuda menor registrada (no bloquea RC)

- `WindowsVoiceInputService.cs` (1,070 líneas) mezcla captura, umbrales, segmentación y transcripción.
  Se separará en Fase 3 mediante las interfaces nuevas.
- `CommandPaletteWindow.xaml.cs` (903 líneas) — candidato a extracción posterior.
- `JsonSettingsStore` escribe atómicamente pero no conserva copia previa: un JSON válido pero
  semánticamente corrupto no es recuperable.
- Namespaces `Nexo.*` internos: **se conservan deliberadamente**. Renombrarlos añade riesgo sin
  beneficio para el usuario. Ver `MIGRATION_PLAN.md`.

## 6. Lo que NO debe tocarse en esta actualización

`ResourceGovernorPolicy` · `VisionPrivacyPolicy` · `NaturalCommandParser` · `SpanishCommandLexicon` ·
los 196 tests existentes · rutas de datos · namespaces internos · instalador (hasta Fase 10).

Todos funcionan y están alineados con la visión. Reescribirlos por preferencia está prohibido.
