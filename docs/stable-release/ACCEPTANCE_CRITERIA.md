# Criterios de aceptación — Kohana 1.0 / RC

## Cómo leer este documento

Los presupuestos de latencia de §2 son **objetivos propuestos, no medidos**. Se calibran contra el
baseline real registrado en `IMPLEMENTATION_LOG.md` durante la Fase 0 en Windows. Hasta entonces
están marcados `PENDIENTE DE CALIBRAR`. **Está prohibido reportarlos como logrados sin medición.**

## 1. Criterios de salida para RC (todos obligatorios)

| # | Criterio | Verificación |
|---|---|---|
| 1 | Compila en Release sin errores | `dotnet build -c Release` |
| 2 | Todas las pruebas aprobadas pasan | `dotnet test -c Release` |
| 3 | No existen fallas críticas conocidas | `KNOWN_LIMITATIONS.md` |
| 4 | No se pierden datos en actualización | Test de migración |
| 5 | La migración desde Nexo funciona | Test de migración |
| 6 | El instalador funciona | Verificación manual + `verify-release.ps1` |
| 7 | El portable funciona | Verificación manual |
| 8 | El onboarding funciona sin terminal | Escenario D3 |
| 9 | Comandos locales básicos **actúan** (no explican) | Escenario D1 |
| 10 | La privacidad de Vision funciona | Test de privacidad |
| 11 | El Runtime se recupera de fallos | Escenarios de recuperación |
| 12 | La segunda instancia funciona | Escenario dedicado |
| 13 | El Resource Governor funciona | Test + modo juego |
| 14 | El perfil de hardware funciona | Test en ≥2 equipos distintos |
| 15 | La interfaz es coherente | Revisión visual |
| 16 | Los iconos son finales o aprobados | Revisión visual |
| 17 | La actividad local/remota es visible | Pantalla Actividad y privacidad |
| 18 | La memoria es controlable | Los 9 comandos de §G |
| 19 | Las funciones experimentales están marcadas | Revisión de UI |
| 20 | Las limitaciones están documentadas | `KNOWN_LIMITATIONS.md` |

## 2. Presupuestos de latencia `PENDIENTE DE CALIBRAR`

La velocidad es la prioridad #1, así que debe ser **medible y asertable**, no aspiracional.
Propuesta inicial a validar contra baseline:

| Ruta | Objetivo propuesto | Se mide desde → hasta |
|---|---|---|
| Wake word → escuchando | ≤ 300 ms | Fin de la frase → micrófono entregado |
| Comando local completo | ≤ 500 ms | Fin del habla → acción ejecutada |
| Primer audio de respuesta (TTS) | ≤ 700 ms | Primer token → primer sonido |
| Barge-in (corte de voz) | ≤ 150 ms | Voz del usuario detectada → silencio |
| Apertura de paleta | ≤ 100 ms | `Ctrl+Espacio` → primer fotograma nítido |
| Arranque en frío del Hub | ≤ 2 s | Proceso iniciado → interfaz utilizable |

Una vez calibrados, estos objetivos se convierten en pruebas que **fallan** si se exceden.

## 3. Las tres experiencias del día 1 (decisión D)

### D1 — "Oye Kohana, abre X"
- [ ] Se activa con `Oye Kohana` y con `Kohana`
- [ ] **No** invoca el LLM (verificable: sin llamada al proveedor en el log)
- [ ] Ejecuta la acción real
- [ ] Confirma brevemente
- [ ] Funciona sin conexión a internet
- [ ] Funciona con el proveedor de IA apagado
- [ ] Falla de forma comprensible si la app no existe

### D2 — "Oye Kohana, ¿qué es esto?"
- [ ] Captura la ventana activa bajo demanda
- [ ] Aplica la política de privacidad antes de procesar
- [ ] Muestra evidencia de lo que detectó
- [ ] Responde por voz
- [ ] **La voz es interrumpible** (barge-in)
- [ ] La imagen se descarta al terminar
- [ ] Avisa visiblemente si la imagen sale a un proveedor remoto
- [ ] Se niega correctamente ante una ventana sensible

### D3 — Instalación limpia
- [ ] Instalar → onboarding → detección de hardware → configuración recomendada → primera orden
- [ ] **Cero uso de terminal**
- [ ] Cero conocimiento previo requerido
- [ ] Descarga de modelos solo con consentimiento explícito
- [ ] Funciona en cuenta de Windows recién creada

## 4. Criterios por subsistema

**Wake word:** `Oye Kohana` y `Kohana` estables y calibrados **por separado**;
`Ey/Hey Kohana` marcados experimentales; falso positivo y falso negativo medidos con pronunciación
mexicana; funciona tras suspensión/reanudación; se pausa en modo juego.

**Voz de salida:** streaming por oración; detención inmediata; barge-in; Kohana no se escucha a sí
misma; SAPI disponible como fallback de emergencia.

**Perfil de hardware:** detecta los 13 campos declarados; clasifica en Ligero/Equilibrado/Acelerado;
respeta la elección manual del usuario; **nunca** descarga modelos sin consentimiento.

**Agencia:** ningún comando conocido llega al LLM; abrir PowerShell no pide confirmación; ejecutar un
comando dentro de PowerShell **sí** la pide; toda acción sensible queda registrada.

**Memoria:** nada se escribe sin aprobación; los 9 comandos funcionan; los archivos son legibles y
editables; la sesión privada no deja rastro.

**Accesibilidad:** navegación completa por teclado; `AutomationProperties` en controles interactivos;
respeta las preferencias de movimiento reducido del sistema; contraste verificado.
