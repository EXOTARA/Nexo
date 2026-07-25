# Focus Continuity & Daily Flow Polish — Diseño D3.1

> Continúa D3 sobre `design/daily-flow-v1`. No repite el descubrimiento arquitectónico de D3
> (ver `SAKURA_DAILY_FLOW_V1.md`) ni el de D2 (`SAKURA_COMMAND_CENTER_V2.md`) — documenta solo lo
> nuevo de este sprint.

## 1. Defecto y causa raíz

Ver `artifacts\design-d3.1\reproduction-before.txt` para la reproducción completa con evidencia.
Resumen: `_focusTickTimer` (1 s, ya existente) mantiene el dominio al día, pero `NavigateTo` solo
forzaba un refresco inmediato para el destino "Home"; Enfoque (y la reactivación desde bandeja)
esperaban hasta 999 ms al siguiente tick. Corregido llamando a `CheckFocusTimer()` —el mismo punto
que ya usa `HandleSystemResume()`— desde `NavigateTo` (sin condición por destino) y desde
`ShowFromBackground()`.

## 2. Arquitectura de Focus Continuity

Para no seguir haciendo crecer `MainWindow.xaml.cs` (~4.900 líneas), la lógica nueva se divide en:

- **`FocusDisplayState`** (Nexo.App/DailyFlow) — registro inmutable: texto del reloj, estado
  (activa/pausada/ninguna), etiqueta/tarea, progreso, y qué acciones tienen sentido ahora
  (pausar/continuar/finalizar/iniciar). Nada de WPF.
- **`FocusDisplayStateBuilder`** — función pura `FocusManager × DateTimeOffset × resolver de título
  → FocusDisplayState`. Un único lugar que decide "qué significa el estado de enfoque ahora mismo",
  usado por el mini temporizador, la tarjeta de Inicio y (indirectamente) FocusView.
- **`FocusMiniTimer`** (UserControl) — presenta un `FocusDisplayState` y expone eventos
  (`PauseRequested`, `ResumeRequested`, `FinishRequested`, `OpenFocusRequested`); no conoce
  `FocusManager` ni `MainWindow`.
- **`FocusContinuityCoordinator`** — conecta `FocusManager`, el tick ya existente, la navegación,
  Inicio, FocusView y el mini temporizador. `MainWindow` lo construye una vez y le delega el
  refresco de estos tres puntos, en vez de hacerlo línea por línea en cada manejador.

## 3. Mini temporizador global

Vive en el encabezado del shell (`MainWindow.xaml`), junto al botón del Command Center — visible
en cualquier sección salvo Enfoque (evitar redundancia visual con la vista completa). Se oculta por
completo sin sesión. Reutiliza `WorkspaceStatusChipStyle`/`SecondaryButtonStyle` de D2 donde encaja;
no introduce blur ni animación continua.

## 4. Historial y resumen

`FocusHistorySummaryBuilder` (Nexo.Core.Focus o Nexo.App/DailyFlow, según si toca dominio o
presentación puramente de UI — ver código) consulta `FocusHistoryEntry` ya existente: sesiones
recientes (excluye descansos de la lista de "sesiones" contables, igual que ya hace
`GetSnapshot().FocusMinutesToday`), duración real, tarea asociada cuando exista. Sin rachas, sin
puntuación, sin comparaciones sin datos.

## 5. Command Center y finalización

Presets 15/25/45 min con disponibilidad real (solo sin sesión activa); `focus.pause`/`resume`/
`finish` ya existían desde D3 y se mantienen. El aviso de finalización se unifica para cubrir tanto
la finalización natural como `Finish`, con una sola aparición por sesión.

## 6. No objetivos

Sin Always On Top, sin widget de escritorio, sin segundo timer, sin cambios a
`SingleInstanceCoordinator`, sin tocar Asistente/Audio/Captura/Sistema/voz.
