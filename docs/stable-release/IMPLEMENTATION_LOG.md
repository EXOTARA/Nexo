# Registro de implementación — Kohana 1.0

> **Este archivo es la memoria persistente del proyecto.**
> Todo agente que retome el trabajo debe leerlo **primero** y actualizarlo **antes** de quedarse sin
> contexto. Las decisiones registradas aquí y en `PRODUCT_VISION.md` **no vuelven a preguntarse**.

---

## ESTADO ACTUAL

| Campo | Valor |
|---|---|
| **Fase actual** | Fase 0 — **completada parcialmente** |
| **Siguiente fase** | Fase 1, paso 1.1 (pruebas de caracterización) |
| **Rama** | `release/kohana-1.0-rc` — **pendiente de crear en Windows** |
| **Versión base** | 0.9.5-beta |
| **Última actualización** | 2026-07-22 |
| **Bloqueador activo** | Baseline no medido (ver ⚠️ abajo) |

### ⚠️ Bloqueador: baseline sin medir

La Fase 0 se ejecutó en un entorno **Linux sin SDK de .NET, sin micrófono y sin GPU**. Conforme a la
decisión A (*"no inventar benchmarks en entornos sin Windows, micrófono o GPU accesibles"*), los
siguientes campos quedan **vacíos a propósito** y deben completarse desde Windows antes de iniciar
la Fase 1:

```
[ ] Sistema operativo:        ____________________
[ ] Ruta del repositorio:     ____________________
[ ] Rama:                     ____________________
[ ] dotnet --info (SDK):      ____________________
[ ] git status (limpio?):     ____________________
[ ] Commit inicial (hash):    ____________________
[ ] Total de pruebas:         ____________________
[ ] Pruebas fallidas:         ____________________
[ ] Warnings de compilación:  ____________________
[ ] Tiempo de restore:        ____________________
[ ] Tiempo de build Release:  ____________________
[ ] Tiempo de test:           ____________________
[ ] Tamaño del portable:      ____________________
[ ] Tamaño del instalador:    ____________________
[ ] SHA-256 del portable:     ____________________
```

Referencia esperada (de la auditoría estática, **a confirmar**): 196 atributos `[Fact]`/`[Theory]`
en 48 archivos de `tests/Nexo.Core.Tests`.

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

### Fase 0 — cierre (en Windows)
- [ ] `git checkout -b release/kohana-1.0-rc`
- [ ] Confirmar SO, ruta, rama, `dotnet --info`, `git status`
- [ ] `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`
- [ ] Rellenar el bloque de baseline de arriba
- [ ] Commit: `docs(stable-release): baseline y documentos de la versión estable`

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
| Baseline | *pendiente* | — | — | Medir en Windows |

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

En el repositorio local de Windows:

```powershell
cd <ruta-del-repo>
git status                       # debe estar limpio
git checkout -b release/kohana-1.0-rc
dotnet --info
dotnet restore .\Nexo.slnx
dotnet build   .\Nexo.slnx -c Release
dotnet test    .\Nexo.slnx -c Release
```

Luego: rellenar el bloque de baseline de este archivo, commitear los documentos de Fase 0, y
**empezar por el paso 1.1** — pruebas de caracterización de `MainWindow.xaml.cs`.

**No iniciar 1.2 (DI) sin que 1.1 esté verde.** La caracterización es la única red de seguridad
para el resto de la extracción.
