# ADR 0004 — Vosk pasa a fallback heredado; sustitución solo por evidencia

**Estado:** Aceptado · **Fecha:** 2026-07-22 · **Fase:** 3–4

## Contexto

`VoskWakeWordService` usa Vosk (ASR de vocabulario abierto) restringido con una gramática tipo lista
para simular un detector de palabra clave. La versión 0.9.5 amplió esa lista con pronunciación
mexicana (`cojana`, `kojana`) y aliases personales.

La estrategia de fondo es **acumular excepciones fonéticas**, lo que tiene un techo: cada variante no
prevista es un fallo, y se carga un modelo acústico completo para detectar dos palabras.

## Decisión

1. Vosk se declara **`FallbackHeredado`**, no la solución objetivo.
2. **No se sustituye por popularidad ni por recomendación documental.** Cualquier candidato
   (openWakeWord, Silero VAD, Piper, Kokoro, Chatterbox, Moonshine…) pasa por el mismo proceso:

   repositorio oficial → licencia actual → soporte Windows → compatibilidad .NET →
   distribución de modelos → **adaptador aislado** → medir → comparar contra fallback →
   documentar → decidir

3. Se construye **Kohana Voice Lab** con benchmarks reproducibles antes de tocar el motor.
4. Cada frase de activación se calibra **por separado**. Prohibido asumir que un mismo modelo y
   umbral sirven para `Oye Kohana`, `Kohana`, `Ey Kohana` y `Hey Kohana`.
5. Estables en 1.0: `Oye Kohana`, `Kohana`. Experimentales: `Ey Kohana`, `Hey Kohana`.
6. Vosk **permanece disponible** como fallback seleccionable incluso tras promover un motor nuevo.
   Solo puede retirarse tras un periodo de estabilidad documentado.

## Restricciones de licencia (decisión C)

Solo MIT / Apache 2.0 / BSD. Excluidos: Porcupine (comercial, requiere `AccessKey`) y
Moondream2 (restricción no comercial). No se redistribuyen pesos sin verificar permiso.

## Métricas que deciden

latencia de detección · falso positivo · falso negativo · CPU · RAM · ruido · distancia ·
pronunciación mexicana · recuperación tras suspensión · comportamiento en modo juego.

Un candidato se promueve **solo si gana medido** al fallback en la mayoría de estas métricas, sin
empeorar gravemente ninguna.

## Nota sobre datos

No se guarda audio salvo modo dataset explícito, local y opt-in (`PRIVACY_BOUNDARIES`).
Los benchmarks registran métricas y texto reconocido, nunca audio.
