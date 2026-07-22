# Aplicar Kohana 0.9.4-beta

1. Cierra Kohana desde la bandeja.
2. Extrae el ZIP sobre `C:\Dev\Nexo` conservando `.git`.
3. Limpia `bin` y `obj`.
4. Ejecuta `dotnet restore`, `dotnet test` y `dotnet build` en Release.
5. Abre `src\Nexo.App\bin\Release\net10.0-windows\Kohana.exe`.

## Pruebas prioritarias

- Expandir y contraer la barra lateral varias veces; no debe quedar espacio residual.
- Confirmar que el botón del menú y Ctrl + Espacio tienen símbolos distintos.
- Probar “Ey Kohana” en sensibilidad Equilibrada y Alta.
- Usar el botón **Probar frase**: debe confirmar la detección sin iniciar una orden.
- Reiniciar Kohana y comprobar que conserva el estado de la barra lateral y la sensibilidad.
