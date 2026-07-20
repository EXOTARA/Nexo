# Checklist de lanzamiento

## Código

- [ ] Nexo está cerrado completamente.
- [ ] `dotnet build .\Nexo.slnx -c Release` pasa.
- [ ] `dotnet test .\Nexo.slnx -c Release` pasa.
- [ ] No hay cambios sin commit.
- [ ] `CHANGELOG.md` contiene la versión que se publicará.
- [ ] El número de versión coincide con la etiqueta.

## Artefactos

- [ ] `scripts\publish.ps1` genera `Nexo.exe`.
- [ ] `scripts\verify-release.ps1` no encuentra datos privados.
- [ ] El ZIP portable abre después de extraerse en otra carpeta.
- [ ] El instalador instala sin permisos de administrador.
- [ ] El acceso directo abre Nexo.
- [ ] La bandeja, Alt + A y Peek funcionan instalados.
- [ ] El desinstalador elimina la aplicación.
- [ ] El usuario puede conservar o borrar sus datos locales.

## Prueba limpia

- [ ] Primera ejecución muestra onboarding.
- [ ] Nexo detecta que Ollama no está instalado o cerrado sin crashear.
- [ ] Se puede usar el modo local sin modelos descargados.
- [ ] Micrófono desconectado produce una explicación clara.
- [ ] Tareas, enfoque y rutinas persisten.
- [ ] Vision funciona en al menos un monitor y con escalado 125 %.
- [ ] La actualización abre la página de Releases cuando el build contiene `RepositoryUrl`.

## Publicación

- [ ] Se creó una etiqueta `vX.Y.Z-sufijo`.
- [ ] GitHub Actions terminó en verde.
- [ ] Release marcada como beta/prerelease.
- [ ] ZIP, instalador y archivos SHA256 están adjuntos.
- [ ] Las notas de versión no incluyen claves, rutas personales ni capturas.
