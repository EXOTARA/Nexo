# Limitaciones conocidas — Kohana

> Documento honesto. Si algo no cumple los criterios de estabilidad, aparece aquí — no se oculta.
> Formato: qué es · por qué · cómo está aislado · qué falta para declararlo estable.

## Bloqueantes de RC (deben resolverse)

### L1 — Baseline parcialmente medido ⚠️ (reducido el 2026-07-23)
**Qué:** Build, pruebas y tiempos **ya están medidos** (356 pruebas, 0 fallidas, 0 warnings, build
Release en frío 2.52 s — ver `IMPLEMENTATION_LOG.md`). **Siguen sin medir** el tamaño del portable,
el tamaño del instalador y el SHA-256, porque requieren `dotnet publish` y compilar el instalador.
**Aislamiento:** Presupuestos de latencia siguen marcados `PENDIENTE DE CALIBRAR`: el baseline de
build/test **no** mide latencia de voz, wake word ni TTS, que exigen micrófono y escenarios reales.
**Para estable:** medir artefactos en Fase 10 y calibrar latencias en Fase 3 (Voice Lab).

### L2 — `MainWindow.xaml.cs` como God Object
**Qué:** 3,532 líneas, 119 métodos, 49 campos `readonly` (31 instanciados con `new` en la
declaración). Sin DI. *(Cifras medidas el 2026-07-23; la auditoría estática original decía 4,007/224.)*
**Por qué:** Crecimiento incremental sin composition root.
**Aislamiento:** Ninguno hoy — es el bloqueador raíz.
**Para estable:** completar los 7 pasos de la Fase 1 (ADR 0001).

### L3 — Accesibilidad ausente
**Qué:** 0 `AutomationProperties` en los 22 archivos XAML. Sin soporte de lector de pantalla.
**Aislamiento:** Ninguno.
**Para estable:** Fase 9 — es criterio de salida explícito.

## No bloqueantes (documentadas, aisladas)

### L4 — Wake word sobre ASR de propósito general
**Qué:** Vosk con gramática de excepciones fonéticas. Techo estructural de precisión.
**Aislamiento:** Declarado `FallbackHeredado` (ADR 0004). `IWakeWordService` ya desacoplada.
**Para estable:** un candidato debe **ganar medido** en Voice Lab. Si ninguno gana, Vosk sigue y se
documenta como limitación aceptada.

### L5 — TTS sin naturalidad ni barge-in
**Qué:** SAPI5 (`System.Speech`). Sin streaming, sin interrupción, sin AEC.
**Impacto:** Afecta directamente la experiencia D2 del día 1.
**Aislamiento:** Declarado `FallbackHeredado`.
**Para estable:** Fases 3–4. La experiencia D2 exige voz interrumpible, así que **esto sí bloquea D2**
aunque no bloquee el build.

### L6 — Sin OCR ni UI Automation
**Qué:** Vision depende hoy de enviar la imagen completa al modelo.
**Aislamiento:** Bloqueado por TFM.
**Para estable:** Fase 7 (ADR 0003).

### L7 — Sin recuperación de settings semánticamente corruptos
**Qué:** La escritura es atómica, pero no hay `.bak`. Un JSON válido con contenido inválido no se
puede revertir.
**Aislamiento:** Escenario 4 de `TEST_MATRIX` lo cubrirá.
**Para estable:** añadir copia previa (Fase 1).

### L8 — Sin firma Authenticode
**Qué:** No hay certificado.
**Aislamiento:** Sin actualización silenciosa. El usuario ve versión, notas y hash, y confirma.
**Para estable:** no bloquea RC. La automática se habilita solo con firma + rollback probado.

## Fuera de alcance de 1.0 (decidido, no es limitación)

Puentes de mensajería · marketplace comunitario · automatización de navegador (experimental) ·
emparejamiento de dispositivos · clonación de voz · Windows 10.
