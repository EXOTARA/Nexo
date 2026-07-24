# Kohana — Visión de producto (1.0)

> Documento normativo. Registra decisiones tomadas por el propietario del producto el 2026-07-22.
> Si un cambio de código contradice este documento, el código está equivocado o el documento debe
> actualizarse **explícitamente** mediante un ADR. No se resuelve por interpretación.

## Definición

**Kohana es un agente personal nativo de Windows que escucha, ve, recuerda, organiza y actúa de
forma segura sobre el equipo, adaptándose automáticamente al hardware y manteniendo al usuario en
control.**

Diferencia esencial frente a plataformas tipo OpenClaw:

| | OpenClaw | Kohana |
|---|---|---|
| Naturaleza | Plataforma general de agentes que *puede* ejecutarse en Windows | Agente diseñado *desde* Windows |
| Control | Canales de mensajería externa (WhatsApp/Telegram/Discord) | Superficie local (Hub/Peek/Capsule/voz) |
| Superficie de ataque | Amplia por diseño | Mínima por diseño |
| Ejecución | Shell arbitrario generado por el modelo | Acciones tipadas con permiso verificado |

## Jerarquía de prioridad (desempate obligatorio)

1. **Velocidad y baja latencia**
2. **Calidad de la interacción y naturalidad de la voz**
3. **Privacidad y funcionamiento local**
4. **Amplitud de integraciones**

Reglas derivadas, aplicables sin consultar:

- Una solución local extremadamente lenta **no gana** por ser local.
- Una integración adicional **no justifica** aumentar latencia, consumo o superficie de ataque.
- Una voz más natural **no debe** bloquear comandos locales ni acaparar la GPU durante un juego.
- El sistema **debe hacer visible** cuándo algo ocurre localmente y cuándo sale a internet.

## Decisiones registradas

### A. Entorno de ejecución y fuentes de verdad
Dos fuentes, ambas obligatorias:
1. **Claude Code sobre el repositorio local en Windows** — compilación, pruebas y mediciones reales
   de hardware, audio y latencia.
2. **GitHub Actions en `windows-latest`** — verificación independiente de build, tests, publicación
   y seguridad.

Antes de modificar código, todo agente debe confirmar: sistema operativo, ruta del repositorio,
rama, `dotnet --info`, `git status` y pruebas iniciales.

**Prohibido inventar benchmarks** en entornos sin Windows, micrófono o GPU accesibles. Si el entorno
no permite medir, el trabajo se limita a diseño y documentación.

### B. Windows mínimo
**Windows 11 24H2 (build 26100) o posterior.**

Windows 10 terminó su ciclo general de soporte y **no debe** limitar la arquitectura. Puede
documentarse compatibilidad experimental cuando sea viable, pero nunca bloqueando APIs modernas de
voz, OCR, seguridad o automatización.

Consecuencia técnica directa: TFM objetivo `net10.0-windows10.0.26100.0` (ver `ADR/0003`).

### C. Licencia y modelo de producto
**Open source, licencia permisiva, preferentemente MIT** para el código propio.

Reglas para dependencias:
- Aceptar **MIT, Apache 2.0 o BSD** compatibles.
- **Evitar** modelos o dependencias con restricción no comercial (excluye p. ej. Moondream2).
- Documentar licencia, origen y versión de **cada** modelo.
- **No redistribuir pesos** sin verificar que esté permitido.
- **No construir** el producto sobre una dependencia comercial obligatoria (excluye Porcupine como
  dependencia; permitido solo como referencia de benchmark).

### D. Las tres experiencias impecables del día 1
Estas tres definen el listón de calidad. Si alguna falla, no hay RC.

1. **"Oye Kohana, abre X"** — entiende, ejecuta localmente y confirma brevemente, **sin llamar al LLM**.
2. **"Oye Kohana, ¿qué es esto?"** — analiza la ventana respetando privacidad, explica el resultado y
   puede responder por voz **de forma interrumpible**.
3. **Instalación limpia** — instalar → onboarding → detección de hardware → configuración recomendada
   → primera orden, **sin terminal**.

### E. Frases de activación
| Frase | Estado en 1.0 |
|---|---|
| `Oye Kohana` | **Estable** |
| `Kohana` | **Estable** |
| `Ey Kohana` | Experimental hasta superar benchmarks |
| `Hey Kohana` | Experimental hasta superar benchmarks |

Cada frase se evalúa y calibra **por separado**. Está prohibido asumir que un mismo modelo y umbral
sirven para todas.

### F. Permisos por defecto
Ver matriz completa y ejecutable en `SECURITY_MODEL.md`. Resumen:

**Sin confirmación** (cuando la petición viene directamente del usuario): abrir aplicaciones, abrir
carpetas conocidas, mostrar configuraciones, cambiar volumen, controlar audio, crear tareas, iniciar
enfoque, ejecutar rutinas previamente aprobadas, captura bajo demanda, consultar estado del sistema.

**Distinción crítica:** abrir PowerShell **no** requiere confirmación; ejecutar un comando arbitrario
dentro de PowerShell **sí** la requiere.

**Siempre confirmar:** escribir/modificar/borrar archivos, ejecutar scripts o comandos arbitrarios,
instalar software, enviar información a internet, publicar/enviar/comprar, usar credenciales,
acciones irreversibles, skill que solicite `network:true`, elevar privilegios.

### G. Memoria
**Opt-in por defecto.** Kohana **no escribe memoria automáticamente**.

Puede sugerir: *"Esto parece útil para el futuro. ¿Quieres que lo recuerde?"* — pero requiere
aprobación explícita antes de escribir.

El **diario automático** puede existir como opción explícita, **desactivada por defecto**.

Comandos obligatorios: recordar esto · no recuerdes esto · qué recuerdas · qué memoria utilizaste ·
editar · olvidar · exportar · borrar · sesión privada.

### H. Dataset de audio
Permitido **solo como modo voluntario y local**:
- desactivado por defecto;
- audio visible para el usuario;
- exportación manual;
- **nunca** subida automática;
- posibilidad de escuchar, revisar y borrar cada muestra;
- consentimiento **separado** para compartirlo.

### I. Firma, publicación y actualizaciones
**No existe certificado Authenticode actualmente.** La infraestructura de firma debe quedar
preparada, pero no bloquea la RC.

Distribución inicial: **GitHub Releases**. Sitio propio más adelante.

Mientras no exista firma:
- Kohana **notifica** que hay actualización;
- muestra versión, notas y hash;
- el usuario **confirma** descarga e instalación;
- **prohibida** la actualización silenciosa.

La actualización automática opcional se habilitará solo cuando existan firma y rollback probado.

### J. Telemetría y crashes
**Sin telemetría remota por defecto.**
- Logs técnicos **locales**, anonimizados.
- Exportación **manual**; el usuario decide compartirlos.
- **Nunca** enviar audio, capturas, memoria, conversaciones, tokens ni contenido del portapapeles.

### K. Skills oficiales para 1.0
**Estables:** 1) Windows y Sistema · 2) Archivos y carpetas · 3) Audio y multimedia ·
4) Tareas, enfoque y rutinas · 5) Vision y diagnóstico.

**Experimental:** 6) Herramientas de desarrollo (VS Code, Git, GitHub).

**No habrá marketplace comunitario en 1.0.**

## Fuera de alcance para 1.0 (decidido, no pendiente)

- Puentes de mensajería (WhatsApp/Telegram/Discord) como canal de control.
- Marketplace comunitario de skills de terceros.
- Automatización de navegador (queda experimental).
- Emparejamiento de dispositivos.
- Clonación de voz sin consentimiento y reglas explícitas.
