# Roadmap tecnológico de Kohana

> Diseño D3.2. Fases reales, no aspiracionales: una fase solo se marca "Implementada" si el código
> correspondiente existe y tiene pruebas en `release/kohana-1.0-rc` hoy. Infraestructura parcial se
> marca "Parcial", nunca "Implementada". Ver el detalle capacidad por capacidad en
> `docs/roadmap/KOHANA_CAPABILITY_MATRIX.md` y la arquitectura de capas en
> `docs/architecture/KOHANA_CAPABILITY_ARCHITECTURE.md`.

## Fase 0 — Stable Shell and Daily Core

**Estado: Implementada** (Diseños D1, D1.1, D2, D3, D3.1, D3.2).

- Objetivo: una base estable de escritorio — shell, navegación, voz, motores y flujo diario —
  antes de construir cualquier superficie ambiental encima.
- Valor: Kohana es usable y confiable hoy mismo, sin depender de fases futuras.
- Dependencias: ninguna (es la base).
- Tecnologías: WPF (.NET 10), Whisper (voz→texto), Vosk (palabra de activación), SAPI
  (texto→voz), `IHardwareCapabilityService`, `IAdaptiveEngineRegistry`.
- Permisos: ninguno más allá de lo que Windows concede a una app de escritorio estándar; micrófono
  bajo consentimiento del sistema operativo.
- Riesgos: ninguno nuevo — es la superficie más probada del proyecto (804 pruebas en D1.1, 1036+
  tras D3.2).
- No objetivos: nada ambiental, nada de visión de pantalla, nada de automatización de acciones.
- Criterio de terminado: cumplido — Hardware Capability Profile, Engine Registry inicial, voz base,
  Sakura Shell, navegación, personalización, Command Center, Daily Flow, Focus Continuity y
  perfiles aislados de validación (Diseño D3.2) están todos implementados y probados.
- Sprints que la componen: D1, D1.1, D2 (Sakura Command Center), D3 (Sakura Daily Flow), D3.1
  (Focus Continuity), D3.2 (este sprint: aprobación, integración, aislamiento, visión y roadmap).

## Fase 1 — Ambient Interaction Foundation

**Estado: Implementada** (Diseño D4 — D4.1 + D4.2 + D4.4 —, validado manualmente por el usuario e
integrado en `release/kohana-1.0-rc` — ver `docs/stable-release/IMPLEMENTATION_LOG.md`, sección
"Diseño D4").

- Objetivo: que Kohana pueda responder brevemente sin que el usuario tenga que abrir ni enfocar la
  ventana principal.
- Valor: reduce la fricción de cambiar de contexto para interacciones cortas — la primera pieza
  real de la visión "ambiental".
- Dependencias: Fase 0 completa (Command Center como origen de acciones, Engine Registry para
  saber qué motores están disponibles).
- Tecnologías: ventanas WPF no activables (`WS_EX_NOACTIVATE` / `ShowActivated=false`, ya usado en
  `HiddenWindowHost` de las pruebas y en el patrón de captura sin foco), un host de overlay nuevo.
- Permisos: ninguno nuevo más allá de los ya usados por voz y Command Center.
- Riesgos: una ventana que "no roba foco" mal implementada puede robar foco de todas formas en
  casos límite (multi-monitor, DPI mixto) — requiere validación interactiva real, no solo pruebas
  unitarias.
- No objetivos: nada de captura de pantalla, OCR ni control de otras aplicaciones (eso es Fase 2 en
  adelante).
- Criterio de terminado: Sakura Pill Host visible, ciclo de vida de una solicitud (Escuchando →
  Pensando → Resultado) con estados observables, resultado corto con expansión opcional, cancelar,
  deshacer cuando aplica, historial de solicitudes, primitivas de permisos, Context Snapshot de la
  ventana activa, integración inicial con Command Center.
- Sprint sugerido: **D4 — Ambient Interaction Foundation** (alcance detallado en la Sección 14).

## Fase 2 — Kohana Lens

**Estado: Parcial** (D5.1 migración de TFM + D5.2 servicio de OCR real implementados y probados en
`design/kohana-lens-v1`, sin integrar a `release/kohana-1.0-rc` — ver
`docs/stable-release/IMPLEMENTATION_LOG.md`, sección "Diseño D5". Falta todo lo demás: UI
Automation, consentimiento visible, redacción, integración con IA, resaltado visual, los tres
modos).

- Objetivo: que Kohana pueda observar y explicar lo que hay en pantalla (con autorización), no
  actuar sobre ello todavía.
- Valor: soporte técnico, estudio y desarrollo asistido por contexto visual real, sin depender de
  que el usuario describa lo que ve.
- Dependencias: Fase 1 (superficies ambientales para mostrar resultados sin robar foco).
- Tecnologías: UI Automation, captura de pantalla autorizada (ya existe `IScreenCaptureService`
  como base), OCR, un modelo visual (VLM), análisis de región.
- Permisos: captura de pantalla y ventana activa requieren consentimiento explícito y visible (ver
  estado "Mirando" en el modelo de confianza).
- Riesgos: exposición accidental de información sensible visible en pantalla; requiere exclusiones
  y redacción antes de cualquier envío a un proveedor externo.
- No objetivos: control automático de otras aplicaciones — la primera versión observa y guía.
- Criterio de terminado: modo soporte, modo estudio y modo desarrollo funcionando sobre la ventana
  activa, con resaltados y guía visual, sin ninguna acción automática sobre terceros.
- Sprints sugeridos: al menos dos — "Lens: captura y OCR" y "Lens: guía visual y modos".

## Fase 3 — Kohana Flow

**Estado: Planeada — investigación.**

- Objetivo: dictado global de alta calidad en cualquier aplicación de Windows.
- Valor: reemplaza el cambio de ventana para escribir texto largo por voz.
- Dependencias: Fase 1 (Voice Bar como superficie ambiental).
- Tecnologías: push-to-talk global, Whisper (ya integrado), puntuación y eliminación de muletillas,
  diccionario personalizado, snippets, inserción universal de texto.
- Permisos: acceso a micrófono global (fuera del foco de Kohana) y a la inserción de texto en la
  aplicación activa.
- Riesgos: inserción de texto en el lugar equivocado si el foco cambia durante el dictado.
- No objetivos: transformación de código o edición de proyectos (eso es Fase 5).
- Criterio de terminado: modo texto, correo y código funcionando con inserción universal confiable.
- Sprint sugerido: "Flow: dictado global v1".

## Fase 4 — Adaptive Computer Optimization

**Estado: Planeada — investigación.**

- Objetivo: que el usuario pueda pedir "optimiza mi computadora para X" y Kohana proponga y aplique
  (con confirmación) cambios reversibles basados en el hardware real.
- Valor: convierte el ya existente `HardwareCapabilityProfile` y `AdaptiveEnginePolicy` —hoy usados
  solo para elegir motores propios— en una capacidad que beneficia a todo el equipo, no solo a
  Kohana.
- Dependencias: Fase 0 (Hardware Capability Profile, Engine Registry como base del Capability
  Router).
- Tecnologías: perfiles de optimización, plan de cambios con simulación previa, snapshots del
  estado anterior, reversión y restauración automática, auditoría.
- Permisos: cambios de configuración del sistema — nivel de riesgo alto, requiere confirmación
  explícita y snapshot previo obligatorio (ver modelo de confianza).
- Riesgos: un cambio de sistema mal revertido puede dejar el equipo en peor estado — el snapshot y
  la reversión no son opcionales, son requisito de diseño.
- No objetivos: no es una lista genérica de "tweaks" de internet — cada cambio debe justificarse
  con una medición real del equipo.
- Criterio de terminado: los siete comandos objetivo (jugar, programar, edición de video,
  videollamada, batería, general, restaurar) funcionando con plan, confirmación, aplicación y
  reversión completa verificada.
- Sprints sugeridos: "Optimización: snapshots y reversión", "Optimización: perfiles por escenario".

## Fase 5 — Project Companion

**Estado: Planeada — investigación.**

- Objetivo: que Kohana trabaje junto al usuario dentro de un proyecto de código autorizado —desde
  guiar hasta ejecutar cambios— con el nivel de autonomía que el usuario elija.
- Valor: acompaña tareas de desarrollo reales (archivos, terminal, Git, pruebas) sin sustituir al
  IDE.
- Dependencias: Fase 2 (relación entre pantalla, terminal y código) y el modelo de confianza
  completo (los cinco modos de autonomía: Guía, Observador, Copiloto, Colaborador, Agente).
- Tecnologías: workspace autorizado, búsqueda y edición de archivos, diffs, terminal, build,
  pruebas, Git, checkpoints, detección de secretos.
- Permisos: acceso de lectura/escritura a un workspace explícitamente autorizado, nunca a todo el
  disco.
- Riesgos: ejecución de comandos irreversibles (borrado, force-push) — requiere los mismos
  principios de snapshot/reversión que la Fase 4.
- No objetivos: no reemplaza revisión humana de cambios significativos por defecto.
- Criterio de terminado: los cinco modos operando con checkpoints y detección de secretos activa
  antes de cualquier acción de escritura.
- Sprints sugeridos: "Companion: workspace y modo Guía", "Companion: modo Agente y checkpoints".

## Fase 6 — Context and Memory

**Estado: Planeada — investigación.**

- Objetivo: que Kohana recuerde contexto relevante entre sesiones sin convertirse en vigilancia
  permanente.
- Valor: continuidad real (p. ej. retomar una tarea de ayer) sin que el usuario tenga que repetir
  contexto.
- Dependencias: Fases 1–5 como fuentes de contexto a recordar.
- Tecnologías: historial, memoria de proyecto, timeline visual opcional, búsqueda semántica.
- Permisos: retención de datos — requiere controles de retención y redacción de datos sensibles
  visibles y accesibles al usuario.
- Riesgos: acumulación silenciosa de datos sensibles si la retención no tiene límites claros.
- No objetivos: nunca activada por defecto.
- Criterio de terminado: controles de exclusión y retención funcionando antes de que exista
  cualquier almacenamiento de memoria de facto.
- Sprint sugerido: "Memoria: retención y exclusiones antes que almacenamiento".

## Fase 7 — Safe Computer Use

**Estado: Planeada — investigación.**

- Objetivo: permitir que Kohana ejecute acciones reales sobre el equipo, siempre por el camino más
  seguro disponible primero.
- Valor: cierra el círculo entre "observar" (Lens) y "actuar" con el menor riesgo posible en cada
  paso.
- Dependencias: modelo de confianza completo (Fase 0), Fase 2 para saber qué hay en pantalla.
- Tecnologías, en orden de preferencia: API oficial → Windows App Actions → MCP → integración
  nativa → UI Automation → shell seguro → portapapeles → mouse/teclado simulados como último
  recurso.
- Permisos: el más alto del roadmap — requiere el Permission Broker y el Audit Log completos antes
  de habilitarse.
- Riesgos: automatizar mouse/teclado es frágil e inseguro si se usa como primera opción en vez de
  último recurso — de ahí el orden estricto.
- No objetivos: no se salta niveles del modelo de autonomía (Ver → Guiar → Proponer → Ejecutar un
  paso → Colaborar con confirmaciones → Automatizar una secuencia autorizada).
- Criterio de terminado: los seis niveles de autonomía disponibles y auditables para al menos una
  herramienta de cada categoría de la lista de preferencia.
- Sprints sugeridos: "Computer Use: niveles Ver/Guiar/Proponer", "Computer Use: ejecución y
  auditoría".

## Fase 8 — Skills Platform

**Estado: Planeada — investigación.**

- Objetivo: empaquetar combinaciones de capacidades anteriores en "packs" con propósito claro.
- Valor: un usuario no técnico puede activar "Kohana Study" sin entender qué capacidades incluye.
- Dependencias: Fases 1–7 (los packs combinan capacidades ya existentes, no inventan nuevas).
- Tecnologías: sistema de packs (Dev, Study, Support, Creator, Access, Meeting) sobre la misma
  arquitectura de capacidades.
- Permisos: cada pack hereda los permisos de las capacidades que combina — no introduce
  excepciones.
- Riesgos: fragmentación si cada pack termina con su propia lógica en vez de reutilizar capacidades
  comunes.
- No objetivos: no es un marketplace de terceros en esta fase.
- Criterio de terminado: al menos dos packs completos usando exclusivamente capacidades ya
  implementadas en fases anteriores.
- Sprints sugeridos: uno por pack, empezando por "Skills: Kohana Study" y "Skills: Kohana Dev".

## Fase 9 — Productization

**Estado: Planeada — investigación.**

- Objetivo: llevar Kohana de "build interno validado por el usuario" a producto distribuible.
- Valor: onboarding, actualización y soporte reales para usuarios que no son parte del desarrollo.
- Dependencias: todas las anteriores en la medida en que definan qué se onboardea/actualiza.
- Tecnologías: onboarding (ya existe una versión inicial — `OnboardingWindow`), comprobación de
  hardware (ya existe `IHardwareCapabilityService`), selección y descarga de modelos, permisos,
  instalador, actualizador, recuperación, diagnóstico, exportación de soporte, distribución.
- Permisos: instalación y actualización requieren los permisos de sistema habituales de un
  instalador de Windows.
- Riesgos: un actualizador mal diseñado puede romper instalaciones existentes — requiere el mismo
  rigor de reversibilidad que la Fase 4.
- No objetivos: no incluye tiendas de terceros ni distribución fuera de los canales que el usuario
  apruebe.
- Criterio de terminado: instalación, actualización y desinstalación limpias verificadas de punta a
  punta, con diagnóstico exportable para soporte.
- Sprints sugeridos: "Productization: instalador y actualizador", "Productization: diagnóstico y
  soporte".

---

## Fase recomendada a continuación (Sección 14 del encargo D3.2)

## D4 — Ambient Interaction Foundation

Sprint grande recomendado inmediatamente después de Diseño D3.2. Implementa la Fase 1 completa.

**Alcance previsto:**

- Sakura Pill Host.
- Ventanas no activables que no roban foco.
- Request lifecycle (ciclo de vida de una solicitud).
- Estados de request (Escuchando / Pensando / Resultado).
- Resultado corto y expandible.
- Una o dos acciones rápidas por resultado.
- Cancelar.
- Deshacer cuando aplique.
- Request History (historial de solicitudes).
- Audit básico.
- Permission primitives (primitivas de permisos, no el Permission Broker completo de la Fase 7).
- Context Snapshot de la ventana activa.
- Integración inicial con Command Center.

**Explícitamente fuera de alcance para D4:** Lens completa (Fase 2), Flow (Fase 3) y Computer Use
(Fase 7) — D4 sienta las bases ambientales; esas tres fases se construyen encima, no dentro de D4.

**Estado real (actualizado tras D4.1 + D4.2 + D4.4):** Sakura Pill Host, ventanas no activables,
ciclo de vida de solicitud, resultado corto/expandible, cancelar, deshacer (tanto en la solicitud
visible como en el historial), primitivas de permisos, Context Snapshot, historial de solicitudes
visible e integración inicial con el Command Center están implementados, probados, validados
manualmente por el usuario e integrados en `release/kohana-1.0-rc` — ver
`docs/stable-release/IMPLEMENTATION_LOG.md`, sección "Diseño D4".
