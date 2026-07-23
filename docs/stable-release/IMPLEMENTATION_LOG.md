# Registro de implementación — Kohana 1.0

> **Este archivo es la memoria persistente del proyecto.**
> Todo agente que retome el trabajo debe leerlo **primero** y actualizarlo **antes** de quedarse sin
> contexto. Las decisiones registradas aquí y en `PRODUCT_VISION.md` **no vuelven a preguntarse**.

---

## ESTADO ACTUAL

| Campo | Valor |
|---|---|
| **Fase actual** | Fase 0 — **completada** (baseline medido) |
| **Siguiente fase** | Fase 1, paso 1.1 (pruebas de caracterización) |
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

## TAREAS PENDIENTES

### Fase 0 — cierre (en Windows) ✅ 2026-07-23
- [x] `git checkout -b release/kohana-1.0-rc`
- [x] Confirmar SO, ruta, rama, `dotnet --info`, `git status`
- [x] `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`
- [x] Rellenar el bloque de baseline de arriba
- [x] Corregir cifras estáticas de `CURRENT_STATE_AUDIT.md` contra el código real
- [x] Commit: `docs: record measured Kohana 1.0 baseline`

### Fase 1 — extracción (7 pasos, en orden)
- [ ] 1.1 Pruebas de caracterización de `MainWindow`
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

---

## SIGUIENTE PASO EXACTO

Fase 0 cerrada con baseline medido. El siguiente paso es **1.1 — pruebas de caracterización**:
congelar en pruebas la conducta actual observable antes de mover una sola línea de
`MainWindow.xaml.cs`.

Regla de la fase: extraer **seams** (lógica pura ya comprobable o extraíble sin cambiar conducta) y
probarlos. **Prohibido** probar WPF con aserciones frágiles sobre píxeles, layout o temporización de
animación.

**No iniciar 1.2 (DI) sin que 1.1 esté verde.** La caracterización es la única red de seguridad
para el resto de la extracción.
