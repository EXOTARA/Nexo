# Nexo Shell v4.1 — Focus & Look

## Objetivo

Reducir ruido visual y convertir **Mirar** en una acción inmediata de contexto, no en un flujo de captura manual.

## Look Mode

Formas de activación:

- `Ctrl + Shift + Espacio`
- Botón de contexto visual en el compositor
- `Ctrl + Espacio` → `mira esta ventana`
- Tarjeta **Mirar ahora** en Inicio

Flujo:

1. Nexo recuerda la ventana que estaba activa.
2. Obtiene la ventana mediante el servicio de captura existente.
3. La imagen se mantiene en memoria y no se guarda en disco.
4. Se adjuntan aplicación, título y dimensiones visibles como contexto.
5. El contexto permanece durante dos minutos y se renueva tras cada respuesta.
6. El usuario puede descartarlo desde el compositor.

El módulo **Captura** conserva el selector manual y la vista previa para elegir una ventana diferente o un monitor.

## Paleta

- El texto escrito ya no se reemplaza automáticamente.
- `Backspace` siempre edita el texto real.
- `Tab` acepta la sugerencia seleccionada.
- `Enter` ejecuta una coincidencia clara o envía el prompt.
- `Ctrl + Enter` fuerza una consulta.
- `Esc` limpia y después cierra.
- El historial antiguo se normaliza hacia comandos canónicos.

## Shell

- Ancho recomendado: 680–820 px.
- Barra contraída: botones de 52 px dentro de un rail de 68 px.
- Barra expandida: 178 px dentro de un rail de 194 px.
- El botón de comandos del encabezado se reduce a un solo icono.
- Los títulos se recortan con elipsis en vez de salir del contenedor.

## Pendiente

- OCR local para decidir entre texto e imagen.
- Detección de cambio de ventana para invalidar contexto antes de los dos minutos.
- Resource Governor y Modo Juego.
- Pruebas multi-monitor y HDR.
