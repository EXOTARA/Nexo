# Kohana Brand Foundation

## Decisión

La identidad pública de Nexo cambia a **Kohana**. La intención es representar un agente que nace desde Windows y conecta voz, visión, memoria, automatización y sistema desde un mismo centro.

Lema:

> **Tu Windows, en flor.**

## Sakura Fluent

La interfaz combina superficies oscuras de Windows con una identidad orgánica discreta:

- fondo grafito;
- superficies azul grisáceo;
- rosa sakura como acento, no como fondo dominante;
- blanco cálido para texto;
- menta, ámbar y coral para estados;
- animaciones suaves y desactivables;
- geometría de pétalos aplicada a iconos sin perder claridad funcional.

## Marca

El símbolo principal es una flor de cinco pétalos. Debe seguir siendo reconocible en:

- 16 px para bandeja;
- 24–32 px para navegación;
- 64 px para onboarding;
- 256 px para instalador y documentación.

Los módulos no usan la misma flor repetida. Cada icono conserva su significado:

- Inicio: flor abierta.
- Chat: burbuja con pétalo.
- Tareas: hoja con check.
- Enfoque: capullo dentro de un objetivo.
- Automatizaciones: pétalos conectados.
- Vision: ojo con iris floral.
- Voz: pétalos como onda.
- Sistema: flor con pulso.
- Configuración: roseta con estructura de engrane.

## Superficies

### Kohana Capsule

Estado inmediato: escuchando, procesando, éxito, advertencia, error o confirmación.

### Kohana Peek

Vista rápida de tareas, enfoque, recursos y acceso a órdenes sin abrir todo el Hub.

### Kohana Hub

Interfaz completa con Inicio, Chat, Tareas, Enfoque, Automatizaciones, Vision, Voz, Conexiones, Skills, Sistema y Configuración. Los módulos futuros se incorporarán gradualmente.

## Compatibilidad técnica

Para reducir riesgos, esta etapa mantiene nombres internos como:

```text
Nexo.App
Nexo.Core
Nexo.Windows
Nexo.slnx
```

El usuario ya ve Kohana y el artefacto público es `Kohana.exe`. El renombrado de namespaces y carpetas será un sprint separado después de validar migración, actualización y publicación.

## Wake word

Frases actuales:

- `Oye Kohana` — recomendada.
- `Kohana` — rápida.
- `Hey Kohana` — alternativa.

Durante la transición también se reconocen las frases anteriores de Nexo. Los archivos de preferencias con valores heredados se migran a `Oye Kohana`.

## Datos

Kohana usa `%LocalAppData%\Kohana`. La migración desde `%LocalAppData%\Nexo`:

- copia, no mueve;
- no elimina el origen;
- no sobrescribe archivos existentes;
- excluye logs, temporales, modelos y runtimes pesados;
- reutiliza los modelos y el runtime heredados sin duplicarlos;
- deja un marcador de migración;
- puede reintentarse sin duplicar datos.

## Repositorio

El repositorio permanece temporalmente como `EXOTARA/Nexo`. Esto evita romper actualizaciones y enlaces durante el cambio de marca. Se renombrará después de validar el instalador y el sistema de releases.
