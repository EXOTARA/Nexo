# Plan de migración — Kohana 1.0

## 1. Datos de usuario: Nexo → Kohana

Comportamiento actual (**ya implementado y correcto — conservar**):
- Copia, **no** mueve
- **No** elimina el origen
- **No** sobrescribe archivos existentes
- Excluye logs, temporales, modelos y runtimes pesados
- Reutiliza modelos y runtime heredados sin duplicarlos
- Deja marcador de migración
- Puede reintentarse sin duplicar datos

**No modificar en esta actualización.** Solo añadir cobertura de pruebas.

## 2. Esquema de preferencias

Actual: **v16**. Incremental, con escritura atómica (`.tmp` + `File.Move`).

| Versión | Cambio | Fase |
|---|---|---|
| v17 | `PerformancePreference` (Automático/Ahorro/Equilibrado/Máximo) | 2 |
| v18 | Preferencias de motor por subsistema | 4 |
| v19 | Memoria: opt-in, diario automático (por defecto `false`) | 6 |
| v20 | Permisos por skill | 8 |

Reglas: cada incremento es aditivo · valores nuevos con default seguro · nunca borrar datos del
usuario en una migración · toda migración necesita prueba.

**Mejora pendiente:** conservar `settings.json.bak` antes de escribir. Hoy la escritura es atómica
frente a fallos de E/S, pero un JSON válido y semánticamente corrupto no es recuperable
(escenario 4 de `TEST_MATRIX`).

## 3. Cambio de TFM (Fase 7)

```
net10.0-windows  →  net10.0-windows10.0.26100.0
```

Habilita WinRT: `Windows.Media.Ocr` y APIs modernas de audio.
Requiere Windows 11 24H2+ (decisión B). Ver `ADR/0003`.

Riesgos: paquetes que no resuelvan con el nuevo TFM · CI debe usar imagen con SDK de Windows
adecuado · el instalador debe declarar el requisito mínimo.

Mitigación: cambiar el TFM **solo** en `Nexo.Windows` y `Nexo.App`. **`Nexo.Core` permanece en
`net10.0` puro** — es la garantía de que la lógica de dominio sigue siendo testeable sin Windows.

## 4. Namespaces internos `Nexo.*`

**Decisión: conservarlos.** Renombrar añade riesgo (rutas, CI, instalador, historial) sin beneficio
para el usuario, que nunca los ve. Ya se verificó que no hay fugas de "Nexo" en la UI: las únicas
coincidencias son claves de estilo internas (`NexoSliderStyle`).

Migración futura, fuera de 1.0: renombrar en un sprint dedicado, con el instalador y el sistema de
releases ya validados.

## 5. Orden de migración en el arranque

1. Detectar carpeta heredada de Nexo → copiar si aplica
2. Cargar `settings.json` (o `.bak` si el primario está corrupto)
3. Aplicar migraciones de esquema en orden
4. Escribir la versión resultante atómicamente
5. Detectar perfil de hardware (primera vez) → guardar
6. Continuar el arranque

Si cualquier paso falla: arrancar con valores seguros, avisar al usuario, **no borrar nada**.
