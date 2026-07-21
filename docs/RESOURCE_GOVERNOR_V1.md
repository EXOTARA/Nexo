# Resource Governor v1 + Silent Voice Look

## Objetivo

Nexo protege la experiencia de Windows antes de iniciar inferencia local o Vision.
Los comandos locales permanecen disponibles en todos los estados.

## Estados

- **Normal**: IA local, IA remota, Vision y wake word disponibles.
- **Busy**: se pausa IA local y Vision cuando CPU, GPU o RAM superan los umbrales de seguridad. Un proveedor remoto ya configurado puede seguir respondiendo.
- **Game**: una aplicación externa cubre completamente un monitor. Se pausan IA, Vision, wake word y cápsulas transitorias; los comandos locales siguen disponibles.

## Umbrales v1

- GPU: 88 %
- CPU: 92 %
- RAM: 92 %

Son umbrales conservadores para la alpha y se ajustarán con telemetría local y pruebas reales.

## Mirar mediante voz

Al usar una frase configurada y preguntar, por ejemplo:

- “Hey Nexo, ¿qué es esto?”
- “Nexo, ¿por qué falla esto?”
- “Oye Nexo, mira esto y dime qué significa.”

Nexo captura un único fotograma de la ventana activa en memoria, lo adjunta silenciosamente a la consulta y lo elimina al vencer el contexto temporal. No abre selector ni vista previa y no guarda la imagen en disco.

Esto es el puente hacia la visión conversacional final. Todavía no es video continuo ni permite interrupción de voz durante la respuesta.

## Registro

Los cambios de modo se registran en:

`%LocalAppData%\Nexo\Logs\resource-governor.log`
