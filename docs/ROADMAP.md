# Roadmap de Kohana

Kohana busca convertirse en un agente personal nativo para Windows: capaz de escuchar, ver, recordar, organizar y actuar, pero siempre con permisos comprensibles y confirmación para acciones sensibles.

## Principios del producto

- **Windows primero:** .NET y WPF nativos; sin depender de Electron o WSL para el uso normal.
- **Local por defecto:** comandos conocidos, voz y datos personales permanecen locales cuando sea posible.
- **Control humano:** las acciones sensibles se previsualizan, aprueban y registran.
- **Modular:** cada capacidad futura se integra como skill o acción tipada.
- **Ligero:** Kohana reduce actividad durante juegos o cargas elevadas.
- **Sakura Fluent:** identidad orgánica, clara y consistente con Windows.

## Completado hasta 0.9.1

- Shell modular, Inicio, Peek, Capsule y paleta de comandos.
- Métricas reales y Resource Governor.
- Audio general y por aplicación.
- Whisper local, Vosk y wake word.
- Proveedores de IA y runtime privado de Ollama.
- Vision bajo demanda y diagnóstico visual.
- Tareas, enfoque, rutinas y acciones seguras.
- Bandeja, inicio con Windows, instancia única y recuperación.
- Onboarding, administración de modelos y diagnóstico local.
- Portable autocontenido, scripts de publicación, CI e instalador.

## 0.9.3-beta — Sakura Shell + Chat Refresh

- Integración del sistema visual de Figma.
- Iconografía modular coherente.
- Chat renovado con acciones rápidas.
- Paleta neutral Sakura Premium.
- Compatibilidad de voz Kohana/Nexo corregida.

## 0.9.2-beta — Kohana Brand Foundation

- Nombre público Kohana y lema “Tu Windows, en flor”.
- Identidad centralizada para evitar nombres duplicados en el código.
- Logo de sakura, icono de bandeja y paleta Sakura Fluent.
- `Kohana.exe`, portable e instalador Kohana.
- Wake words Kohana con compatibilidad temporal para Nexo.
- Migración conservadora de `%LocalAppData%\Nexo`.
- Documentación y workflows renombrados.

## 0.9.3-beta — Setup y onboarding definitivo

- Instalador probado en una cuenta limpia.
- Actualización conservando datos y posibilidad de recuperación.
- Selección clara entre modo local, híbrido y nube.
- Calibración guiada del micrófono.
- Descarga del modelo recomendado sin terminal.
- Permisos explicados para voz, Vision, archivos y notificaciones.
- Primera orden guiada y verificación final.

## 0.10 — Kohana Runtime

- Runtime persistente separado visualmente del Hub.
- Sesiones, trabajos largos y actividad en segundo plano.
- Memoria personal controlable: recordar, olvidar, exportar y borrar.
- Recuperación de trabajos después de reiniciar.
- Panel de actividad con resultados y cancelación.
- Perfiles de rendimiento y salud del runtime.

## 0.11 — Acciones, skills y permisos

- Planes de varias acciones antes de ejecutar.
- Niveles `Bloqueado`, `Preguntar` y `Permitido`.
- Manifiestos de skills con permisos, red y datos usados.
- Registro de cada acción y resultado por paso.
- Integraciones iniciales con archivos, VS Code, PowerShell y GitHub.
- Almacenamiento de secretos mediante mecanismos seguros de Windows.

## 0.12 — Automatizaciones persistentes

- Programaciones únicas y recurrentes.
- Disparadores de Windows y aplicaciones.
- Reintentos, pausas e historial de ejecuciones.
- Rutinas generadas como planes seguros y editables.
- Notificaciones cuando una automatización requiere aprobación.

## 0.13 — Navegador y servicios conectados

- Navegador aislado para investigación y formularios.
- Gmail, calendario, Telegram y Discord mediante conexiones opcionales.
- Preparación de mensajes, eventos y archivos antes de enviarlos.
- Confirmación obligatoria para publicar, eliminar, comprar o enviar.

## 0.14 — Agentes y dispositivos

- Agentes especializados para programación, estudio y organización.
- Memorias y permisos separados por agente.
- Delegación de tareas con límites de tiempo y costo.
- Emparejamiento seguro con otros dispositivos.

## 1.0 — Estable

- Firma digital y actualizaciones confiables.
- Instalación, reparación y desinstalación probadas.
- Auditoría de privacidad y seguridad.
- Accesibilidad completa y navegación por teclado.
- Documentación pública y canal estable.
- Telemetría únicamente opcional y claramente explicada.
