# Publicación de Kohana

Los proyectos y la solución conservan temporalmente el nombre interno `Nexo`, pero los artefactos públicos ya se llaman Kohana.

## Requisitos

- Windows 10/11 x64.
- SDK de .NET 10.
- PowerShell 7 recomendado.
- Inno Setup 6 para crear el instalador.
- Kohana y cualquier instalación anterior de Nexo cerradas completamente.

## Validar la rama

```powershell
taskkill /F /IM Kohana.exe 2>$null
taskkill /F /IM Nexo.exe 2>$null

dotnet restore .\Nexo.slnx
dotnet test .\Nexo.slnx -c Release
dotnet build .\Nexo.slnx -c Release
```

## Crear la edición portable

```powershell
.\scripts\publish.ps1 `
  -Version "0.9.5-beta" `
  -RepositoryUrl "https://github.com/EXOTARA/Nexo"
```

Resultados:

```text
artifacts\publish\win-x64
artifacts\dist\Kohana-0.9.5-beta-win-x64-portable.zip
artifacts\dist\Kohana-0.9.5-beta-win-x64-portable.zip.sha256
```

La publicación es autocontenida: el usuario no necesita instalar .NET. Ollama y los modelos de IA/voz se administran por separado. No se incluyen datos personales.

## Crear el instalador

Instala Inno Setup 6 y ejecuta:

```powershell
.\scripts\build-installer.ps1 `
  -Version "0.9.5-beta" `
  -RepositoryUrl "https://github.com/EXOTARA/Nexo"
```

Resultados:

```text
artifacts\installer\Kohana-0.9.5-beta-Setup.exe
artifacts\installer\Kohana-0.9.5-beta-Setup.exe.sha256
```

El instalador conserva el `AppId` de la etapa Nexo para permitir una actualización continua, cambia el acceso directo a Kohana y elimina las entradas antiguas de inicio con Windows cuando corresponde.

## Prueba de migración

Antes de publicar, comprobar en una copia de prueba:

1. Iniciar una versión anterior de Nexo y crear una tarea o preferencia reconocible.
2. Cerrar Nexo completamente.
3. Abrir Kohana.
4. Confirmar que aparece `%LocalAppData%\Kohana`.
5. Confirmar que los datos antiguos se copiaron.
6. Confirmar que los modelos anteriores siguen disponibles sin haberse duplicado.
7. Confirmar que `%LocalAppData%\Nexo` sigue intacta.
8. Volver a abrir Kohana y confirmar que no duplica ni sobrescribe archivos.

## GitHub Release

Después de fusionar el PR y comprobar CI:

```powershell
git tag v0.9.5-beta
git push origin v0.9.5-beta
```

El workflow `release.yml` compila, prueba, publica, crea el instalador y adjunta ZIP, EXE y hashes.

## Repositorio durante la transición

El repositorio permanece temporalmente en `EXOTARA/Nexo`. La URL está centralizada y puede cambiarse en un sprint posterior, después de comprobar que el actualizador y los enlaces del instalador continúan funcionando.

## Firma digital

La beta no está firmada. Windows SmartScreen puede mostrar una advertencia. Para una versión estable se necesitará un certificado de firma de código y deberán firmarse tanto `Kohana.exe` como el instalador.
