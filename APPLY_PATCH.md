# Aplicar Sprint 10

Copia únicamente el contenido de la carpeta `patch` sobre la raíz de
`C:\Dev\Nexo` y acepta combinar/reemplazar archivos.

Antes de compilar, sal completamente de Nexo desde la bandeja.

```powershell
cd C:\Dev\Nexo
dotnet restore .\Nexo.slnx
dotnet build .\Nexo.slnx
dotnet test .\Nexo.slnx
```

Para crear la edición portable:

```powershell
.\scripts\publish.ps1 -Version "0.9.0-beta"
.\scripts\verify-release.ps1
```

Para el instalador, instala Inno Setup 6 y ejecuta:

```powershell
.\scripts\build-installer.ps1 -Version "0.9.0-beta"
```
