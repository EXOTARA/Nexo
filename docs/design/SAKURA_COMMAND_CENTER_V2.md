# Sakura Command Center — Diseño D2

> Segundo sprint de diseño. Parte de `release/kohana-1.0-rc` con Diseño D1 + D1.1 ya integrados y
> aprobados por el usuario. Convierte el Sakura Shell (marco visual) en un centro de control con
> comandos reales, componentes reutilizables y preferencias visuales persistentes.

## 1. Estado actual (medido, no supuesto)

| Área | Estado real encontrado |
|---|---|
| Shell | `MainWindow.xaml` + `MainWindow.xaml.cs` (~4.100 líneas). Navegación por nueve botones con `Tag`/`Click`, `ModuleHost` como `ContentControl`, encabezado central (`WorkspaceTitleText`/`WorkspaceSubtitleText`). |
| Navegación | `ShellNavigationPolicy.KnownDestinations` (Core) define las nueve secciones. `UpdateNavigationState`/`ApplyNavigationItemState` aplican el estado activo en code-behind. |
| Vistas | Nueve `UserControl` en `src/Nexo.App/Views/`. Sin ViewModels: code-behind directo, con servicios inyectados por constructor (`TasksView(TaskManager)`, `AudioView(IAudioMixerService)`, …). |
| Design System | Ocho diccionarios en `src/Nexo.App/Themes/` fusionados por `ThemeResources.xaml`. Tokens de color, espaciado, radios, tipografía, iconos y estilos de control ya centralizados desde la Fundación 0.1. |
| Iconos | `Themes/Brand.xaml`: geometrías **de línea** (`IconKohanaHome`, `IconKohanaSystem`, …), pensadas para trazarse, no para rellenarse. |
| Persistencia | **Ya existe y es sólida**: `JsonSettingsStore` (Nexo.Windows) + `ShellPreferences` (Nexo.Core) con `SchemaVersion`, escalera de migración en `Normalize()`, reemplazo atómico (`.tmp` + `File.Move(overwrite)`), respaldo de archivo corrupto (`CorruptFileBackup`) y ruta inyectable para pruebas. |
| Paleta de comandos | **Ya existe** `CommandPaletteWindow` (~900 líneas) en **Ctrl + Espacio**, orientada a *prompts de lenguaje natural* hacia el asistente. Sus sugerencias están **incrustadas** en la propia ventana (`BuiltInSuggestions`), no en un registro. |
| Atajos globales | `Alt+A` (mostrar/ocultar), `Alt+Shift+A` (peek), `Ctrl+Espacio` (paleta), `Ctrl+Shift+Espacio` (mirar). Registrados por `RegisterHotKey` en `MainWindow`. |
| Servicios | `KohanaCompositionRoot` (Nexo.Windows/Composition) compone manualmente: IA, audio, captura, voz, hardware, motor adaptativo. Sin contenedor DI ni Service Locator. |
| Pruebas | 834 en tres proyectos: `Nexo.Core.Tests` (629), `Nexo.Windows.Tests` (164, basadas en texto), `Nexo.App.Tests` (41, WPF real en hilo STA vía `StaWpfFixture`). |

## 2. Inventario de servicios reales reutilizables

Todos existen y funcionan; D2 **consume** estos servicios, no los reimplementa:

- `TaskManager` (Nexo.Core/Tasks) sobre `ITaskStore`.
- `FocusManager` (Nexo.Core/Focus) sobre `IFocusStore`.
- `RoutineManager` (Nexo.Core/Automation) sobre `IRoutineStore`.
- `IAudioMixerService` (Nexo.Core/Audio) — 11 operaciones, snapshot + control por sesión.
- `IScreenCaptureService` (captura).
- `VoiceCoordinator` (Whisper, wake word, TTS) — **no se toca**.
- `AdaptiveEnginePolicy` + Engine Registry (motores registrados, recomendado, configurado) — **no se toca**.
- `HardwareCapabilityService` (perfil de capacidad del equipo).
- `SingleInstanceCoordinator` (instancia única, `ActivationRequested` → `ShowFromBackground`).
- `JsonSettingsStore` + `ShellPreferences` (preferencias persistentes).

## 3. Problemas encontrados

1. **Defecto D2.0 — iconos seleccionados se convierten en bloques sólidos.** Descrito y corregido
   abajo (§4). Es el defecto reportado por el usuario tras aprobar D1.1.
2. **No existe un registro de comandos.** Las acciones de la paleta actual están incrustadas como
   un arreglo estático dentro de `CommandPaletteWindow`, mezcladas con la lógica de UI. No hay
   identificador estable, ni categoría, ni disponibilidad, ni ejecución asíncrona con manejo de
   errores. No es reutilizable ni comprobable sin WPF.
3. **No hay forma de restaurar solo las preferencias visuales.** `ShellPreferences` mezcla
   preferencias visuales (acento, opacidad, animaciones, barra lateral) con configuración
   funcional (voz, IA, motores, integración con Windows). Restaurar valores por defecto hoy
   implicaría borrar también lo funcional, que es justo lo que no debe pasar.
4. **`MainWindow.xaml.cs` concentra demasiada responsabilidad** (~4.100 líneas). D2 no lo reescribe
   —sería una refactorización masiva fuera de alcance— pero **no añade** lógica nueva de comandos
   ahí: el registro vive en Nexo.Core.

## 4. Corrección propuesta para los iconos (D2.0) — implementada

**Causa raíz, medida con render real** (`RenderTargetBitmap`, cobertura de tinta sobre el recuadro
delimitador), no por inspección visual:

| Icono | Normal (trazado) | Seleccionado (relleno) — defecto |
|---|---|---|
| `IconKohanaSystem` | 12.2 % | **95.6 %** (bloque sólido) |
| `IconKohanaTasks` | 10.7 % | **93.2 %** (bloque sólido) |
| `IconKohanaAssistant` | 8.3 % | **79.7 %** (mancha) |
| `IconKohanaHome` | 8.3 % | **53.0 %** |
| `IconKohanaSettings` | 9.4 % | **0.0 % — icono invisible** |

`SakuraNavigationIconFilledStyle` aplicaba `Fill` + `StrokeThickness="0"`. Las geometrías de
`Brand.xaml` son **trazos abiertos** (la casa de Inicio, el ecualizador de Sistema, los
deslizadores de Personalizar). Al rellenarlas ocurren dos cosas a la vez: los contornos cerrados se
vuelven manchas sólidas, y los trazos de detalle interiores —que no encierran área— desaparecen.
Eso produce exactamente lo reportado: "un bloque magenta con apenas una marca dentro".
`IconKohanaSettings`, que son solo segmentos, desaparecía por completo.

**Corrección (mínima y compartida, no nueve arreglos sueltos):** el estado seleccionado se expresa
con **grosor de trazo**, nunca con relleno.

- `SakuraNavigationIconFilledStyle` → renombrado a `SakuraNavigationIconSelectedStyle` (el nombre
  anterior habría quedado mintiendo).
- Grosores tokenizados en `Spacing.xaml`: `NavigationIconStrokeThickness` (1.65) y
  `NavigationIconStrokeThicknessSelected` (2.4).
- Un solo estilo compartido cubre las nueve secciones; `ApplyNavigationItemState` sigue siendo el
  único punto que decide el estado.

El estado activo **sigue sin depender solo del color**: superficie elevada + indicador vertical +
grosor de trazo + peso del texto. Se sustituye una señal no cromática (relleno) por otra
(grosor), no se elimina.

Referencias entre diccionarios: se usa `DynamicResource` para los tokens de `Spacing.xaml` desde
`Controls.xaml`, siguiendo el patrón ya existente. **No** se introduce ningún `StaticResource`
entre archivos distintos — esa fue exactamente la causa del crash de D1.1.

## 5. Arquitectura del Command Center

Objetivo: un registro de comandos **real, probado y sin WPF en el núcleo**, en vez de una lista
incrustada en una ventana.

```
Nexo.Core/Commands/CommandCenter/          (sin dependencias de UI, comprobable sin WPF)
  KohanaCommandCategory.cs      enum: Navegación, Enfoque, Tareas, Audio, Captura, Sistema, Shell
  KohanaCommandAvailability.cs  disponible / no disponible + motivo legible
  KohanaCommandDescriptor.cs    Id estable, Título, Descripción, PalabrasClave, Categoría,
                                IconKey, Shortcut, disponibilidad y ejecución async
  KohanaCommandRegistry.cs      alta de comandos, IDs únicos, consulta por Id
  CommandSearchEngine.cs        normalización, coincidencia por título/alias/palabras clave,
                                ranking determinista
  CommandExecutionResult.cs     éxito / fallo con mensaje, sin lanzar hacia la UI
```

La capa WPF (`Nexo.App`) solo:
1. construye los descriptores enlazando cada uno a un servicio real ya existente;
2. muestra resultados y traslada teclado;
3. informa errores de forma no modal.

**Atajo:** `Ctrl + K`. Se registra **junto a** los atajos existentes; `Alt+A`, `Alt+Shift+A`,
`Ctrl+Espacio` y `Ctrl+Shift+Espacio` se conservan intactos. La paleta de prompts en
`Ctrl+Espacio` (orientada al asistente) **no se elimina ni se fusiona**: son cosas distintas —una
envía lenguaje natural a la IA, la otra ejecuta acciones locales— y fusionarlas sería un cambio de
producto, no una mejora de diseño.

**Búsqueda:** normaliza mayúsculas y espacios sobrantes, busca en título, palabras clave y alias,
prioriza el título, y ordena de forma determinista. Sin reflexión por pulsación y sin construir
vistas al buscar: el registro se materializa una sola vez.

**Errores:** un comando que falla devuelve `CommandExecutionResult` con el motivo; nunca cierra
Kohana, nunca informa éxito falso, y el detalle (tipo, mensaje, inner, stack trace) se registra.
Los comandos no disponibles se muestran deshabilitados con una explicación, en vez de fallar al
ejecutarse.

## 6. Componentes compartidos de workspace

Estilos reutilizables añadidos a `Themes/Controls.xaml` (ningún diccionario nuevo, para no alterar
el orden de fusión ya cubierto por `DesignSystemResourceTests`):

encabezado de sección, subtítulo, tarjeta, tarjeta interactiva, métrica, chip de estado, estado
vacío, estado no disponible, estado de error, barra de herramientas, campo de búsqueda, divisor y
botón de icono.

No se introduce un framework MVVM: las vistas actuales son code-behind con servicios inyectados y
migrarlas sería una reescritura, no un sprint de diseño.

## 7. Persistencia

**No se crea un almacén nuevo.** `JsonSettingsStore` ya cumple todo lo exigido: JSON versionado,
valores por defecto, archivo inexistente, archivo corrupto (con respaldo), normalización de rangos,
escritura con reemplazo atómico, sin privilegios y sin secretos.

Lo que **sí falta** y añade D2: restaurar **solo** las preferencias visuales (acento, opacidad,
animaciones, estado de la barra lateral, posición y ancho) sin tocar tareas, rutinas, voz, motores,
historial ni ninguna otra configuración funcional. Se implementa en `ShellPreferences` como una
operación explícita y acotada, con pruebas que verifican que lo funcional sobrevive.

## 8. Accesibilidad

`AutomationProperties.Name` en todo control interactivo nuevo; foco visible mediante
`BrushFocusRing` (nunca `FocusVisualStyle` nulo para tapar defectos); `Escape` cierra el Command
Center y devuelve el foco al control anterior; navegación completa con `Tab`, flechas, `Enter` y
`Space`; el estado seleccionado nunca depende solo del color (§4).

## 9. Manejo de errores

Sin `catch (Exception) { }`, sin `Environment.Exit`/`Process.Kill`, sin `.Wait()`/`.Result`/
`.GetAwaiter().GetResult()` en la UI, sin `Task` ignoradas. `async void` solo en manejadores de
evento reales y siempre con captura de errores. Los fallos recuperables se registran, se muestran
de forma no modal, identifican la acción fallida y preservan el estado.

## 10. Estrategia de pruebas

- **Núcleo sin WPF** (`Nexo.Core.Tests`): registro, IDs únicos, búsqueda, alias, mayúsculas,
  espacios, ranking, disponibilidad, ejecución, error. Rápidas y deterministas.
- **WPF real** (`Nexo.App.Tests`, `StaWpfFixture` compartido por *collection fixture*): render de
  iconos con `RenderTargetBitmap` y medición de tinta, vistas reales insertadas y con layout
  forzado, recursos resueltos tras mutar el acento.
- **Persistencia** (`Nexo.Windows.Tests`): ida y vuelta, archivo inexistente, corrupto, versión,
  rangos, y que restaurar visuales no borre datos funcionales.

Nada de `Thread.Sleep` como sincronización, ni `Skip` para ocultar fallos, ni aserciones de
"belleza" imposibles de medir.

> **Nota de infraestructura:** `StaWpfFixture` pasó de `IClassFixture` a **fixture de colección**
> (`StaWpfCollection`). Con `IClassFixture`, xUnit crea una instancia por clase de prueba, así que
> la segunda clase que necesitara WPF construía un segundo `Application` y fallaba ("Application
> already exists"). La colección garantiza un único `Application` por proceso y serializa esas
> clases en un solo hilo STA, que es justo lo que exige el Dispatcher compartido.

## 11. Riesgos

- **`MainWindow.xaml.cs` es grande.** Cada cambio ahí tiene más superficie de regresión que en un
  archivo pequeño. Mitigación: la lógica nueva vive en Nexo.Core; en `MainWindow` solo queda el
  cableado.
- **Atajos globales pueden estar ocupados** por otras aplicaciones. `RegisterHotKey` ya informa sin
  bloquear el arranque; `Ctrl+K` sigue el mismo camino.
- **Recursos WPF entre diccionarios.** Riesgo demostrado por el crash de D1.1. Mitigación: solo
  `DynamicResource` entre archivos, y pruebas que resuelven recursos con WPF real.
- **Alcance.** D2 es un sprint amplio; el rediseño profundo de las nueve vistas es el elemento con
  mayor coste y menor riesgo si se pospone parcialmente. Se documenta con honestidad lo entregado
  frente a lo pendiente, en vez de declarar completado lo que no lo esté.

## 12. No objetivos

Sin servicios en la nube, sin proveedores de IA nuevos, sin API keys incrustadas, sin instalador,
sin updater, sin marketplace de plugins. Sin renombrar masivamente los namespaces `Nexo` a
`Kohana`. Sin sustituir `AdaptiveEnginePolicy`, `VoiceCoordinator` ni los motores. Sin convertir
Sistema en un administrador de tareas. Sin reescribir el proyecto.

## 13. Plan de commits

1. `docs: define Sakura Command Center v2 architecture` — este documento.
2. `fix: preserve navigation icon shapes when selected` — D2.0 + pruebas de render.
3. `feat: add reusable Sakura workspace components` — estilos compartidos.
4. `feat: add Sakura Command Center` — registro en Core + ventana Ctrl+K.
5. `feat: persist Sakura personalization settings` — restaurar visuales sin tocar lo funcional.
6. `test: cover navigation icons and command center`.
7. `docs: record Sakura Command Center D2 implementation` — informe final.

Cada commit compila y pasa la suite completa antes de crearse.
