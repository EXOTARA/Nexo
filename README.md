# Kohana

**Tu Windows, en flor.**

Kohana es un agente personal nativo para Windows. Combina comandos locales, voz, visión, tareas, rutinas, métricas del equipo e inteligencia artificial en una interfaz ligera que puede permanecer activa en segundo plano.

> El producto ya se presenta como **Kohana**. Los nombres internos `Nexo.App`, `Nexo.Core`, `Nexo.Windows` y `Nexo.slnx` se conservan temporalmente para reducir el riesgo del cambio de marca.

## Estado

La versión actual es `0.9.5-beta — Voice Reliability v3 + Kohana Runtime v1`.

Esta etapa incorpora:

- Identidad pública centralizada como Kohana.
- Diseño **Sakura Fluent** con grafito, rosa sakura e iconografía floral vectorial.
- Nuevo icono para la aplicación, la bandeja, el portable y el instalador.
- Ejecutable público `Kohana.exe`.
- Palabras de activación `Kohana`, `Oye Kohana` y `Hey Kohana`.
- Pronunciaciones españolas `cojana` y `kojana`, diagnóstico visible de Vosk y aliases personales.
- Migración conservadora de `%LocalAppData%\Nexo` a `%LocalAppData%\Kohana`.
- Publicación portable e instalador renombrados como Kohana.

## Capacidades actuales

### Windows y productividad

- Barra lateral modular, Inicio tipo bento y modo Peek.
- Atajos globales `Alt + A`, `Alt + Shift + A`, `Ctrl + Espacio` y `Ctrl + Shift + Espacio`.
- Bandeja del sistema, inicio opcional con Windows e instancia única.
- Tareas, recordatorios, sesiones de enfoque y rutinas locales.
- Acciones seguras para abrir aplicaciones, carpetas y terminales.
- Mezclador real de volumen general y por aplicación.
- Métricas de CPU, RAM, GPU, VRAM, almacenamiento y proceso principal.
- Resource Governor con modos Normal, Busy y Game.

### Voz e IA

- Push-to-talk local con Whisper.
- Wake word local con Vosk.
- Búfer previo para decir la activación y la orden de corrido.
- Proveedores OpenAI, Ollama, LM Studio y endpoints compatibles.
- Runtime privado de Ollama administrado por Kohana.
- Streaming de respuestas y contexto de sistema opcional.
- Comandos conocidos resueltos localmente antes de consultar un modelo.

### Runtime y estado

- Panel unificado en Sistema para voz, IA, Vision y rendimiento.
- Reinicio de voz y acceso directo a Diagnóstico.
- Registro textual de intentos de wake word sin conservar audio.

### Vision y privacidad

- Captura bajo demanda de ventanas y monitores.
- Look Mode temporal para consultar la ventana activa.
- Vista previa antes de compartir una imagen.
- Bloqueo inicial de gestores de contraseñas y ventanas sensibles.
- Diagnóstico técnico visual con evidencia y comprobación.
- Capturas mantenidas en memoria y fuera del historial de texto.

## Datos y migración

Los datos nuevos se guardan en:

```text
%LocalAppData%\Kohana
```

En la primera ejecución, Kohana busca una carpeta anterior:

```text
%LocalAppData%\Nexo
```

Si existe, copia los archivos que falten sin sobrescribir datos nuevos y sin eliminar la carpeta anterior. Las carpetas temporales, logs, modelos y runtimes pesados no se copian. Se crea un marcador local para evitar repetir el proceso.

Los modelos de voz y el runtime local no se duplican: Kohana reutiliza temporalmente sus rutas anteriores cuando todavía no existen copias nuevas. Las claves no se almacenan en el repositorio ni dentro de `settings.json`.

## Palabras de activación

La recomendada es:

```text
Oye Kohana
```

También se puede elegir `Kohana` o `Hey Kohana`. Las frases antiguas de Nexo solo se conservan como valores heredados de configuración; el modo Kohana ya no las acepta implícitamente.

## Desarrollo

Requisitos:

- Windows 10/11 x64.
- SDK de .NET 10.
- PowerShell 7 recomendado.

Comandos:

```powershell
dotnet restore .\Nexo.slnx
dotnet test .\Nexo.slnx -c Release
dotnet build .\Nexo.slnx -c Release
```

El proyecto se trabaja mediante ramas cortas y Pull Requests contra `main`. La protección de `main` debe exigir CI en verde antes de fusionar.

## Publicación

```powershell
.\scripts\publish.ps1 `
  -Version "0.9.5-beta" `
  -RepositoryUrl "https://github.com/EXOTARA/Nexo"
```

La edición portable se genera como:

```text
artifacts\dist\Kohana-0.9.5-beta-win-x64-portable.zip
```

Con Inno Setup 6 instalado:

```powershell
.\scripts\build-installer.ps1 `
  -Version "0.9.5-beta" `
  -RepositoryUrl "https://github.com/EXOTARA/Nexo"
```

Consulta `docs/PUBLISHING.md`, `docs/KOHANA_BRAND_FOUNDATION.md` y `RELEASE_CHECKLIST.md` antes de publicar.

## Dirección final

Kohana evolucionará hacia un agente completo para Windows con memoria controlable, acciones aprobables, skills, automatizaciones persistentes, navegador aislado, servicios conectados, agentes especializados y dispositivos emparejados. La prioridad será mantener una instalación sencilla, privacidad visible y control humano sobre cada acción sensible.
