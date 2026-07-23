# Auditoría del estado actual — Kohana 0.9.5-beta

> Fecha original: 2026-07-22 · Base auditada: `Kohana-0_9_5-foundation-base.zip`
> **Revisión medida: 2026-07-23** sobre el repositorio real `C:\Dev\Nexo`, rama `release/kohana-1.0-rc`,
> commit `144bb13`. Las cifras estáticas de la auditoría original se corrigieron contra el código real
> (ver §0). Método: inspección estática (222 `.cs`, 22 `.xaml`) **más** restore/build/test ejecutados
> en Windows.

## 0. Verificación de versión y correcciones de la auditoría original

**Versión real del código: `0.9.5-beta`.** Verificado en `Directory.Build.props`
(`VersionPrefix=0.9.5`, `VersionSuffix=beta`, `AssemblyVersion=0.9.5.0`). La cadena `0.9.6-beta`
aparece **una sola vez** en todo el repositorio, en `docs/ROADMAP.md:54`, como encabezado de un
sprint **planificado y no implementado** ("Setup y onboarding definitivo"). No existe código,
`CHANGELOG` publicado ni etiqueta de 0.9.6. **La documentación no se rebaja y el código no se
sustituye:** ambos ya están en 0.9.5-beta.

Correcciones a cifras de la auditoría estática original (medidas el 2026-07-23):

| Dato | Documentado (0.9.5 estático) | **Medido en el repositorio real** |
|---|---|---|
| `MainWindow.xaml.cs` — líneas | 4,007 | **3,532** |
| `MainWindow.xaml.cs` — métodos | 224 | **119** declaraciones de método |
| `MainWindow.xaml.cs` — campos `readonly` | «~28 servicios» | **49 campos `readonly`**, de ellos **31** instanciados con `new` en la declaración y ~11 más en el constructor |
| `WindowsVoiceInputService.cs` — líneas | 1,070 | **932** |
| `CommandPaletteWindow.xaml.cs` — líneas | 903 | **801** |
| Atributos `[Fact]`/`[Theory]` | 196 en 48 archivos | **199 en 48 archivos** |
| Pruebas ejecutadas | 196 | **356** (353 `Nexo.Core.Tests` + 3 `Nexo.Windows.Tests`) |

Confirmado sin cambios (medido, coincide con lo documentado):
`Nexo.Core` con **0** `PackageReference` · **0** `AutomationProperties` en los 22 XAML ·
**0** coincidencias de `HardwareCapability`/`CapabilityProfile` · TFM `net10.0-windows` en
`Nexo.Windows`/`Nexo.App` y `net10.0` puro en `Nexo.Core` · 222 `.cs` · 22 `.xaml`.

La discrepancia de tamaños se explica porque la auditoría original se hizo sobre el ZIP
`Kohana-0_9_5-foundation-base.zip` y el repositorio contiene los commits posteriores `a161a0a`
(0.9.3 Sakura shell), `c1b45e7` (0.9.4) y `1be4d4a` (0.9.5 voice runtime) ya consolidados.
**La conclusión cualitativa no cambia:** `MainWindow.xaml.cs` sigue siendo el God Object y el
bloqueador raíz.

## 0.1 Baseline medido

Ver el bloque completo con tiempos, SDK y resultados en `IMPLEMENTATION_LOG.md` §Baseline.

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
src/Nexo.App/MainWindow.xaml.cs   3,532 líneas · 119 métodos · 49 campos readonly
                                  (31 instanciados con `new` en la declaración)
```
> Cifras medidas el 2026-07-23 sobre el repositorio real. Ver §0 para el contraste con la auditoría
> estática original.

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
3. **La capa App es intesteable.** 353 tests cubren `Nexo.Core` y 3 cubren `Nexo.Windows`;
   `Nexo.App` y `MainWindow` tienen **0**.
4. **Deuda compuesta.** Cada fase futura que toque voz, IA o Vision engorda este archivo.

Este es el bloqueador raíz. Todo lo demás depende de resolverlo primero.

## 3. Estado por subsistema

| Subsistema | Estado | Evidencia / nota |
|---|---|---|
| `Nexo.Core` | **Sólido** | Cero `PackageReference` (verificado). Lógica pura, 353 tests. **Invariante a proteger.** |
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

- `WindowsVoiceInputService.cs` (932 líneas) mezcla captura, umbrales, segmentación y transcripción.
  Se separará en Fase 3 mediante las interfaces nuevas.
- `CommandPaletteWindow.xaml.cs` (801 líneas) — candidato a extracción posterior.
- `JsonSettingsStore` escribe atómicamente pero no conserva copia previa: un JSON válido pero
  semánticamente corrupto no es recuperable.
- Namespaces `Nexo.*` internos: **se conservan deliberadamente**. Renombrarlos añade riesgo sin
  beneficio para el usuario. Ver `MIGRATION_PLAN.md`.

## 6. Lo que NO debe tocarse en esta actualización

`ResourceGovernorPolicy` · `VisionPrivacyPolicy` · `NaturalCommandParser` · `SpanishCommandLexicon` ·
los 356 tests existentes · rutas de datos · namespaces internos · instalador (hasta Fase 10).

Todos funcionan y están alineados con la visión. Reescribirlos por preferencia está prohibido.
