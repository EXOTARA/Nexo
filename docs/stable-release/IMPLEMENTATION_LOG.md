# Registro de implementación — Kohana 1.0

> **Este archivo es la memoria persistente del proyecto.**
> Todo agente que retome el trabajo debe leerlo **primero** y actualizarlo **antes** de quedarse sin
> contexto. Las decisiones registradas aquí y en `PRODUCT_VISION.md` **no vuelven a preguntarse**.

---

## ESTADO ACTUAL

| Campo | Valor |
|---|---|
| **Fase actual** | **Fase 1.3A parcial: coordinador aislado, MainWindow aún no migrado** |
| **Siguiente fase** | Fase 1, paso 1.3B (migrar `MainWindow` a `VoiceCoordinator`) — **no iniciada** |
| **Rama** | `release/kohana-1.0-rc` — **creada y activa** |
| **Versión base** | **0.9.5-beta** (verificada en `Directory.Build.props`) |
| **Última actualización** | 2026-07-23 |
| **Bloqueador activo** | Ninguno para iniciar 1.3B |

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

### Fase 1.1.1 — correctiva (2026-07-23)

Corrige los tres defectos que la caracterización destapó y que bloqueaban o ensuciaban la
fase 1.2. **606 pruebas, 0 fallidas, 0 warnings.** +86 pruebas sobre 1.1.

#### Auditoría del diff previa a los cambios

El contador de la interfaz mostraba **+10.512 / −31.997**, cifra que no correspondía al trabajo
realizado. Comprobado:

| Comparación | Resultado |
|---|---|
| `144bb13..HEAD` (trabajo de fases 0 y 1.1) | **+2.268 / −106**, 19 archivos |
| `main..HEAD` | +14.473 / −1.137 |
| **`origin/main..HEAD`** | **+10.512 / −31.997** ← coincide exactamente con la interfaz |

Conclusión: **el contador compara contra `origin/main`, no contra `144bb13`.** Las 31.997
eliminaciones son **reales pero ajenas a este trabajo**: provienen de `264c525`
*"chore: remove tracked patch backups"*, que borró `.nexo-patch-backup/` y es **ancestro de
`144bb13`**, es decir, anterior a la fase 0. La rama local `main` está además por detrás de
`origin/main`.

Verificaciones adicionales, todas negativas (sin problema):
- **Cero** archivos de `.nexo-patch-backup/` tocados en `144bb13..HEAD`.
- **Cero** binarios, artefactos de build o archivos generados añadidos.
- **Sin conversión masiva de finales de línea**: `git diff --shortstat` y
  `--ignore-all-space` dan cifras idénticas (+2.268 / −106).
- Un único renombrado, **intencional y documentado**: `SingleInstanceCoordinator` de
  `Nexo.App` a `Nexo.Windows` (fase 1.1).
- Working tree **limpio** antes y después.
- No hay pérdida accidental ni archivos ajenos a las fases 0 y 1.1.

> Nota operativa: `core.autocrlf=true` sin `.gitattributes`. Los archivos están en LF en el
> árbol de trabajo y Git los almacena en LF, así que hoy no hay diferencia. Conviene añadir un
> `.gitattributes` antes de que alguien haga un `checkout` limpio en otra máquina.

#### D1 — Rutinas eclipsaban las órdenes de enfoque ✅ `667a873`

`RoutineMatchConfidence` distingue el reclamo **explícito** ("la rutina X", "modo X") del
**inferido** ("inicia X"), y `PromptDispatchPolicy` concentra el orden normativo: rutina
explícita → enfoque → tareas → rutina inferida **solo si existe** → comando local → IA.
`MainWindow` evalúa los cuatro parsers y delega la decisión, en vez de quedarse con el primer
reclamo de la cascada. Sin listas de excepciones.

#### D2 — Ejecución arbitraria sin confirmación ✅ `4e3524d`

`ShellExecutionPolicy` incorpora los argumentos a la evaluación tipada de riesgo. Abrir un
intérprete sin argumentos sigue sin pedir confirmación; con cualquier argumento pasa a
`Sensitive`. La detección normaliza rutas, comillas, variables de entorno, separadores,
mayúsculas, extensión omitida y los espacios y puntos finales que Windows descarta, e inspecciona
también los argumentos para detectar el rodeo de invocar un intérprete desde otro programa.

`RoutineExecutionApproval` viaja como argumento de cada ejecución y **nunca** se guarda en la
rutina ni en la acción: aprobar una rutina al crearla no concede permiso permanente. `RoutineRunner`
rechaza los pasos sensibles sin aprobación explícita, de modo que el permiso se aplica **en el
ejecutor** y no se confía a que la interfaz haya preguntado.

#### D4 — `Dispose` no idempotente ✅ `787db71`

Guarda `_disposed` en `SingleInstanceCoordinator`, que **previene** la excepción en lugar de
capturarla. Mismo patrón que ya usaban `ManagedOllamaSupervisor` y `TrayIconController`, que no
necesitaban cambios.

#### Smoke test manual (aplicación real, 2026-07-23)

Conducido sobre la app compilada en Release, dirigiendo la interfaz con UI Automation.

| Paso | Resultado |
|---|---|
| Abrir Kohana | ✅ arranca y la ventana responde |
| Navegación | ✅ los 8 destinos del riel: Inicio · Asistente · Hoy · Enfoque · Rutinas · Audio · Captura · Sistema, y vuelta a Inicio |
| Crear/iniciar temporizador | ✅ *"Inicia un temporizador de 20 minutos"* → **"Inicié temporizador por 20 minutos."**; la vista Enfoque muestra `EN CURSO · 19:40`. **D1 verificado en vivo** |
| Rutina explícita | ✅ *"inicia la rutina estudio"* llega al subsistema de rutinas y la ejecuta |
| Abrir PowerShell sin ejecutar | ✅ procesos 1 → 2, **sin confirmación**, "PowerShell abierto en C:\Users\Usuario" |
| Rutina con shell exige confirmación | ✅ diálogo *"Ejecutar Smoke Shell — Kohana ejecutará estas acciones: 1. Abrir powershell.exe"*; al pulsar **No** → "Cancelé la rutina Smoke Shell." y **cero** procesos PowerShell. **D2 verificado en vivo** |
| Cerrar y reabrir | ✅ reabre correctamente y conserva el estado |
| Segunda instancia | ✅ la segunda termina sola; sobrevive el PID original |

Observación honesta: al ejecutar *"inicia la rutina estudio"* el informe fue "0 de 3 acciones".
Las tres causas son **del entorno, no del cambio**: Spotify y Discord no estaban en ejecución
(0 procesos) y ya había un temporizador activo creado por el paso anterior. Los mensajes de la
propia app lo dicen literalmente.

Para probar la confirmación se añadió temporalmente una rutina "Smoke Shell" a
`%LOCALAPPDATA%\Kohana\routines.json`, con copia de seguridad previa, y **se restauró el archivo
original** al terminar (4 rutinas: Programación, Estudio, Descanso, test).

---

### Checkpoint portable — 2026-07-23

> ⚠️ **Esto es un checkpoint de desarrollo, NO una Release Candidate ni una versión estable.**
> Se publica únicamente para poder probar el estado actual fuera del entorno de compilación.
> No cumple los criterios de salida de `ACCEPTANCE_CRITERIA.md` §1 y **no debe distribuirse como
> 1.0 ni como RC**. Siguen abiertos, entre otros, el perfil de hardware, la memoria transparente,
> las skills, OCR/UI Automation y la accesibilidad (`AutomationProperties` = 0).

| Campo | Valor |
|---|---|
| **Commit** | `3edba24` (`docs: record phase 1.1.1 corrective work`) |
| **Rama** | `release/kohana-1.0-rc` |
| **Fecha** | 2026-07-23 |
| **SDK** | .NET 10.0.302 (host 10.0.10, RID win-x64) |
| **Versión del producto** | `0.9.5-beta` — **sin cambios**, no se versionó el checkpoint |
| **Pruebas** | **606** (576 `Nexo.Core.Tests` + 30 `Nexo.Windows.Tests`), **0 fallidas**, 0 omitidas |
| **Warnings de compilación** | **0** |
| **Portable** | `artifacts\Kohana-0.9.5-beta-checkpoint-win-x64\` — self-contained win-x64, 489 archivos, 224,17 MB |
| **ZIP** | `artifacts\Kohana-0.9.5-beta-checkpoint-win-x64.zip` — 91.963.645 bytes (87,7 MB) |
| **SHA-256** | `6f4da7bf97a6a17f3d4b0fae550470f71b9bf6832d010165cd08e1d6693c61fb` |

Comando de publicación:

```powershell
dotnet publish .\src\Nexo.App\Nexo.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\Kohana-0.9.5-beta-checkpoint-win-x64
```

**Smoke test sobre el portable publicado** (no sobre `bin\Release`):

| Paso | Resultado |
|---|---|
| `Kohana.exe` existe | ✅ 285.696 bytes |
| La aplicación abre | ✅ ventana «Kohana», vista inicial «Inicio» |
| Navegación | ✅ Hoy · Enfoque · Asistente |
| Comando local | ✅ *"cómo está mi PC"* → «CPU 31% · RAM 50% · GPU 3%. Mayor uso de memoria: zen · 1380 MB.» — resuelto localmente, sin LLM |
| Cierre correcto | ✅ cerrado por PID exacto; no se tocó ningún otro proceso |

`artifacts/`, `bin/` y `obj/` ya estaban cubiertos por `.gitignore` (líneas 2–4): **no se añadió
ninguna regla** y **ningún artefacto se versiona**.

---

### Checkpoint portable — Fase 1.2 (2026-07-23)

> ⚠️ **Esto es un checkpoint de desarrollo, NO una Release Candidate ni una versión estable.**
> Mismas salvedades que el checkpoint anterior: no cumple `ACCEPTANCE_CRITERIA.md` §1, no
> distribuir como 1.0 ni como RC. El smoke test manual **interactivo** (navegación por clics, voz,
> wake word) no se repitió en esta sesión — ver riesgo #13.

| Campo | Valor |
|---|---|
| **Commit** | `2195805` (`docs: record composition root migration`) |
| **Rama** | `release/kohana-1.0-rc` |
| **Fecha** | 2026-07-23 |
| **SDK** | .NET 10.0.302 (host 10.0.10, RID win-x64) |
| **Versión del producto** | `0.9.5-beta` — sin cambios, no se versionó el checkpoint |
| **Pruebas** | **615** (576 `Nexo.Core.Tests` + 39 `Nexo.Windows.Tests`), **0 fallidas**, 0 omitidas |
| **Warnings de compilación** | **0** |
| **Portable** | `artifacts\Kohana-0.9.5-beta-phase1.2-win-x64\` — self-contained win-x64, 491 archivos |
| **ZIP** | `artifacts\Kohana-0.9.5-beta-phase1.2-win-x64.zip` — 89.272.344 bytes (85,1 MB) |
| **SHA-256** | `29c32a43e1e50d4755c8d5a2ceba233becd2c22cfe7a0463354a9ba34533181f` |

Comando de publicación:

```powershell
dotnet publish src\Nexo.App\Nexo.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\Kohana-0.9.5-beta-phase1.2-win-x64
```

**Smoke test sobre el portable publicado** (no sobre `bin\Release`; sin herramienta de
automatización de UI de escritorio disponible en esta sesión):

| Paso | Resultado |
|---|---|
| `Kohana.exe` existe | ✅ 285.696 bytes (idéntico al checkpoint anterior) |
| La aplicación abre | ✅ proceso alcanza `Responding=True` en <4 s, sin `WerFault.exe` |
| Cierre correcto | ✅ cerrado por PID exacto iniciado por este agente; ningún otro proceso tocado |
| Navegación por clics, voz, wake word, rutinas | ⚠️ no probado — ver riesgo #13 |

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
- [x] 1.2 Composition root + DI **sin cambiar comportamiento** ✅ 2026-07-23
- [ ] 1.3 Extraer coordinador de voz — **1.3A parcial** ✅ 2026-07-23 (coordinador aislado
      + wiring en el composition root); **1.3B pendiente** (migrar `MainWindow`)
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
| Fase 1.1.1 (2026-07-23) | **606** | **0** | **0** | Core 576 + Windows 30. +86 pruebas. Correcciones D1, D2 y D4 |
| Fase 1.2 (2026-07-23) | **615** | **0** | **0** | Core 576 (sin cambios) + Windows 39. +9 pruebas de composition root e invariantes |
| Fase 1.3A (2026-07-23) | **638** | **0** | **0** | Core 576 (sin cambios) + Windows 62. +23 pruebas: `VoiceCoordinator` aislado (17) e invariantes de composition root/estructurales (6) |

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
| 7 | ~~Rutinas eclipsan órdenes de enfoque (D1)~~ | — | ✅ **Resuelto** en 1.1.1 (`667a873`), verificado en vivo |
| 8 | ~~`OpenApplication` permite ejecución arbitraria sin confirmación (D2)~~ | — | ✅ **Resuelto** en 1.1.1 (`4e3524d`), verificado en vivo |
| 9 | `JsonSettingsStore.Load` no normaliza en las rutas de recuperación (D3) | Media | **Abierto.** Congelado en prueba. No bloquea 1.2. Corregir junto al `.bak` de L7 |
| 10 | ~~`SingleInstanceCoordinator.Dispose` no es idempotente (D4)~~ | — | ✅ **Resuelto** en 1.1.1 (`787db71`) |
| 11 | Propiedad del mutex por hilo, no por proceso (D5) | Informativo | **Abierto.** No es un defecto; es una trampa para quien comparta el componente en 1.2. Congelado en prueba |
| 12 | ~~Sin `.gitattributes` con `core.autocrlf=true`~~ | — | ✅ **Resuelto** en 1.2 (`af13d7f`): `.gitattributes` mínimo, sin renormalización masiva (2 archivos de cambio real, `.gitattributes` nuevo) |
| 13 | Smoke test manual interactivo (clics, voz, wake word) no repetido en 1.2 | Media | Esta sesión no tuvo herramienta de automatización de UI de escritorio. Ver detalle en "Fase 1.2" arriba. **Recomendado antes de considerar el checkpoint apto para uso diario** |
| 14 | Discrepancia de líneas en `MainWindow.xaml.cs`: la auditoría de 1.1.1 documentaba 3.532, medido ahora en el checkpoint `82a36fb` (antes de tocar nada en 1.2) da **4.027** | Baja | Descubierto incidentalmente al medir para 1.2, no causado por esta fase. `CURRENT_STATE_AUDIT.md` se corrige con la cifra medida hoy; no se investigó la causa de la discrepancia anterior por estar fuera de alcance de 1.2 |

### Defectos descubiertos por la caracterización (1.1)

Se congelaron tal cual en 1.1, y **D1, D2 y D4 quedaron corregidos en la fase 1.1.1**.
D3 y D5 siguen abiertos y **no bloquean la fase 1.2**.

**D1 — Las rutinas se comen las órdenes de enfoque. (Alta, visible) — ✅ RESUELTO en 1.1.1**
`SpanishRoutineCommandParser` usa `^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+)?(?:rutina\s+)?(?<name>.+)$`,
que captura *cualquier* frase que empiece por "inicia". Como el parser de rutinas corre
**primero** en `MainWindow.HandlePromptAsync`, y `MainWindow` **no reintenta** cuando
`FindBestMatch` no encuentra nada, decir *"Inicia un temporizador de 20 minutos"* responde
*"No encontré una rutina que coincida con..."* y **no arranca ningún temporizador**.
Afecta también a *"Inicia un descanso"* e *"Inicia un pomodoro"*.
`SpanishFocusCommandParserTests` no lo detecta porque prueba el parser **aislado**, sin la
precedencia real del shell. Es justamente el tipo de fallo que solo aparece al caracterizar la
composición.

**D2 — `OpenApplication` es ejecución arbitraria sin confirmación. (Alta, seguridad) — ✅ RESUELTO en 1.1.1**
`NexoAutomationActionExecutor.OpenApplication` reenvía `action.Arguments` al proceso, y
`AutomationPermissionPolicy` clasifica esa acción como `Reversible`, es decir **sin
confirmación**. Un paso de rutina con `Target="powershell.exe"` y `Arguments="-Command ..."`
ejecuta lo que sea sin el paso de aprobación que exige `SECURITY_MODEL` (escenario 22).
*Mitigación existente:* las rutinas las crea el propio usuario en la interfaz y esa creación es
la aprobación (`PRODUCT_VISION` §F). No es una vía de explotación remota. Pero el invariante
**no está aplicado técnicamente**, que es lo que `SECURITY_MODEL` §4 exige explícitamente.
En contraste, `OpenTerminal` **sí** es seguro: ignora `Arguments` y construye siempre su propia
línea de comandos. Esa asimetría queda congelada como invariante.

**D3 — `Load` no normaliza en las rutas de recuperación. (Media) — ABIERTO, no bloquea 1.2**
`JsonSettingsStore.Load` solo llama a `Normalize()` en la ruta de éxito. Con archivo ausente o
corrupto devuelve `new ShellPreferences()` con `SchemaVersion = 0`. Consecuencia: el siguiente
`Save` reejecuta **todas** las migraciones desde 0, incluida la de v10
(`HasCompletedOnboarding = false`), así que un valor asignado justo antes de guardar se pierde
en ese ciclo. Tras un archivo corrupto los valores por defecto son aceptables —los datos ya
eran ilegibles— pero el shell no puede marcar el onboarding como completado en ese arranque.
La degradación dura un solo ciclo.

**D4 — `SingleInstanceCoordinator.Dispose` no es idempotente. (Baja) — ✅ RESUELTO en 1.1.1**
Un segundo `Dispose` lanza `ObjectDisposedException` (`_cancellation.Cancel()` sobre un CTS ya
liberado). Hoy no se manifiesta porque `App.OnExit` pone el campo a `null`, pero un contenedor
de DI que libere de forma genérica —justo lo que llega en 1.2— sí puede sacarlo a la luz.

**D5 — La propiedad del mutex es por hilo, no por proceso. (Informativo) — ABIERTO, no bloquea 1.2**
Dos `SingleInstanceCoordinator` en el **mismo hilo** se consideran ambos primarios, porque el
segundo `WaitOne` es una adquisición recursiva del mismo dueño. En producción no ocurre —cada
instancia es un proceso distinto— pero quien extraiga o comparta este componente en 1.2 debe
saberlo. Queda congelado en `MutexOwnershipIsPerThread_NotPerProcess`.

---

## PLAN DE 1.2 EJECUTADO (histórico — ver "Fase 1.2" arriba para el resultado real)

> Esta sección documenta el plan **tal como se escribió antes de ejecutarlo**. El resultado real,
> con una desviación documentada (dónde vive el paquete DI y la clase de composición), está en la
> sección "Fase 1.2 — composition root + DI (2026-07-23)" más arriba. Se conserva sin editar por
> trazabilidad.

Fases 1.1 y 1.1.1 cerradas y **verdes**: 606 pruebas, 0 fallidas, 0 warnings, smoke test manual
completo sobre la aplicación real. La red de seguridad existe y los defectos que la propia red
destapó (D1, D2, D4) están corregidos.

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
6. ~~Resolver antes D4~~ ✅ ya resuelto en la fase 1.1.1.
7. Criterio de salida: build en Release con **0 warnings**, **606 pruebas verdes**, arranque
   real de la aplicación verificado, y `MainWindow` sin ningún `= new` de servicio en la
   declaración de campos.

Riesgo principal de 1.2: el orden de construcción. Hoy los campos se inicializan en orden de
declaración y luego el constructor cablea eventos. Un contenedor cambia *cuándo* se construye
cada servicio. Mitigación: registrar todo como singleton y resolver de forma **ansiosa** en el
mismo orden que hoy, antes de cablear eventos.

---

## SIGUIENTE PASO EXACTO

**Fase 1.3A parcial: coordinador aislado, MainWindow aún no migrado.** Ver la sección completa
"Fase 1.3A — VoiceCoordinator aislado" más abajo para el resultado exacto. 638 pruebas, 0
fallidas, 0 warnings. `MainWindow.xaml.cs` y `App.xaml.cs` **no se tocaron** en esta subfase.

El siguiente paso es **1.3B — migrar `MainWindow` a `VoiceCoordinator`**. No se ha iniciado ningún
trabajo de 1.3B. Antes de empezar, quien retome debe:

1. Releer la sección "Fase 1.3A" completa para conocer la API real de `VoiceCoordinator` y la
   corrección de propiedad (el coordinador **no libera** los tres servicios de voz).
2. Repetir el smoke test manual interactivo (riesgo #13, heredado de 1.2, aún abierto) antes de
   dar 1.3B por cerrada — es el paso de mayor riesgo funcional de toda la Fase 1, y esta subfase
   no tuvo forma de verificarlo interactivamente.
3. Decidir explícitamente, y documentarlo antes de tocar código, si `MainWindow.Window_Closed`
   pasa a delegar la liberación de los tres servicios de voz en `VoiceCoordinator.Dispose()` (lo
   que exigiría que el coordinador SÍ los libere, revirtiendo la corrección de propiedad de 1.3A)
   o si `MainWindow` sigue liberándolos directamente y `VoiceCoordinator` sigue sin ser su dueño
   de forma permanente. **No cambiar esto por inercia**: es una decisión de diseño, no un detalle.
4. Migrar método por método (dispositivo de entrada → push-to-talk → wake word runtime → modo
   prueba/aliases → puente con Resource Governor → disposición), en commits pequeños y
   compilables, siguiendo el plan detallado en `artifacts\Kohana-Fase-1.3-Auditoria.md`.

**No iniciar 1.4 sin que 1.3 (A y B) esté verde.**

---

### Fase 1.2 — inventario previo de los seis servicios (2026-07-23)

Registro exigido por el plan **antes** de tocar `MainWindow.xaml.cs`. Estado tal como existía en
el checkpoint `82a36fb`.

| # | Interfaz | Implementación actual | Lifetime | Orden de construcción (campo) | Eventos asociados | `IDisposable` |
|---|---|---|---|---|---|---|
| 1 | `IAiChatService` | `AiChatRouterService` | Singleton por ventana (una instancia durante toda la vida de `MainWindow`) | 8º inicializador de campo (tras `_settingsStore`, `_startupService`, `_conversationStore`, `_commandParser`, `_taskCommandParser`, `_focusCommandParser`, `_routineCommandParser`) | Ninguno suscrito por `MainWindow` | La interfaz **no** extiende `IDisposable`; la implementación concreta sí (`AiChatRouterService : IAiChatService, IDisposable`). `Window_Closed` comprueba `is IDisposable` antes de liberar |
| 2 | `IAudioMixerService` | `WindowsAudioMixerService` | Singleton por ventana | 9º inicializador de campo | Ninguno | Ni la interfaz ni la implementación son `IDisposable`. No se libera en `Window_Closed` |
| 3 | `IVoiceInputService` | `WhisperVoiceInputService` | Singleton por ventana | 10º inicializador de campo | Ninguno suscrito directamente (se consulta por métodos: `GetInputDevices`, `IsReady`, `StartListeningAsync`, etc.) | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` (línea ~808) |
| 4 | `IVoiceOutputService` | `WindowsTextToSpeechService` | Singleton por ventana | 11º inicializador de campo | Ninguno | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` (línea ~807) |
| 5 | `IWakeWordService` | `VoskWakeWordService` | Singleton por ventana | 12º inicializador de campo | `WakeWordDetected` y `RecognitionObserved`, suscritos en el constructor (líneas 191-192) y **desuscritos antes** de `Dispose()` en `Window_Closed` (líneas 800-802) | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` |
| 6 | `IScreenCaptureService` | `WindowsScreenCaptureService` | Singleton por ventana | 13º inicializador de campo (último de los seis) | Ninguno | Ni la interfaz ni la implementación son `IDisposable`. No se libera en `Window_Closed` |

**Consecuencia para el diseño de la fase:** de los seis, solo tres (`IVoiceInputService`,
`IVoiceOutputService`, `IWakeWordService`) tienen liberación explícita hoy en `Window_Closed`, y
`MainWindow` **ya** conoce y aplica ese contrato exacto (incluida la comprobación condicional de
`IAiChatService`). Cualquier contenedor de DI que también intente liberar estas instancias al
cerrarse causaría una doble liberación no probada. Decisión tomada para evitarlo: el contenedor
registra las **seis instancias ya construidas** (`services.AddSingleton<TInterface>(instancia)`),
no sus *tipos*. Un `ServiceProvider` de `Microsoft.Extensions.DependencyInjection` **no** libera
instancias que no creó él mismo — solo libera lo que construye a partir de un tipo o fábrica — así
que `Window_Closed` sigue siendo la única ruta que llama a `Dispose()` sobre estos seis servicios,
exactamente como hoy. El contenedor solo libera su propio `ServiceProvider`, no los servicios.

### Fase 1.2 — composition root + DI (2026-07-23) ✅

Objetivo cumplido **sin cambiar comportamiento observable**: `MainWindow` ya no fija ningún motor
con `new` en la declaración de campos para los seis servicios de interfaz.

**Paquete añadido:** `Microsoft.Extensions.DependencyInjection` **10.0.10**, licencia **MIT**
(`https://licenses.nuget.org/MIT`), autor Microsoft, verificada contra el `.nuspec` del paquete
restaurado. Misma familia de versión que el resto de paquetes `Microsoft.*` ya presentes en
`Nexo.Windows` (`System.Diagnostics.PerformanceCounter`, `System.Drawing.Common`, `System.Speech`,
todos en `10.0.10`).

**Dónde vive el paquete — desviación documentada del plan original:** el plan de
`SIGUIENTE PASO EXACTO` (arriba) proponía `Nexo.App/Composition/KohanaServiceCollection.cs`. Se
implementó en cambio como `Nexo.Windows/Composition/KohanaCompositionRoot.cs`, y el paquete se
referenció **solo en `Nexo.Windows`, no en `Nexo.App`**. Motivo: la fase 1.1 ya estableció (ver
más arriba, "Seams extraídos") que referenciar `Nexo.App` desde un proyecto de pruebas arrastra
`UseWPF` y provoca `MSB3277`, subiendo los warnings de 0 a 1 — inaceptable contra el baseline de 0
warnings. Igual que se hizo con `SingleInstanceCoordinator` en 1.1, la clase que hace el trabajo
real de composición vive en `Nexo.Windows` (testeable, sin `UseWPF`), y `Nexo.App/App.xaml.cs`
sigue siendo el **único punto de la aplicación** que instancia `KohanaCompositionRoot` — sigue
existiendo un único composition root y un único `ServiceProvider` para toda la vida del proceso;
solo cambió en qué proyecto vive la *clase* que lo implementa. `Nexo.App` no necesitó ninguna
referencia directa al paquete DI: solo consume las propiedades tipadas de
`KohanaCompositionRoot`, nunca `IServiceProvider`.

**Diseño de liberación (evita doble `Dispose`):** el contenedor registra las seis instancias ya
construidas (`AddSingleton<TInterface>(instancia)`), no los tipos. `Window_Closed` en
`MainWindow.xaml.cs` sigue siendo, sin ningún cambio, la única ruta que llama a `Dispose()` sobre
`IWakeWordService`, `IVoiceInputService`, `IVoiceOutputService` y condicionalmente `IAiChatService`.
`KohanaCompositionRoot.Dispose()` (llamado desde `App.OnExit`) solo libera el `ServiceProvider` en
sí — que, al no haber creado esas instancias, no vuelve a llamar `Dispose()` sobre ellas. Verificado
con la prueba `Dispose_DoesNotDisposeTheUnderlyingServiceInstances`.

**Archivos creados:**
- `src/Nexo.Windows/Composition/KohanaCompositionRoot.cs` — construye los seis servicios en el
  mismo orden relativo que los antiguos inicializadores de campo, los registra como instancia
  singleton y los resuelve de forma ansiosa desde el `ServiceProvider`.
- `tests/Nexo.Windows.Tests/Composition/KohanaCompositionRootTests.cs` — 6 pruebas.
- `tests/Nexo.Windows.Tests/Composition/CompositionInvariantTests.cs` — 3 pruebas.

**Archivos modificados:**
- `src/Nexo.Windows/Nexo.Windows.csproj` — añade el `PackageReference`.
- `src/Nexo.App/App.xaml.cs` — crea `_compositionRoot` en `OnStartup` (antes de `MainWindow`),
  pasa sus seis propiedades al constructor de `MainWindow`, y lo libera en `OnExit`.
- `src/Nexo.App/MainWindow.xaml.cs` — los seis campos de servicio pierden su inicializador `new`
  y se reciben por constructor (con valor por defecto `?? new Concreto()` solo para permitir la
  construcción sin argumentos que WPF usa en el diseñador; `App.OnStartup` siempre provee los seis
  explícitamente). Ningún otro campo, orden de suscripción de eventos ni firma pública cambió.

**Orden de construcción — verificado, no solo asumido:** los seis servicios ahora se construyen
en el cuerpo del constructor de `MainWindow` (antes de `_startHidden = startHidden` y de cualquier
uso, incluido `_routineRunner`/`_audioView` que dependen de `_audioMixerService`), lo que sigue
cumpliendo el invariante crítico: **construidos antes de cablear eventos**. Su posición relativa
frente a los demás inicializadores de campo (`_settingsStore`, `_homeView`, semáforos, etc.) sí
cambió — pasaron de intercalarse entre inicializadores de campo a construirse justo al principio
del cuerpo del constructor — pero se verificó que ninguno de los seis constructores concretos
(`AiChatRouterService`, `WindowsAudioMixerService`, `WhisperVoiceInputService`,
`WindowsTextToSpeechService`, `VoskWakeWordService`, `WindowsScreenCaptureService`) toca estado
compartido, estático o cualquiera de los demás campos: todos son `new()` independientes que solo
leen rutas de modelos o el dispositivo de audio por defecto. No hay acoplamiento observable que ese
cambio de orden pueda romper.

**Pruebas añadidas (9 nuevas en `Nexo.Windows.Tests`):**
- Resolución de los seis servicios con el tipo concreto esperado (motores actuales preservados).
- Las propiedades del composition root coinciden exactamente con lo que resuelve el `Provider`.
- Identidad singleton: resolver dos veces devuelve la misma instancia.
- Las seis instancias son mutuamente distintas (sin aliasing accidental).
- `Dispose()` no lanza y es idempotente.
- `Dispose()` del contenedor no libera las instancias subyacentes (evita doble `Dispose`).
- Invariante: `Nexo.Core.csproj` sigue sin ningún `PackageReference`.
- Invariante: `MainWindow.xaml.cs` no contiene la cadena `IServiceProvider`.
- Invariante: `MainWindow.xaml.cs` ya no construye los seis servicios con `= new ...()`.

Las pruebas de caracterización de 1.1/1.1.1 (`PromptDispatchCharacterizationTests`,
`LocalActionPermissionCharacterizationTests`, etc.) no se tocaron y siguen verdes sin
modificación: cubren que los comandos locales siguen resolviéndose sin LLM y que la precedencia de
parsers no cambió, algo ajeno a esta fase pero que confirma que el cambio de composición no la
afectó.

**Resultado de build y pruebas (Release, en frío tras cerrar una instancia de `Kohana.exe` que
bloqueaba `Nexo.Core.dll`/`Nexo.Windows.dll` — mismo tipo de incidencia ya documentada en el
baseline, PID distinto, cerrado con autorización explícita del propietario):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll →  39 superadas, 0 con error, 0 omitidas   (30 + 9 nuevas)
Total: 615 pruebas, 0 fallidas, 0 warnings.
```

**Smoke test (portable `bin\Release`, sin herramienta de automatización de UI de escritorio
disponible en esta sesión — limitado a lo verificable por proceso y logs):**

| Paso | Resultado |
|---|---|
| Arranque | ✅ `Kohana.exe` alcanza estado `Responding=True` en <4 s, sin `WerFault.exe` |
| Segunda instancia | ✅ el segundo proceso termina solo con código 0; el original sigue vivo — `SingleInstanceCoordinator` intacto |
| Cierre por PID exacto | ✅ `Stop-Process` sobre el PID iniciado por este agente; sin procesos huérfanos |
| Reapertura | ✅ nuevo proceso alcanza `Responding=True` normalmente |
| Runtime de IA administrado | ✅ `Logs\ollama-runtime.log` registra `ManagedRunning` en ambos arranques, sin errores — confirma que `ManagedOllamaSupervisor` y el `IAiChatService` resuelto por el contenedor siguen intercambiando datos correctamente |
| Navegación, comando local, temporizador, wake word, rutinas | ⚠️ **NO probado en esta sesión** — requiere control de UI de escritorio (clics, teclado, micrófono) que esta sesión no tiene disponible. La fase 1.1 ya caracterizó y congeló este comportamiento en pruebas automatizadas (`PromptDispatchCharacterizationTests`, `VoiceRuntimeCharacterizationTests`), que siguen verdes sin modificación, pero eso **no sustituye** una verificación manual interactiva |

**Riesgo residual explícito:** el smoke test manual interactivo completo (navegación por clics,
orden de voz, wake word en vivo) que sí se hizo en la fase 1.1.1 **no se repitió** en 1.2 por falta
de herramienta de automatización de UI en esta sesión. Mitigación: las 615 pruebas automatizadas
cubren la lógica pura y la composición; el arranque real confirma que el grafo de objetos se
construye sin excepciones. Recomendado repetir el smoke test manual completo de 1.1.1 antes de
considerar este checkpoint apto para uso diario, no solo para verificación técnica.

---

### Fase 1.3A — `VoiceCoordinator` aislado (2026-07-23)

Subfase aprobada tras revisar `artifacts\Kohana-Fase-1.3-Auditoria.md`, con una corrección
obligatoria sobre propiedad de recursos respecto a lo que proponía esa auditoría: en 1.3A,
`VoiceCoordinator` **no es dueño** del ciclo de vida de los tres servicios de voz. Objetivo
cumplido: el coordinador existe, está probado de forma aislada y está conectado al composition
root, pero **`MainWindow.xaml.cs` y `App.xaml.cs` no se tocaron**.

**Corrección de propiedad aplicada:** `VoiceCoordinator.Dispose()` libera únicamente los dos
`SemaphoreSlim` que el propio coordinador crea (`_voiceGate`, `_wakeWordGate`). No llama
`Dispose()` sobre `IVoiceInputService`, `IVoiceOutputService` ni `IWakeWordService` en ningún
punto de su código — verificado tanto por prueba de comportamiento
(`Dispose_DoesNotDisposeTheUnderlyingServiceInstances` en `KohanaCompositionRootTests` y
`Dispose_DoesNotDisposeTheUnderlyingServices` en `VoiceCoordinatorTests`) como por prueba
estructural sobre el código fuente (`VoiceCoordinator_DoesNotDisposeTheThreeInjectedServices`).
`MainWindow.Window_Closed` sigue siendo, sin ningún cambio, la única ruta que libera esos tres
servicios, en el mismo orden que fijó la fase 1.2 — verificado por
`MainWindow_StillOwnsTheThreeVoiceServicesAndTheirDisposalOrder`, que localiza el bloque de
liberación en el código fuente y confirma el orden: desuscribir `WakeWordDetected` → desuscribir
`RecognitionObserved` → `_wakeWordService.Dispose()` → `_aiChatService` condicional →
`_voiceOutputService.Dispose()` → `_voiceInputService.Dispose()`.

**API real de `VoiceCoordinator`** (`src/Nexo.Windows/Voice/VoiceCoordinator.cs`):
- Constructor: `(IVoiceInputService, IVoiceOutputService, IWakeWordService)` — sin valores por
  defecto; `null` lanza `ArgumentNullException` en vez de construir un motor de reemplazo.
- Eventos `WakeWordDetected` y `RecognitionObserved`: **accessors de paso directo**
  (`add => _wakeWordService.WakeWordDetected += value;`), no una suscripción interna propia. No
  hay nada que desuscribir en `Dispose()` para estos dos eventos.
- Solo lectura: `IsVoiceInputReady`, `IsVoiceInputListening`, `IsWakeWordReady`, `IsWakeWordListening`.
- Paso a través de configuración (sin conocer preferencias): `InputDeviceNumber` (aplica a los dos
  servicios), `WakeWordSensitivity`, `WakeWordCustomAliases`.
- `GetInputDevices()`, `PrepareVoiceInputAsync`, `PrepareWakeWordAsync`.
- `StartPushToTalkAsync`, `StopPushToTalkAsync`, `CancelPushToTalkAsync`.
- `StartWakeWordAsync(phrase, ct)`, `StopWakeWordAsync()`, `PauseWakeWordAsync()` (alias semántico
  de `StopWakeWordAsync`: no existe una operación distinta de "pausar" en los servicios
  subyacentes).
- `ListenAfterWakeWordAsync(maximumDuration, trailingSilence, preRoll, postWake, ct)`.
- `Speak(text)`, `StopSpeaking()`.

**Decisión deliberada — no auto-preparación:** `StartPushToTalkAsync` y
`ListenAfterWakeWordAsync` **no** llaman `PrepareVoiceInputAsync` internamente antes de escuchar,
a diferencia de las ramas equivalentes en `MainWindow` hoy. Se verificó en
`WindowsVoiceInputService.StartListeningAsync` (línea 226) que el propio servicio **ya** se
autoprepara si `!IsReady`; la comprobación manual en `MainWindow` solo existe para mostrar texto de
progreso en la UI antes de la llamada. El coordinador expone `PrepareVoiceInputAsync` como
operación independiente para que quien lo use (1.3B) decida si quiere ese texto de progreso, sin
duplicar lógica que el servicio ya garantiza.

**Estrategia de candados:** `_voiceGate` se adquiere **siempre antes** que `_wakeWordGate` cuando
una operación necesita ambos (`StartPushToTalkAsync` y `ListenAfterWakeWordAsync` adquieren
`_voiceGate` y, dentro de su bloque, `StopWakeWordCoreAsync` adquiere `_wakeWordGate`). Las
operaciones que solo tocan wake word (`StartWakeWordAsync`, `StopWakeWordAsync`,
`PauseWakeWordAsync`) **nunca** tocan `_voiceGate`, así que no existe ninguna ruta que adquiera
`_wakeWordGate` primero y `_voiceGate` después — se eliminó estructuralmente la posibilidad de
interbloqueo por orden invertido. Cada `WaitAsync()` está seguido de un `try/finally` que libera
exactamente el candado que adquirió esa misma llamada; ningún camino libera un semáforo que no
adquirió. Verificado con `ConcurrentStartWakeWordAndPushToTalk_DoNotDeadlock` (con límite de
tiempo del propio `[Fact(Timeout = 5000)]`, sin `Task.Delay`) y
`TwoSimultaneousPushToTalkCalls_NeverOverlapInTheUnderlyingService` (contador de concurrencia
máxima observada, sin sleeps).

**Archivos creados:**
- `src/Nexo.Windows/Voice/VoiceCoordinator.cs`.
- `tests/Nexo.Windows.Tests/Voice/VoiceCoordinatorFakes.cs` — dobles de prueba controlables
  (`FakeVoiceInputService`, `FakeVoiceOutputService`, `FakeWakeWordService`) con ganchos
  (`BeforeStartListeningReturns`, `BeforeStopListeningReturns`) para forzar suspensión real en
  pruebas de concurrencia/cancelación sin `Task.Delay`, y un `VoiceCallLog` compartido para
  afirmar orden relativo de llamadas entre los tres dobles.
- `tests/Nexo.Windows.Tests/Voice/VoiceCoordinatorTests.cs` — 17 pruebas.

**Archivos modificados:**
- `src/Nexo.Windows/Composition/KohanaCompositionRoot.cs` — construye **una sola** instancia de
  `VoiceCoordinator` envolviendo exactamente las mismas tres instancias de voz que ya construía
  (no un cuarto motor), la registra como instancia singleton en el mismo `ServiceProvider`, y la
  expone como propiedad `VoiceCoordinator`. `Dispose()` del composition root ahora también libera
  `VoiceCoordinator` (sus dos semáforos propios) antes de liberar el `Provider` — no libera los
  seis servicios de voz/IA/etc., que siguen sin ser de su propiedad, exactamente como en 1.2.
- `tests/Nexo.Windows.Tests/Composition/KohanaCompositionRootTests.cs` — +4 pruebas: instancia
  única del coordinador, identidad de sus tres servicios verificada por comportamiento (no hay
  forma de exponer los campos internos sin romper la superficie mínima a propósito), ausencia de
  un cuarto conjunto de motores registrados, y que `Dispose()` del root libera los recursos
  propios del coordinador sin tocar los tres servicios de voz.
- `tests/Nexo.Windows.Tests/Composition/CompositionInvariantTests.cs` — +4 pruebas: el código
  fuente de `VoiceCoordinator` no menciona WPF/`Dispatcher`/la vista principal/preferencias/
  decisión del gobernador de recursos/`IServiceProvider`; no llama `Dispose()` sobre los tres
  servicios; y `MainWindow` conserva tanto los tres parámetros de constructor de voz como el
  orden exacto de liberación en `Window_Closed`.

**`MainWindow.xaml.cs` y `App.xaml.cs`: sin cambios en esta subfase.** Confirmado por
`git diff --stat`, que solo lista los cinco archivos de arriba.

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  62 superadas, 0 con error, 0 omitidas   (39 + 23 nuevas)
Total: 638 pruebas, 0 fallidas, 0 warnings.
```

Dos correcciones de prueba durante la implementación (documentadas por disciplina, no ocultadas):
`Assert.ThrowsAsync<OperationCanceledException>` no acepta la subclase `TaskCanceledException`
que realmente lanza `SemaphoreSlim.WaitAsync` cancelado — se cambió a `ThrowsAnyAsync`; y
`VoskWakeWordService.CustomAliases` devuelve una copia defensiva en cada lectura, así que la
prueba de identidad de aliases compara contenido (`Assert.Equal`) en vez de referencia
(`Assert.Same`).

**No se generó portable en esta subfase**, según instrucción explícita.

**Riesgos pendientes para la migración 1.3B:** ver la sección "Fase 1.3A" de este mismo bloque —
en particular, la decisión aún no tomada sobre quién libera los tres servicios de voz tras 1.3B
(punto 3 de "Siguiente paso exacto"), y el smoke test manual interactivo, todavía sin repetir
desde la fase 1.1.1.
