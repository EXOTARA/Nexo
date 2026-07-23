# Registro de implementación — Kohana 1.0

> **Este archivo es la memoria persistente del proyecto.**
> Todo agente que retome el trabajo debe leerlo **primero** y actualizarlo **antes** de quedarse sin
> contexto. Las decisiones registradas aquí y en `PRODUCT_VISION.md` **no vuelven a preguntarse**.

---

## ESTADO ACTUAL

| Campo | Valor |
|---|---|
| **Fase actual** | Fase 1, paso **1.1 completado** (pruebas de caracterización, verde) |
| **Siguiente fase** | Fase 1, paso 1.2 (composition root + DI) |
| **Rama** | `release/kohana-1.0-rc` — **creada y activa** |
| **Versión base** | **0.9.5-beta** (verificada en `Directory.Build.props`) |
| **Última actualización** | 2026-07-23 |
| **Bloqueador activo** | Ninguno para iniciar 1.1 |

### ✅ Baseline medido — 2026-07-23

Medido en Windows sobre el repositorio real. Ningún valor es estimado; los campos que **todavía no
pueden medirse** en esta fase se marcan `NO MEDIDO` en lugar de rellenarse.

```
[x] Sistema operativo:        Microsoft Windows NT 10.0.26200.0 (Windows 11 Pro, build 26200)
                              → cumple el mínimo de la decisión B (26100+)
[x] Ruta del repositorio:     C:\Dev\Nexo
[x] Rama:                     release/kohana-1.0-rc
[x] dotnet --info (SDK):      10.0.302  (MSBuild 18.6.11, host 10.0.10, RID win-x64)
                              global.json fija 10.0.302 con rollForward=latestFeature
[x] git status (limpio?):     limpio (sin cambios pendientes)
[x] Commit inicial (hash):    144bb13e794dcf085fb31df0fba171896583f169
[x] Versión real del código:  0.9.5-beta
[x] Total de pruebas:         356   (Nexo.Core.Tests 353 + Nexo.Windows.Tests 3)
[x] Pruebas fallidas:         0
[x] Pruebas omitidas:         0
[x] Warnings de compilación:  0
[x] Errores de compilación:   0
[x] Tiempo de restore:        1.70 s   (en frío, tras borrar bin/obj)
[x] Tiempo de build Release:  2.52 s   (MSBuild, en frío; 2.80 s de reloj)
[x] Tiempo de test:           Core 210 ms · Windows 308 ms  (2.81 s de reloj, `--no-build`)
[ ] Tamaño del portable:      NO MEDIDO — requiere `dotnet publish` (Fase 10)
[ ] Tamaño del instalador:    NO MEDIDO — requiere compilar `installer/Kohana.iss` (Fase 10)
[ ] SHA-256 del portable:     NO MEDIDO — depende del portable (Fase 10)
```

Comandos exactos ejecutados:

```powershell
dotnet restore .\Nexo.slnx
dotnet build   .\Nexo.slnx -c Release --no-restore
dotnet test    .\Nexo.slnx -c Release --no-build
```

**Incidencia registrada durante la medición:** el primer `dotnet build` falló con `MSB3021`/`MSB3027`
porque una instancia de `Kohana.exe` (PID 31064) lanzada desde `src\Nexo.App\bin\Release` mantenía
bloqueados `Nexo.Core.dll` y `Nexo.Windows.dll`. Se cerró el proceso con autorización del
propietario y se repitió la medición en frío. **No es un defecto del código**, pero conviene
documentarlo: compilar con la app corriendo desde el directorio de salida siempre fallará.

### Verificación de versión (0.9.5 vs 0.9.6)

La versión real del código es **0.9.5-beta**. La cadena `0.9.6-beta` aparece **una sola vez** en el
repositorio, en `docs/ROADMAP.md:54`, como encabezado de un sprint **planificado y no implementado**.
No hay código, entrada de `CHANGELOG` publicada ni etiqueta de 0.9.6. Por tanto **no se rebaja código
ni se sustituye base**: documentación y código ya coinciden en 0.9.5-beta.

Se corrigieron en cambio las **cifras estáticas** de `CURRENT_STATE_AUDIT.md`, que se habían tomado
del ZIP `Kohana-0_9_5-foundation-base.zip` y no del repositorio con los commits 0.9.3–0.9.5
consolidados. Ver `CURRENT_STATE_AUDIT.md` §0. Resumen: `MainWindow.xaml.cs` mide **3,532** líneas
(no 4,007) y hay **356** pruebas (no 196). La conclusión cualitativa —God Object y bloqueador raíz—
no cambia.

---

## DECISIONES TOMADAS (no volver a preguntar)

Todas registradas en detalle en `PRODUCT_VISION.md`. Resumen ejecutable:

| # | Decisión | Valor |
|---|---|---|
| A | Fuentes de verdad | Claude Code en Windows local **+** GitHub Actions `windows-latest` |
| B | Windows mínimo | **Windows 11 24H2 (build 26100)+**. TFM objetivo `net10.0-windows10.0.26100.0` |
| C | Licencia | **MIT** propio. Dependencias: MIT/Apache-2.0/BSD. Sin restricción no comercial |
| D | Experiencias día 1 | "abre X" sin LLM · "¿qué es esto?" interrumpible · instalación sin terminal |
| E | Wake words | Estables: `Oye Kohana`, `Kohana`. Experimentales: `Ey Kohana`, `Hey Kohana` |
| F | Permisos | Abrir PowerShell = **sin** confirmación; ejecutar comando dentro = **con** confirmación |
| G | Memoria | **Opt-in**. Nada se escribe sin aprobación. Diario automático off por defecto |
| H | Dataset de audio | Opt-in, local, revisable, exportación manual, **nunca** subida automática |
| I | Firma/updates | **Sin Authenticode hoy**. GitHub Releases. Notificar, no actualizar en silencio |
| J | Telemetría | **Ninguna remota**. Logs locales anonimizados, exportación manual |
| K | Skills 1.0 | 5 estables + 1 experimental (dev tools). **Sin marketplace** |
| — | Extracción de `MainWindow` | **7 pasos**, cada uno compilable + probado + commit propio |

Decisiones estructurales adicionales:
- `Nexo.Core` permanece **sin dependencias** y en `net10.0` puro. Invariante.
- Namespaces internos `Nexo.*` **se conservan**. No hay fugas visibles al usuario.
- Vosk y SAPI pasan a `FallbackHeredado`; **no se retiran** hasta que un sustituto gane medido.

---

## TAREAS TERMINADAS

### Fase 0 (2026-07-22)
- [x] Auditoría estática completa de la base 0.9.5 (222 `.cs`, 22 `.xaml`, 16 `.md`)
- [x] Delta 0.9.4 → 0.9.5 identificado (6 nuevos, 31 modificados, 0 eliminados)
- [x] Hallazgo crítico documentado: `MainWindow.xaml.cs` = 4,007 líneas / 224 métodos / ~28 servicios
- [x] Bloqueador de TFM identificado (WinRT/OCR inaccesible con `net10.0-windows`)
- [x] Verificado: 0 `AutomationProperties` en XAML (accesibilidad ausente)
- [x] Verificado: sin fugas de marca "Nexo" en UI (solo claves de estilo internas)
- [x] Verificado: escritura de settings es atómica; falta `.bak` de recuperación
- [x] `PRODUCT_VISION.md` con decisiones A–K
- [x] `CURRENT_STATE_AUDIT.md`
- [x] `STABLE_RELEASE_PLAN.md` (Fases 0–10 + 7 pasos de extracción)
- [x] `ACCEPTANCE_CRITERIA.md` (presupuestos marcados `PENDIENTE DE CALIBRAR`)
- [x] `TEST_MATRIX.md` (24 escenarios; 7 marcados como bloqueantes de seguridad)
- [x] `SECURITY_MODEL.md` (matriz de permisos + modelo de amenaza)
- [x] `PRIVACY_BOUNDARIES.md` (matriz local/remoto)
- [x] `MIGRATION_PLAN.md`
- [x] `KNOWN_LIMITATIONS.md`
- [x] ADR 0001 (runtime), 0002 (hardware), 0003 (TFM), 0004 (wake word)

---

### Fase 1.1 — pruebas de caracterización (2026-07-23)

Objetivo: congelar la conducta observable **antes** de mover nada de `MainWindow`.
Resultado: **520 pruebas, 0 fallidas, 0 warnings.** 164 pruebas nuevas.

| Área exigida | Dónde queda congelada |
|---|---|
| Navegación | `ShellNavigationCharacterizationTests` (destinos, alternador de Ajustes, módulo oculto) |
| Inicio y cierre | `ShellLifecycleCharacterizationTests` (`--background`, bandeja, salida explícita) |
| Segunda instancia | `SingleInstanceCharacterizationTests` |
| Comandos locales | `PromptDispatchCharacterizationTests` (orden de despacho completo) |
| PowerShell: abrir vs. ejecutar | `LocalActionPermissionCharacterizationTests` |
| Wake word y voz | `VoiceRuntimeCharacterizationTests` |
| Tareas, enfoque, rutinas | `PromptDispatchCharacterizationTests` (precedencia real entre parsers) |
| Vision y privacidad | `ShellLifecycleCharacterizationTests` (ventanas sensibles) |
| Resource Governor | `ShellLifecycleCharacterizationTests` (Normal/Busy/Game, umbrales 88/92/92) |
| Preferencias y migraciones | `PreferencesMigrationCharacterizationTests` (v0→v16, clamps, idempotencia) |
| Settings corruptos | `SettingsStoreCharacterizationTests` (escenario 4 de `TEST_MATRIX`) |

**Seams extraídos** (mínimos, sin cambio de conducta, verificados con build + 520 pruebas +
arranque real de la aplicación):

1. `Nexo.Core/Shell/ShellNavigationPolicy.cs` — **nuevo**. Reglas puras de navegación que ya
   aplicaba `MainWindow`: destinos conocidos, alternador de Ajustes, caída al Asistente al
   ocultar el módulo activo, y mapa de órdenes de navegación. `MainWindow` delega en él y usa
   sus constantes para construir `_views`, de modo que no puede haber deriva entre ambos.
2. `SingleInstanceCoordinator` movido de `Nexo.App` a
   `Nexo.Windows/WindowsIntegration/`. No tenía ninguna dependencia de WPF y desde `Nexo.App`
   era inalcanzable para las pruebas. Se añadió un parámetro opcional `instanceKey` cuyo valor
   por defecto (`null`) **conserva exactamente** los nombres históricos del mutex y del evento;
   solo las pruebas pasan una clave propia, para no colisionar con una instancia real de Kohana.

> Se descartó referenciar `Nexo.App` desde el proyecto de pruebas: arrastra `UseWPF`, que
> cambia el conjunto de *implicit usings* y provoca `MSB3277` (conflicto de `WindowsBase`).
> Habría subido los warnings de 0 a 1 y el baseline exige 0.

**Ninguna prueba depende de píxeles, layout ni temporización de animación**, conforme a la
instrucción de la fase.

---

## TAREAS PENDIENTES

### Fase 0 — cierre (en Windows) ✅ 2026-07-23
- [x] `git checkout -b release/kohana-1.0-rc`
- [x] Confirmar SO, ruta, rama, `dotnet --info`, `git status`
- [x] `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`
- [x] Rellenar el bloque de baseline de arriba
- [x] Corregir cifras estáticas de `CURRENT_STATE_AUDIT.md` contra el código real
- [x] Commit: `docs: record measured Kohana 1.0 baseline`

### Fase 1 — extracción (7 pasos, en orden)
- [x] 1.1 Pruebas de caracterización de `MainWindow` ✅ 2026-07-23
- [ ] 1.2 Composition root + DI **sin cambiar comportamiento**
- [ ] 1.3 Extraer coordinador de voz
- [ ] 1.4 Extraer coordinador de navegación
- [ ] 1.5 Extraer tareas, enfoque y rutinas
- [ ] 1.6 Extraer IA y Vision
- [ ] 1.7 `MainWindow` como vista mínima (objetivo < 500 líneas)

### Fases 2–10
Ver `STABLE_RELEASE_PLAN.md`. No adelantar fases.

---

## PRUEBAS

| Fase | Total | Fallidas | Warnings | Nota |
|---|---|---|---|---|
| Baseline (2026-07-23) | **356** | **0** | **0** | Core 353 + Windows 3. Commit `144bb13`. Build Release en frío 2.52 s |
| Fase 1.1 (2026-07-23) | **520** | **0** | **0** | Core 494 + Windows 26. +164 pruebas de caracterización. Cero regresiones |

---

## RIESGOS ACTIVOS

| # | Riesgo | Severidad | Mitigación |
|---|---|---|---|
| 1 | La extracción de `MainWindow` rompe conducta no cubierta por pruebas | **Alta** | Paso 1.1 (caracterización) **antes** de mover nada |
| 2 | El cambio de TFM rompe resolución de paquetes | Media | Aplazado a Fase 7, aislado a `Nexo.Windows`/`Nexo.App` |
| 3 | Ningún motor candidato gana medido al fallback | Media | Aceptable: Vosk/SAPI siguen. Documentar y seguir |
| 4 | Alcance de 1.0 no cabe responsablemente | Media | Entregar RC honesta + bloqueadores, nunca 1.0 falsa |
| 5 | Accesibilidad ausente descubierta tarde | Media | Es criterio de salida; Fase 9 dedicada |
| 6 | Presupuestos de latencia irreales sin baseline | Media | Marcados `PENDIENTE DE CALIBRAR`; no reportar como logrados |
| 7 | **Rutinas eclipsan órdenes de enfoque** (ver D1 abajo) | **Alta** | Congelado en prueba. Corregir en 1.4/1.5 con el coordinador de navegación y dominio |
| 8 | **`OpenApplication` permite ejecución arbitraria sin confirmación** (D2) | **Alta** | Congelado en prueba. Corregir en Fase 5 (agencia tipada) |
| 9 | `JsonSettingsStore.Load` no normaliza en las rutas de recuperación (D3) | Media | Congelado en prueba. Corregir junto al `.bak` de L7 |
| 10 | `SingleInstanceCoordinator.Dispose` no es idempotente (D4) | Baja | Congelado en prueba. Corregir al introducir el contenedor en 1.2 |

### Defectos descubiertos por la caracterización (1.1)

Se **congelaron tal cual**, no se corrigieron: la fase 1.1 documenta la conducta actual. Cada
uno tiene ya una prueba que fallará cuando se arregle, obligando a un cambio consciente.

**D1 — Las rutinas se comen las órdenes de enfoque. (Alta, visible para el usuario)**
`SpanishRoutineCommandParser` usa `^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+)?(?:rutina\s+)?(?<name>.+)$`,
que captura *cualquier* frase que empiece por "inicia". Como el parser de rutinas corre
**primero** en `MainWindow.HandlePromptAsync`, y `MainWindow` **no reintenta** cuando
`FindBestMatch` no encuentra nada, decir *"Inicia un temporizador de 20 minutos"* responde
*"No encontré una rutina que coincida con..."* y **no arranca ningún temporizador**.
Afecta también a *"Inicia un descanso"* e *"Inicia un pomodoro"*.
`SpanishFocusCommandParserTests` no lo detecta porque prueba el parser **aislado**, sin la
precedencia real del shell. Es justamente el tipo de fallo que solo aparece al caracterizar la
composición.

**D2 — `OpenApplication` es ejecución arbitraria sin confirmación. (Alta, seguridad)**
`NexoAutomationActionExecutor.OpenApplication` reenvía `action.Arguments` al proceso, y
`AutomationPermissionPolicy` clasifica esa acción como `Reversible`, es decir **sin
confirmación**. Un paso de rutina con `Target="powershell.exe"` y `Arguments="-Command ..."`
ejecuta lo que sea sin el paso de aprobación que exige `SECURITY_MODEL` (escenario 22).
*Mitigación existente:* las rutinas las crea el propio usuario en la interfaz y esa creación es
la aprobación (`PRODUCT_VISION` §F). No es una vía de explotación remota. Pero el invariante
**no está aplicado técnicamente**, que es lo que `SECURITY_MODEL` §4 exige explícitamente.
En contraste, `OpenTerminal` **sí** es seguro: ignora `Arguments` y construye siempre su propia
línea de comandos. Esa asimetría queda congelada como invariante.

**D3 — `Load` no normaliza en las rutas de recuperación. (Media)**
`JsonSettingsStore.Load` solo llama a `Normalize()` en la ruta de éxito. Con archivo ausente o
corrupto devuelve `new ShellPreferences()` con `SchemaVersion = 0`. Consecuencia: el siguiente
`Save` reejecuta **todas** las migraciones desde 0, incluida la de v10
(`HasCompletedOnboarding = false`), así que un valor asignado justo antes de guardar se pierde
en ese ciclo. Tras un archivo corrupto los valores por defecto son aceptables —los datos ya
eran ilegibles— pero el shell no puede marcar el onboarding como completado en ese arranque.
La degradación dura un solo ciclo.

**D4 — `SingleInstanceCoordinator.Dispose` no es idempotente. (Baja)**
Un segundo `Dispose` lanza `ObjectDisposedException` (`_cancellation.Cancel()` sobre un CTS ya
liberado). Hoy no se manifiesta porque `App.OnExit` pone el campo a `null`, pero un contenedor
de DI que libere de forma genérica —justo lo que llega en 1.2— sí puede sacarlo a la luz.

**D5 — La propiedad del mutex es por hilo, no por proceso. (Informativo, trampa de extracción)**
Dos `SingleInstanceCoordinator` en el **mismo hilo** se consideran ambos primarios, porque el
segundo `WaitOne` es una adquisición recursiva del mismo dueño. En producción no ocurre —cada
instancia es un proceso distinto— pero quien extraiga o comparta este componente en 1.2 debe
saberlo. Queda congelado en `MutexOwnershipIsPerThread_NotPerProcess`.

---

## SIGUIENTE PASO EXACTO

Fase 1.1 cerrada y **verde**: 520 pruebas, 0 fallidas, 0 warnings, aplicación arrancada y
verificada. La red de seguridad existe.

El siguiente paso es **1.2 — composition root + inyección de dependencias, sin cambiar
comportamiento**. Plan exacto:

1. Añadir `Microsoft.Extensions.DependencyInjection` (MIT) **solo** a `Nexo.App`.
   `Nexo.Core` sigue con **cero** `PackageReference` — invariante verificada por prueba.
2. Crear `Nexo.App/Composition/KohanaServiceCollection.cs` que registre exactamente los
   servicios que hoy `MainWindow` instancia en la declaración de sus campos, con **el mismo
   tipo concreto y el mismo tiempo de vida efectivo** (singleton por ventana).
3. Construir el contenedor en `App.OnStartup`, **antes** de crear `MainWindow`, y pasarlo por
   constructor. `MainWindow` mantiene su firma actual (`startHidden`, `managedOllamaSupervisor`)
   más el proveedor, para no tocar el orden de arranque.
4. Sustituir los **6 servicios de interfaz** primero (`IAiChatService`, `IAudioMixerService`,
   `IVoiceInputService`, `IVoiceOutputService`, `IWakeWordService`, `IScreenCaptureService`),
   que son los que bloquean el Adaptive Engine Registry. Los 25 campos restantes instanciados
   con `new` se migran después, en el mismo paso pero en commits separados si crece.
5. **No** introducir interfaces nuevas, **no** renombrar tipos, **no** cambiar el orden de
   suscripción de eventos del constructor: ese orden es conducta observable y no está cubierto
   por pruebas.
6. Resolver antes D4 (`Dispose` no idempotente): el contenedor libera de forma genérica y lo
   destapará. Es un arreglo de tres líneas con prueba ya escrita.
7. Criterio de salida: build en Release con **0 warnings**, **520 pruebas verdes**, arranque
   real de la aplicación verificado, y `MainWindow` sin ningún `= new` de servicio en la
   declaración de campos.

Riesgo principal de 1.2: el orden de construcción. Hoy los campos se inicializan en orden de
declaración y luego el constructor cablea eventos. Un contenedor cambia *cuándo* se construye
cada servicio. Mitigación: registrar todo como singleton y resolver de forma **ansiosa** en el
mismo orden que hoy, antes de cablear eventos.

**No iniciar 1.3 sin que 1.2 esté verde.**
