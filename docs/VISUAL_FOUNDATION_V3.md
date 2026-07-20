# Visual Foundation v3 · Obsidian Bloom

Esta iteración convierte los retoques visuales de Nexo en un sistema reutilizable.
No intenta cerrar el diseño final: establece una base coherente para que los siguientes
sprints funcionales no vuelvan a introducir controles pequeños, barras de Windows o
animaciones que degradan el texto.

## Dirección visual

- Fondo oscuro profundo con capas separadas por contraste, no por bordes gruesos.
- Acento configurable y glifo floral para funciones abstractas de Nexo.
- Tipografía mínima legible de 12–13 px para información normal.
- Iconos principales entre 17 y 20 px.
- Radios consistentes, luces superiores discretas y una sola sombra dominante.
- Movimiento breve mediante opacidad y traslación; no se escala texto durante la entrada.

## Paleta de comandos

- `Ctrl + Espacio`: abrir la paleta.
- Una letra o prefijo corto completa una acción clara.
- `Tab`: aceptar la sugerencia seleccionada.
- `Enter`: ejecutar una coincidencia clara o enviar una consulta natural.
- `Ctrl + Enter`: forzar el envío del texto exacto como consulta.
- `Shift + Enter`: insertar una nueva línea.
- `Alt + Enter`: abrir el espacio completo de Nexo sin enviar.
- `↑` y `↓`: cambiar la selección explícitamente.

Las preguntas largas muestran una opción única **Preguntar a Nexo** para que el usuario
sepa que su texto se enviará completo y no será reemplazado por un comando parecido.

## Navegación

La barra lateral se mantiene compacta por defecto. Al pulsar el glifo superior se expande
sobre el contenido, muestra nombres completos y no cambia el tamaño del módulo activo.
`Esc` contrae primero la navegación; una segunda pulsación oculta Nexo.

## Accesibilidad y legibilidad

- Scrollbars de 8 px, sin flechas ni canales blancos.
- Estados de teclado visibles en botones y navegación.
- Tooltips oscuros con contraste estable.
- Texto secundario más claro y texto terciario reservado para ayudas.
- Ancho mínimo del shell de 468 px para evitar clipping.

## Validación manual

1. Abrir con `Ctrl + Espacio`; el texto debe verse nítido desde el primer fotograma.
2. Escribir `e` y pulsar `Enter`; debe abrir el Explorador.
3. Escribir `abre vis`; debe completar y abrir Visual Studio Code.
4. Escribir una pregunta larga; debe aparecer **Preguntar a Nexo** y conservarse completa.
5. Probar `Shift + Enter` con un prompt multilínea.
6. Recorrer la lista y la conversación; las barras deben ser finas y oscuras.
7. Expandir la barra lateral y verificar nombres, contraste y foco de teclado.
