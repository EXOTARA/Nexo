# Matriz de pruebas — Kohana 1.0

## Regla no negociable

Cuando una prueba falle: (1) determinar si el error está en el código o en la expectativa;
(2) documentar la causa; (3) corregir **la raíz**; (4) añadir prueba de regresión.
**Prohibido modificar una prueba para que pase si la prueba describe el comportamiento correcto.**

## Tipos de prueba

| Tipo | Alcance | Estado |
|---|---|---|
| Unit | `Nexo.Core` puro | ✅ 353 existentes (+3 en `Nexo.Windows` = 356) |
| Caracterización | Conducta actual de `MainWindow` antes de extraer | ⬜ Fase 1.1 |
| Integration | Runtime + servicios | ⬜ Fase 1 |
| Migration | Esquemas de settings, Nexo→Kohana, memoria | ⬜ Fase 1 |
| Privacy | Vision, logs, salida a red | ⬜ Fase 7 |
| Permission | Matriz de `SECURITY_MODEL` | ⬜ Fase 5 |
| Failure recovery | Escenarios de §Escenarios | ⬜ Fase 9 |
| Engine | Adaptadores de motores aislados | ⬜ Fase 3 |
| Benchmark | Voice Lab, latencia | ⬜ Fase 3 (solo Windows) |
| Installer / Portable / Update | Verificación de artefactos | ⬜ Fase 10 |
| UI smoke | Cuando sea viable | ⬜ Fase 9 |
| Manual checklist | `ACCEPTANCE_CRITERIA` §3 | ⬜ Fase 10 |

## Escenarios mínimos obligatorios

| # | Escenario | Resultado esperado |
|---|---|---|
| 1 | Instalación limpia | Onboarding completo sin terminal |
| 2 | Actualización | Datos y configuración conservados |
| 3 | Migración desde Nexo | Copia sin sobrescribir; origen intacto |
| 4 | **Configuración corrupta** | Arranca con valores seguros; avisa; no borra |
| 5 | **Memoria corrupta** | Aísla el archivo dañado; el resto sigue |
| 6 | Segunda instancia | Enfoca la existente; no duplica |
| 7 | Suspensión / reanudación | Voz y wake word se recuperan solos |
| 8 | Sin internet | Comandos locales intactos; error claro en IA remota |
| 9 | Ollama cerrado | Degradación clara; sin bloqueo de UI |
| 10 | Micrófono desconectado | Aviso; recuperación al reconectar |
| 11 | Batería baja | Baja a perfil Ahorro; documenta el cambio |
| 12 | Modo juego | Pausa IA, Vision, wake word; comandos locales vivos |
| 13 | **Captura sensible** | Bloqueada; explicada al usuario |
| 14 | **Skill sin permiso** | Rechazada; registrada; no ejecuta parcialmente |
| 15 | Acción cancelada | Rollback si es posible; estado consistente |
| 16 | Proveedor remoto | Salida visible en Actividad y privacidad |
| 17 | Modo privado | Sin rastro en memoria al terminar |
| 18 | Exportación de memoria | Archivo completo y legible |
| 19 | Desinstalación conservando datos | Datos intactos |
| 20 | Desinstalación eliminando datos | Limpieza completa y verificable |
| 21 | **Abrir PowerShell** | Sin confirmación |
| 22 | **Comando arbitrario en PowerShell** | **Con** confirmación |
| 23 | Inyección vía OCR | Texto tratado como dato; nunca ejecutado |
| 24 | Cierre inesperado | Recuperación de trabajos al reiniciar |

Los escenarios 4, 5, 13, 14, 21, 22 y 23 son **pruebas de seguridad**: su fallo bloquea la RC.

## Registro obligatorio por fase

Debe anotarse en `IMPLEMENTATION_LOG.md`: total de pruebas · errores · warnings · resultado de build ·
tiempos · tamaño del portable · tamaño del instalador · hash SHA-256.
