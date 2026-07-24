# Checklist de lanzamiento de Kohana

## Código

- [ ] Kohana y Nexo están cerrados completamente.
- [ ] `dotnet restore .\Nexo.slnx` pasa.
- [ ] `dotnet test .\Nexo.slnx -c Release` pasa.
- [ ] `dotnet build .\Nexo.slnx -c Release` pasa.
- [ ] No hay cambios sin commit.
- [ ] `CHANGELOG.md` contiene la versión que se publicará.
- [ ] El número de versión coincide con la etiqueta.

## Identidad y migración

- [ ] La ventana, Capsule, Peek, bandeja y onboarding muestran Kohana.
- [ ] El ejecutable generado es `Kohana.exe`.
- [ ] El icono de aplicación se ve a 16, 32 y 256 px.
- [ ] `Oye Kohana`, `Kohana`, `Ey Kohana`, `cojana` y `kojana` funcionan según la frase seleccionada.
- [ ] El modo Kohana no acepta `Nexo` por error.
- [ ] La prueba muestra exactamente lo que entendió Vosk.
- [ ] Los aliases personales se guardan sin audio.
- [ ] `%LocalAppData%\Nexo` se copia a `%LocalAppData%\Kohana` sin borrar el origen.
- [ ] Una segunda ejecución no duplica ni sobrescribe la migración.

## Runtime

- [ ] Sistema muestra estado de voz, IA, Vision y rendimiento.
- [ ] Reiniciar voz funciona sin reiniciar Kohana.
- [ ] Diagnóstico abre desde el panel Runtime.

## Artefactos

- [ ] `scripts\publish.ps1` genera `Kohana.exe`.
- [ ] `scripts\verify-release.ps1` no encuentra datos privados.
- [ ] El SHA-256 del ZIP coincide.
- [ ] El ZIP portable abre después de extraerse en otra carpeta.
- [ ] El instalador se crea con Inno Setup 6.
- [ ] El instalador funciona sin permisos de administrador.
- [ ] Los accesos directos abren Kohana.
- [ ] La bandeja, `Alt + A`, Peek y Look Mode funcionan instalados.
- [ ] La desinstalación elimina la aplicación.
- [ ] El usuario puede conservar o borrar sus datos locales de Kohana.

## Prueba limpia

- [ ] La primera ejecución muestra onboarding.
- [ ] Ollama ausente o detenido produce un mensaje claro, no un cierre.
- [ ] El modo local funciona sin modelos descargados.
- [ ] Micrófono desconectado produce una explicación clara.
- [ ] Tareas, enfoque y rutinas persisten.
- [ ] Vision funciona en al menos un monitor y con escalado 125 %.
- [ ] `Windows + Shift + S` no activa Modo Juego.
- [ ] Un juego real a pantalla completa sí activa Modo Juego.
- [ ] Buscar actualizaciones utiliza el repositorio configurado.

## Publicación

- [ ] Se creó una etiqueta `vX.Y.Z-sufijo`.
- [ ] GitHub Actions terminó en verde.
- [ ] La release está marcada como beta/prerelease.
- [ ] ZIP, instalador y archivos SHA-256 están adjuntos.
- [ ] Las notas no incluyen claves, rutas personales ni capturas privadas.
