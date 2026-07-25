# Hardware Capability Profile

> Documento de arquitectura de la Fase 2.1. Describe el modelo de dominio, las fuentes de detección
> en Windows y las decisiones de diseño detrás de `IHardwareCapabilityService`. No documenta motores
> de IA/voz ni modos de rendimiento — eso pertenece a la Fase 2.2 (Adaptive Engine Registry).

## Capacidad estable vs. métricas dinámicas

Kohana ya medía el **uso** del equipo (`Nexo.Core.Metrics.SystemSnapshot`, capturado cada pocos
segundos por `WindowsSystemMetricsService`): porcentaje de CPU, RAM usada, GPU usada, disco. Esos
valores cambian constantemente y no describen qué puede hacer el equipo, solo qué tan ocupado está
en este instante.

`HardwareCapabilitySnapshot` describe lo contrario: los rasgos que **no cambian de un segundo a
otro** — modelo de CPU, núcleos, RAM total instalada, GPU(s) disponibles, presencia de batería,
arquitectura, versión de Windows. Se captura una vez al iniciar la aplicación y de nuevo solo cuando
el usuario pide "Actualizar detección"; nunca en el temporizador de dos segundos que alimenta
`SystemSnapshot`.

Los dos modelos son deliberadamente independientes: `SystemSnapshot` no gana campos de identidad de
hardware, y `HardwareCapabilitySnapshot` no gana campos de uso instantáneo. Una vista puede combinar
ambos (como hace `SystemView`), pero el dominio los mantiene separados porque cambian a velocidades y
por razones distintas.

## Modelo de dominio (`Nexo.Core.Hardware`)

Todo el modelo es inmutable, sin dependencias externas, sin WMI, sin Registry, sin llamadas al
sistema y sin `PackageReference` en `Nexo.Core` (invariante verificado por prueba).

```
ProcessorCapability   — nombre, fabricante, núcleos físicos, procesadores lógicos, arquitectura de SO
                         y de proceso. Cada campo es nullable: "no se pudo determinar" es un valor
                         legítimo, no un error.
MemoryCapability       — RAM física total en bytes (nullable).
GraphicsCapability     — nombre, si es dedicada (nullable — no siempre se puede saber), memoria
                         dedicada y compartida en bytes (nullable).
HardwareCapabilitySnapshot
                       — Processor + Memory + lista de GraphicsAdapters + GPU preferida + presencia
                         de batería + edición/versión de Windows + fecha de captura + si vino de
                         caché. Es la fotografía cruda, sin interpretar.
HardwareCapabilityTier — Basic | Standard | Accelerated | HighPerformance.
HardwareDataConfidence — Unknown | Estimated | Known.
HardwareCapabilityReason
                       — un mensaje legible en español (p. ej. "32 GB de RAM.").
HardwareCapabilityProfile
                       — Snapshot + Tier + Summary + PositiveReasons + Limitations + MissingData +
                         OverallConfidence. Es la fotografía interpretada: el resultado que consume
                         la interfaz.
IHardwareCapabilityService
                       — GetCachedProfile() (síncrono, sin E/S) y RefreshAsync(CancellationToken)
                         (la única operación que vuelve a detectar).
```

`HardwareCapabilityProfile` incluye el `Snapshot` completo en vez de duplicar `CapturedAt`/`WasCached`
como campos propios: la política nunca inventa datos que no vinieron del snapshot, y la interfaz
puede leer tanto el veredicto (`Tier`, `Summary`, razones) como los datos crudos (nombre de CPU,
bytes de RAM) desde un único objeto.

## Manejo de datos desconocidos

Regla central del sprint: **un valor desconocido no equivale a cero.** Si la RAM no se pudo detectar,
esa categoría simplemente no participa en la clasificación — no se trata como "0 GB" ni fuerza
`Basic`. Concretamente, `HardwareCapabilityPolicy` traduce cada categoría conocida (RAM, procesadores
lógicos, GPU/VRAM) a un nivel entero 0–3, excluye las categorías con dato desconocido de la lista, y
el nivel final es `floor(promedio de los niveles conocidos)`. Con cero categorías conocidas, el
resultado es `Standard` con confianza `Unknown` — un punto neutro, no un castigo.

Cada campo desconocido individual (RAM, núcleos físicos, procesadores lógicos, presencia de GPU
dedicada, VRAM, batería) genera un mensaje en `MissingData` para que la interfaz pueda decir
explícitamente "esto no se pudo determinar" en vez de mostrar un `0` engañoso.

## Umbrales de clasificación

Centralizados como `const` en `HardwareCapabilityPolicy`, documentados en el propio código:

| Categoría | Basic | Standard | Accelerated | HighPerformance |
|---|---|---|---|---|
| RAM total | < 8 GiB | 8–16 GiB | 16–32 GiB | ≥ 32 GiB |
| Procesadores lógicos | ≤ 4 | 5–8 | 9–16 | ≥ 17 |
| GPU / VRAM dedicada | sin GPU dedicada conocida | GPU dedicada, VRAM desconocida o < 4 GiB | ≥ 4 GiB | ≥ 8 GiB |

## Razones de clasificación

`HardwareCapabilityPolicy.Evaluate` produce tres listas separadas de `HardwareCapabilityReason`:

- **`PositiveReasons`** — hechos conocidos que contribuyeron positivamente (p. ej. "GPU dedicada
  detectada.", "12 procesadores lógicos.").
- **`Limitations`** — categorías conocidas que resultaron en el nivel más bajo (p. ej. "RAM limitada
  (4 GB).").
- **`MissingData`** — categorías que no se pudieron determinar en absoluto.

El resultado es determinista: la misma entrada produce exactamente la misma clasificación y las
mismas listas de razones (verificado por prueba), porque `Evaluate` es una función pura sin estado ni
aleatoriedad.

## Fuentes de detección en Windows

`WindowsHardwareCapabilityService` (`Nexo.Windows.Hardware`) no detecta nada directamente: orquesta
cinco interfaces inyectables, cada una con una implementación real que nunca lanza fuera de sus
límites (captura sus propias excepciones y degrada a "desconocido").

| Interfaz | Implementación real | Fuente |
|---|---|---|
| `IProcessorInfoSource` | `RegistryProcessorInfoSource` | Registro (`CentralProcessor\0`) + `Environment.ProcessorCount` + P/Invoke `GetLogicalProcessorInformationEx` |
| `IMemoryInfoSource` | `WinApiMemoryInfoSource` | P/Invoke `GlobalMemoryStatusEx` |
| `IGraphicsInfoSource` | `RegistryGraphicsInfoSource` | Registro (`Control\Video\{GUID}\0000`) |
| `IBatteryInfoSource` | `WinApiBatteryInfoSource` | P/Invoke `GetSystemPowerStatus` |
| `IWindowsVersionInfoSource` | `RegistryWindowsVersionInfoSource` | Registro (`CurrentVersion`) |

Esta separación existe por dos razones: (1) el fallo de una fuente no debe destruir el resto del
snapshot — cada una se captura de forma aislada, tanto dentro de la implementación real como en el
orquestador — y (2) las pruebas pueden sustituir cualquier fuente por un doble de prueba configurable
sin depender del hardware real de la máquina que ejecuta CI.

Ninguna fuente requiere privilegios de administrador. Ninguna usa WMI (`System.Management`) ni agrega
un paquete NuGet nuevo: todo se resuelve con `Microsoft.Win32.Registry` y P/Invoke a `kernel32.dll`,
ya disponibles en el TFM `net10.0-windows`.

### Por qué Registro y P/Invoke en vez de WMI

WMI (`System.Management`) habría sido la ruta más simple para nombre de CPU/GPU, pero requiere un
paquete nuevo y sus consultas son más costosas y menos predecibles bajo políticas de seguridad
restrictivas. El registro expone los mismos datos de forma más barata y con degradación más simple
(una clave ausente es, sencillamente, `null`). La excepción notable es la memoria de vídeo: el campo
legado `HardwareInformation.MemorySize` es de 32 bits y se satura cerca de 4 GB en GPUs modernas, así
que se usa `HardwareInformation.qwMemorySize` (64 bits) como fuente primaria y el campo legado solo
como respaldo cuando el moderno no existe.

### Varias GPUs

Se conserva la lista completa de adaptadores detectados (`HardwareCapabilitySnapshot.GraphicsAdapters`),
nunca se descarta ninguno. La "GPU preferida" (`PreferredGraphicsAdapter`) se elige con una regla
documentada y determinista en `SelectPreferredGraphicsAdapter`: la GPU dedicada con más memoria
dedicada conocida; si ninguna es dedicada conocida, la primera detectada.

### Clasificación dedicada vs. integrada

Distinguir una GPU dedicada de una integrada por registro es inherentemente heurístico: se usa una
lista de marcadores de nombre conocidos para adaptadores integrados (Intel HD/UHD/Iris, Microsoft
Basic Render/Display) combinada con la presencia de memoria dedicada reportada (≥ 512 MB). Cuando
ninguna señal es concluyente, el resultado es `null` (desconocido) en vez de una adivinanza.

## Caché y refresco

El snapshot se cachea en memoria dentro de `WindowsHardwareCapabilityService`. `GetCachedProfile()`
nunca hace E/S — aplica la política pura sobre lo que ya se conoce (o sobre `HardwareCapabilitySnapshot.Empty`
si todavía no se detectó nada). `RefreshAsync(CancellationToken)` es la única operación que vuelve a
consultar el sistema; corre en `Task.Run` para no bloquear el hilo de UI y es cancelable — una
cancelación deja la caché existente intacta. La aplicación llama `RefreshAsync` una vez al construir
`MainWindow` y de nuevo bajo demanda desde el botón "Actualizar detección" de `SystemView`.

## Limitaciones conocidas

- La clasificación dedicada/integrada de GPU es heurística (ver arriba); en equipos con GPUs poco
  comunes puede quedar como "desconocida" en vez de acertar.
- Los umbrales de tier son fijos y pensados para el caso general; no se ajustan por tipo de carga de
  trabajo (eso es tarea de la Fase 2.2).
- La versión/edición de Windows depende de claves de registro documentadas pero no garantizadas por
  Microsoft en todas las builds futuras; si cambian, el campo simplemente queda `null`.
- No se probó en hardware real variado durante este sprint — la cobertura automatizada usa fuentes
  simuladas; el smoke test manual del usuario es el que valida datos reales.

## Relación futura con el Adaptive Engine Registry (Fase 2.2)

Este sprint deliberadamente **no** selecciona motores ni expone modos de rendimiento. El
`HardwareCapabilityProfile` es la entrada que la Fase 2.2 usará para decidir qué motores ofrecer y
qué modo (Automático/Eco/Equilibrado/Máximo) recomendar por defecto — pero esa lógica de decisión no
existe todavía. `HardwareCapabilityPolicy` no conoce ningún motor, modelo de IA ni nombre de proveedor.

## Datos que no se recopilan / privacidad

- No se registra información personal ni identificadores únicos de hardware (números de serie, MAC,
  IDs de licencia).
- No se envía telemetría: toda la detección es local y el resultado solo se muestra en la interfaz o
  se guarda en la caché en memoria del proceso.
- No se realizan consultas costosas de forma periódica; la detección solo ocurre al iniciar y bajo
  demanda explícita del usuario.
- Los errores de detección nunca exponen trazas de excepción en la interfaz — `DiagnosticsWindow`
  muestra únicamente mensajes legibles predefinidos.
