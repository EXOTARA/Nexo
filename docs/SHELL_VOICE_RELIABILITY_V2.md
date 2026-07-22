# Kohana 0.9.4 — Shell y Voice Reliability v2

Este bloque corrige la contracción de la barra lateral, separa visualmente la navegación de la paleta de comandos y mejora la activación local por “Ey Kohana”.

## Shell

- El contenedor interno cambia de 52 a 178 px junto con el borde exterior.
- Las etiquetas se colapsan realmente en lugar de quedar recortadas fuera de vista.
- El estado se conserva en `settings.json`.
- El botón del menú usa un panel lateral con chevrón.
- Ctrl + Espacio usa una lupa de comandos independiente.

## Voz

- Modos Precisa, Equilibrada y Alta.
- Normalización de “ko ana”, “co hana” y variantes frecuentes.
- Gramática Vosk ampliada para “ey”, “e”, “eh”, “hay” y pronunciaciones cercanas.
- Prueba de 12 segundos desde Ajustes que no inicia una orden.

## Privacidad

La detección y la prueba continúan siendo locales. La prueba solo muestra el texto breve reconocido para la frase de activación y no guarda audio.
