# ADR 0003 — TFM `net10.0-windows10.0.26100.0` y Windows 11 24H2 como mínimo

**Estado:** Aceptado · **Fecha:** 2026-07-22 · **Fase:** 7 (el cambio de TFM)

## Contexto

El TFM actual es `net10.0-windows`, que **no da acceso a WinRT**. Esto bloquea:
- `Windows.Media.Ocr` — paso 4 obligatorio del pipeline de Vision
- APIs modernas de audio necesarias para evaluar cancelación de eco (AEC)

## Decisión

**Windows 11 24H2 (build 26100) o posterior** como plataforma mínima de 1.0.
TFM objetivo: `net10.0-windows10.0.26100.0` en `Nexo.Windows` y `Nexo.App`.

**`Nexo.Core` permanece en `net10.0` puro**, sin dependencias y sin TFM de Windows. Esta es la
garantía estructural de que la lógica de dominio sigue siendo testeable sin Windows y de que las
353 pruebas actuales de `Nexo.Core` siguen corriendo en cualquier entorno.

Windows 10 terminó su ciclo general de soporte y no debe limitar la arquitectura. Puede documentarse
compatibilidad experimental si resulta viable, pero **nunca bloqueando** APIs modernas de voz, OCR,
seguridad o automatización.

## Consecuencias

**Positivas:** OCR nativo sin descargar modelos ni añadir dependencias · acceso a audio moderno ·
menor matriz de prueba · sin concesiones arquitectónicas.

**Negativas:** excluye Windows 10 · el instalador debe declarar y verificar el requisito · el CI debe
usar una imagen con el SDK de Windows adecuado.

**Momento del cambio:** Fase 7, **no antes**. Cambiarlo temprano añadiría riesgo de resolución de
paquetes a las fases de extracción, que son las más delicadas.
