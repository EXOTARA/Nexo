# Publicación de Nexo

## Requisitos

- Windows 10/11 x64.
- SDK de .NET 10.
- Inno Setup 6 para crear el instalador.
- Nexo cerrado completamente desde la bandeja.

## Crear la edición portable

```powershell
.\scripts\publish.ps1 `
  -Version "0.9.0-beta" `
  -RepositoryUrl "https://github.com/USUARIO/REPOSITORIO"
```

Los resultados aparecen en:

```text
artifacts\publish\win-x64
artifacts\dist\Nexo-0.9.0-beta-win-x64-portable.zip
```

La publicación es autocontenida: el usuario no necesita instalar .NET. Ollama,
Whisper y Vosk descargan sus modelos por separado y los datos personales no se
incluyen.

## Crear el instalador

Instala Inno Setup 6 y ejecuta:

```powershell
.\scripts\build-installer.ps1 `
  -Version "0.9.0-beta" `
  -RepositoryUrl "https://github.com/USUARIO/REPOSITORIO"
```

El instalador se genera en `artifacts\installer`.

## Primera beta en GitHub

1. Fusiona el PR en `main`.
2. Confirma que CI esté en verde.
3. Crea y sube la etiqueta:

```powershell
git tag v0.9.0-beta
git push origin v0.9.0-beta
```

El workflow `release.yml` compila, prueba, publica, crea el instalador y adjunta
los artefactos a GitHub Releases.

## Firma digital

La beta no está firmada. Windows SmartScreen puede advertir al ejecutarla.
No conviene intentar ocultar esa advertencia. Para una distribución pública
estable se deberá comprar o conseguir un certificado de firma de código y
firmar tanto `Nexo.exe` como el instalador.
