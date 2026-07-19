# Sprint 5B — voz más confiable

Este parche se preparó sobre el ZIP actual de Nexo que compartiste.

## Qué cambia

- Permite decir `Nexo, abre PowerShell` o `Oye Nexo, muestra Peek` de corrido.
- Conserva unos segundos de audio previos al cambio de Vosk a Whisper para no perder el inicio de la orden.
- Permite elegir el micrófono desde **Personalización → Voz**.
- Usa el mismo micrófono para el botón Mic y para la frase de activación.
- Ajusta el umbral de voz según el ruido inicial del micrófono.
- Estima la calidad de la grabación.
- Cuando la orden se escucha con poca claridad, Nexo no la ejecuta de inmediato y pide confirmación.
- Puedes responder `Nexo, confirmar` o `Nexo, cancelar`.
- Elimina `Nexo` y `Oye Nexo` del inicio antes de interpretar la orden.
- Actualiza la migración de preferencias al esquema 7.

## Cómo aplicarlo

1. Cierra Nexo si está abierto.
2. Copia todo el contenido de esta carpeta dentro de `C:\Dev\Nexo`.
3. Acepta combinar carpetas y reemplazar archivos.
4. Ejecuta:

```powershell
cd C:\Dev\Nexo
dotnet restore .\Nexo.slnx
dotnet build .\Nexo.slnx
dotnet test .\Nexo.slnx
dotnet run --project .\src\Nexo.App\Nexo.App.csproj
```

El proyecto tenía 115 pruebas. Este parche agrega siete casos, así que deberían aparecer cerca de **122 pruebas correctas**.

## Qué probar

Primero abre **Personalización → Voz** y elige el micrófono que realmente usas.

Después prueba:

```text
Nexo, abre PowerShell
Nexo, muestra Peek
Oye Nexo, qué día es hoy
Nexo, sube Spotify al 70
```

También habla más bajo o desde un poco más lejos. Si Nexo considera que la grabación no fue clara, debería preguntar antes de actuar. En ese caso prueba:

```text
Nexo, confirmar
```

o:

```text
Nexo, cancelar
```

## Todavía pendiente

- Calibración guiada del micrófono.
- Diccionario personal de correcciones.
- Modelo Whisper preciso como segundo intento.
- Alternativas visibles cuando haya dos interpretaciones posibles.

No hagas commit hasta comprobar el selector, la frase completa y las pruebas.
