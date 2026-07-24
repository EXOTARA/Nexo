# ADR 0002 — Separar Perfil de Hardware del Resource Governor

**Estado:** Aceptado · **Fecha:** 2026-07-22 · **Fase:** 2

## Contexto

`ResourceGovernorPolicy` ya responde bien a *"¿qué puede ejecutar el equipo **ahora mismo** sin
molestar al usuario?"* (Normal/Busy/Game según CPU/GPU/RAM y pantalla completa).

No existe nada que responda *"¿qué puede ejecutar bien este equipo **en general**?"*.
Verificado: 0 coincidencias de `HardwareCapability` o `CapabilityProfile` en el código.

## Decisión

Crear **Hardware Capability Profile** como sistema **independiente**, no como extensión del
Resource Governor. Son dos ejes ortogonales y fusionarlos produce decisiones incorrectas: un equipo
potente momentáneamente ocupado no es lo mismo que un equipo modesto en reposo.

| Eje | Pregunta | Naturaleza | Componente |
|---|---|---|---|
| Capacidad | ¿De qué es capaz este equipo? | Estático, se calcula una vez | `HardwareCapabilityProfile` (nuevo) |
| Carga | ¿Qué puede hacer ahora? | Dinámico, continuo | `ResourceGovernorPolicy` (existente) |

Detección: versión de Windows · arquitectura · CPU · núcleos lógicos · RAM · GPU integrada/dedicada ·
VRAM · aceleradores · batería · alimentación · capacidades ONNX · aceleración disponible · espacio libre.

Etiquetas al usuario: **Ligero · Equilibrado · Acelerado**.
Elección del usuario: **Automático · Ahorro · Equilibrado · Máximo rendimiento** (el usuario siempre
puede forzar; el default es Automático).

Internamente se conservan los **datos concretos**, no solo la etiqueta.

## Consecuencias

Permite que el Adaptive Engine Registry seleccione motor por evidencia y no por suposición.
Añade una superficie nueva que debe probarse en ≥2 equipos distintos.

**Restricción dura:** prohibido descargar modelos automáticamente sin consentimiento explícito.
El perfil *recomienda*; nunca actúa solo.
