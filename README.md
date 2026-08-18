# Kohana

**Tu Windows, en flor.**

Kohana es un agente personal nativo para Windows. Combina comandos locales, voz, visión, memoria
personal, automatización de proyectos y del equipo, e inteligencia artificial en una interfaz
ligera que puede permanecer activa en segundo plano — siempre con permisos comprensibles y
confirmación para cualquier acción sensible.

> El producto ya se presenta como **Kohana**. Los nombres internos `Nexo.App`, `Nexo.Core`, `Nexo.Windows` y `Nexo.slnx` se conservan temporalmente para reducir el riesgo del cambio de marca.

## Estado

El ejecutable publicado reporta `0.9.5-beta`. La rama `design/kohana-sprints-d7-d9` contiene
diseño hasta **D24**, con memoria personal, optimización verificada del equipo, un acompañante de
proyecto que lee y modifica archivos con confirmación, permisos por capacidad, acción controlada
sobre el equipo, seis packs y una autocomprobación interna — nada de esto integrado todavía a
`release/kohana-1.0-rc` ni a `main`.

**→ La lista completa, con cómo activar cada cosa y ejemplos de uso, está en
[`docs/product/KOHANA_CAPABILITIES_GUIDE.md`](docs/product/KOHANA_CAPABILITIES_GUIDE.md).**

Resumen de lo que ya funciona:

- Identidad pública centralizada como Kohana, con diseño **Sakura Fluent**.
- Shell modular (Inicio, Asistente, Tareas, Enfoque, Automatizaciones, Sistema, Personalizar…) con Peek, bandeja e instancia única.
- Voz local: wake word con Vosk, dictado con Whisper, dictado **global** en cualquier aplicación (Kohana Flow).
- Kohana Lens: lee la pantalla bajo demanda, con redacción automática de datos sensibles.
- **Memoria personal**, apagada por omisión, cifrada con DPAPI, con control explícito por categoría.
- **Optimización del equipo** que solo aplica lo que puede revertir con certeza, verificando cada cambio.
- **Acompañante de proyecto**: autoriza una carpeta, explica, busca, y modifica archivos con copia previa y verificación.
- **Permisos por capacidad** (Bloqueado / Preguntar / Permitido) con confirmaciones que no se saltan nunca.
- **Actuar sobre el equipo**, siempre por el método más seguro disponible entre los implementados.
- **Seis packs** (Study, Dev, Support, Creator, Access, Meeting) que combinan lo anterior sin conceder permisos por su cuenta.
- Registro de actividad único, copia de seguridad verificada, diagnóstico exportable redactado y una autocomprobación que revisa la maquinaria en tu propio equipo.

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

## Documentación

| Para… | Documento |
|---|---|
| Ver qué hace cada capacidad y cómo activarla | [`docs/product/KOHANA_CAPABILITIES_GUIDE.md`](docs/product/KOHANA_CAPABILITIES_GUIDE.md) |
| Entender el porqué de cada decisión de diseño | [`docs/stable-release/IMPLEMENTATION_LOG.md`](docs/stable-release/IMPLEMENTATION_LOG.md) |
| Ver el estado por fase del roadmap | [`docs/roadmap/KOHANA_TECHNOLOGY_ROADMAP.md`](docs/roadmap/KOHANA_TECHNOLOGY_ROADMAP.md) |
| Permisos, niveles de autonomía y confirmaciones obligatorias | [`docs/security/KOHANA_TRUST_AND_AUTONOMY_MODEL.md`](docs/security/KOHANA_TRUST_AND_AUTONOMY_MODEL.md) |
| Validar manualmente antes de integrar | [`artifacts/Kohana-Guia-De-Validacion-Manual-D13-D24.md`](artifacts/Kohana-Guia-De-Validacion-Manual-D13-D24.md) |

## Dirección final

Memoria controlable, acciones aprobables por capacidad, skills empaquetadas en packs, y acción
directa sobre el equipo ya existen en la rama de diseño — ver la tabla de arriba para el estado
real de cada una. Lo que sigue: automatizaciones persistentes programadas, navegador aislado,
servicios conectados (correo, calendario, mensajería), agentes especializados y dispositivos
emparejados. La prioridad se mantiene: instalación sencilla, privacidad visible y control humano
sobre cada acción sensible — el modelo de confianza que hace cumplir esa prioridad no es aspiracional,
está en el código y probado.
