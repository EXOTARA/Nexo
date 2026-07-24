# Kohana 0.9.5 — Voice Reliability v3 y Runtime v1

## Voz

- La gramática Kohana ya no incluye `Nexo`; la compatibilidad heredada solo funciona si se selecciona explícitamente una frase antigua.
- La pronunciación española se trata como forma principal: `cojana`, `kojana`, `oye cojana` y `ey cojana`.
- Ajustes muestra el texto parcial/final reconocido por Vosk y el motivo de aceptación o rechazo.
- El usuario puede guardar hasta ocho aliases personales. Solo se guarda texto normalizado, nunca audio.
- Los intentos de reconocimiento se registran en `%LocalAppData%\Kohana\Logs\wake-word-recognition.log`.

## Runtime

La vista Sistema incorpora un panel de estado para:

- voz y wake word;
- proveedor de IA;
- Kohana Vision;
- modo normal, ocupado o juego.

También permite reiniciar la voz y abrir Diagnóstico sin abandonar la vista.
