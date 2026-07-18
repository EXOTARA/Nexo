# Nexo Sprint 4B — Whisper local

Este parche reemplaza el reconocimiento clásico de Windows por una transcripción local con Whisper.

## Antes de copiar

Confirma que estás en:

```powershell
git branch --show-current
```

Debe mostrar:

```text
feat/sprint-4-voice-input
```

Los paquetes `Whisper.net` y `Whisper.net.Runtime` ya aparecen en tu `Nexo.Windows.csproj`, así que no hace falta volver a instalarlos.

## Aplicar

Copia las carpetas `src` y `tests`, además de `README.md` y `CHANGELOG.md`, dentro de:

```text
C:\Dev\Nexo
```

Acepta combinar y reemplazar archivos.

El archivo `src/Nexo.Windows/Voice/WindowsVoiceInputService.cs` conserva su ruta para que Windows lo reemplace correctamente, pero ahora contiene la clase `WhisperVoiceInputService`.

## Compilar

```powershell
cd C:\Dev\Nexo
dotnet build .\Nexo.slnx
dotnet test .\Nexo.slnx
dotnet run --project .\src\Nexo.App\Nexo.App.csproj
```

Deberían pasar 32 pruebas.

## Primera ejecución

Nexo descargará una vez el modelo multilingüe `base` y lo guardará en:

```text
%LocalAppData%\Nexo\Models\ggml-base.bin
```

La descarga puede tardar algunos minutos. El botón Mic permanece desactivado hasta que termine y el estado muestra los MB descargados.

## Prueba

Cuando aparezca `Voz local lista · Whisper base · español`:

1. Mantén `Mic` presionado.
2. Di `pon Discord al veinticinco por ciento`.
3. Suelta `Mic`.

También prueba:

```text
cómo está mi PC
muestra Peek
abre PowerShell
baja Spotify
silencia Discord
```

El audio temporal se guarda en `%LocalAppData%\Nexo\Temp` y se elimina después de transcribirlo.

## Sobre “Nexo” como palabra de activación

Este parche deja estable la transcripción push-to-talk. La escucha de la palabra `Nexo` será el siguiente bloque y usará un detector ligero separado; Whisper no permanecerá transcribiendo todo el tiempo.
