# Fronteras de privacidad — Kohana 1.0

## Regla de oro

**Ninguna captura persistente oculta. Ninguna salida a red invisible.**

## Qué NUNCA sale del equipo sin acción explícita del usuario

- Audio (crudo o procesado)
- Capturas de pantalla
- Contenido de la memoria
- Conversaciones
- Tokens, claves y credenciales
- Contenido del portapapeles

Esto aplica también a logs, reportes de fallo y diagnósticos (decisión J).

## Qué NUNCA se registra en logs

contraseñas · tokens · contenido completo del portapapeles · capturas · audio ·
conversaciones privadas sin autorización.

Los logs contienen **métricas técnicas**: duración, amplitud, ruido estimado, proporción de voz,
conteo de palabras, motivo de aceptación/rechazo. Nunca el contenido.

## Vision — garantías obligatorias

| Garantía | Estado en 0.9.5 |
|---|---|
| Captura **bajo demanda** (nunca continua) | ✅ Ya cumplido |
| Procesamiento **en memoria** | ✅ Ya cumplido |
| **No** guardar capturas por defecto | ✅ Ya cumplido |
| Bloquear gestores de contraseñas | ✅ `VisionPrivacyPolicy` |
| Bloquear ventanas sensibles | ✅ Por proceso y por título |
| Registrar **sin** contenido visual | ✅ |
| Mostrar cuándo una imagen sale a un proveedor remoto | ⬜ Fase 9 |

Pipeline objetivo (Fase 7), en orden estricto:
1. Identificar ventana o región
2. Aplicar política de privacidad
3. Consultar UI Automation
4. Aplicar OCR local
5. Decidir si basta texto/controles
6. Usar VLM **únicamente cuando aporte valor**
7. Mostrar evidencia al usuario
8. **Descartar imagen**

## Memoria — opt-in (decisión G)

- Kohana **no escribe memoria automáticamente**.
- Puede **sugerir** recordar; requiere aprobación explícita.
- Diario automático: opción explícita, **desactivada por defecto**.
- El usuario puede abrir, editar, exportar y borrar los archivos directamente.
- La base SQLite es **índice**, nunca fuente opaca de verdad.
- Sesión privada disponible: sin memoria persistente.
- El usuario puede preguntar **qué memoria se utilizó** en una respuesta concreta.

## Modo dataset de audio — opt-in doble (decisión H)

| Regla | Obligatoria |
|---|---|
| Desactivado por defecto | ✅ |
| Audio visible para el usuario | ✅ |
| Escuchar, revisar y borrar cada muestra | ✅ |
| Exportación **manual** | ✅ |
| Subida automática | ❌ **Prohibida** |
| Consentimiento **separado** para compartir | ✅ |

Fuera de este modo, **el audio temporal se elimina tras transcribirlo**.

## Matriz local vs. remoto (visible al usuario en Fase 9)

| Operación | Local por defecto | Puede ser remota |
|---|---|---|
| Wake word | ✅ Siempre local | ❌ Nunca |
| VAD / fin de turno | ✅ Siempre local | ❌ Nunca |
| STT | ✅ | ❌ En 1.0 no |
| TTS | ✅ | ❌ En 1.0 no |
| OCR | ✅ Siempre local | ❌ Nunca |
| UI Automation | ✅ Siempre local | ❌ Nunca |
| Comandos conocidos | ✅ Siempre local | ❌ Nunca |
| Chat / razonamiento | Configurable | ✅ Con proveedor configurado |
| Análisis de imagen (VLM) | Configurable | ✅ Requiere aviso visible |
| Memoria | ✅ Siempre local | ❌ Nunca |
| Telemetría | ✅ Solo local | ❌ Nunca automática |

Toda celda con salida remota **debe** aparecer en "Actividad y privacidad" con hora, proveedor,
tipo de dato y resultado.
