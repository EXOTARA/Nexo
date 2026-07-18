# Sprint 4C — Activación por “Nexo”

Este parche parte del ZIP `Nexo-wakeword-current.zip` que compartiste. No necesita cuenta, AccessKey ni archivos de Picovoice.

## Qué agrega

- Activación opcional con `Nexo` u `Oye Nexo`.
- Detector local Vosk con vocabulario restringido.
- Descarga automática del modelo pequeño de español la primera vez.
- Indicador `Nexo atento` visible en la cabecera.
- Pausa automática del detector cuando usas Mic o cuando Whisper procesa una orden.
- Grabación automática después de la activación.
- Final automático al detectar silencio o al llegar al tiempo máximo.
- Whisper sigue transcribiendo la orden completa.
- La preferencia se guarda, pero está apagada por defecto.

## Importante sobre esta primera versión

El flujo es de dos pasos:

1. Di `Nexo`.
2. Espera la cápsula **Te escucho**.
3. Pronuncia la orden.

Ejemplo:

```text
Nexo
[Te escucho]
sube Spotify al setenta
```

Decir `Nexo, sube Spotify al setenta` todo seguido todavía puede cortar el inicio de la orden. Eso queda para el siguiente refinamiento con búfer previo de audio.

## Consumo

Whisper solo se carga cuando transcribe. El detector Vosk, en cambio, permanece en memoria mientras la activación está encendida y puede usar alrededor de 300 MB de RAM. Por eso la opción es experimental y permanece desactivada de forma predeterminada.

## Aplicación

Copia todo el contenido de esta carpeta dentro de:

```text
C:\Dev\Nexo
```

Acepta combinar carpetas y reemplazar archivos.

Después ejecuta:

```powershell
cd C:\Dev\Nexo
dotnet restore .\Nexo.slnx
dotnet build .\Nexo.slnx
dotnet test .\Nexo.slnx
dotnet run --project .\src\Nexo.App\Nexo.App.csproj
```

## Primera prueba

Entra a:

```text
Personalización → Voz
```

Activa:

```text
Activar Nexo mediante una frase de voz (experimental)
```

La primera vez descargará `vosk-model-small-es-0.42` y lo guardará en:

```text
%LocalAppData%\Nexo\Models\Vosk\vosk-model-small-es-0.42
```

Cuando aparezca `Nexo atento`, prueba:

```text
Nexo
```

Espera la cápsula **Te escucho** y después di:

```text
cómo está mi PC
```

Luego prueba:

```text
Nexo
[Te escucho]
sube Spotify al setenta
```

También prueba `Oye Nexo` desde Personalización. Es más largo, pero puede producir menos activaciones accidentales.

## Si algo falla

- Si no descarga el modelo, revisa la conexión y el espacio disponible.
- Si Vosk no inicia, ejecuta `dotnet clean .\Nexo.slnx`, restaura y compila otra vez.
- Si hay muchas activaciones accidentales, cambia la frase a `Oye Nexo`.
- Si la orden no se graba, espera a que aparezca **Te escucho** antes de hablar.

Todavía no hagas commit. Primero comprueba compilación, pruebas, descarga, activación y consumo en el Administrador de tareas.
