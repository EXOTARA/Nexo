# Voice Reliability v1

Este sprint corrige los dos problemas observados durante la prueba real:

1. La activación por “Hey Nexo” podía entregar tarde el micrófono a Whisper.
2. La escucha automática terminaba después de solo 700 ms de silencio, por lo que
   cortaba pausas normales y frases un poco largas.

## Nuevo flujo

- Vosk conserva tres segundos de audio previo.
- Después de detectar la frase, conserva también una ventana separada de audio
  posterior para saber si la orden comenzó de inmediato.
- Whisper escucha hasta 20 segundos.
- La escucha no puede cerrarse durante sus primeros 2.5 segundos.
- Una pausa menor a 1.5 segundos no termina la orden.
- Se exige voz sostenida antes de considerar que comenzó una frase.
- El umbral se adapta al ruido y usa histéresis para no cortar el final de las palabras.

## Micrófono manual

El botón ya no requiere mantenerlo presionado:

1. Pulsa una vez para comenzar.
2. Habla con naturalidad.
3. Pulsa de nuevo para terminar.

## Privacidad y diagnóstico

El audio temporal sigue eliminándose después de transcribirlo. Nexo escribe solamente
métricas técnicas, nunca el audio ni la transcripción, en:

`%LocalAppData%\Nexo\Logs\voice-capture.log`

El registro contiene duración, amplitud, ruido estimado, proporción de voz y cantidad
de palabras. Sirve para calibrar la siguiente iteración sin exponer conversaciones.

## Alcance

Este sprint mejora captura, segmentación y variantes frecuentes de “Hey Nexo”.
Continúa usando Whisper base. Si la grabación completa sigue produciendo errores,
el siguiente paso será permitir Whisper small como modo de mayor precisión.
