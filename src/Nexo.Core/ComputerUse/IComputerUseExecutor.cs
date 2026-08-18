namespace Nexo.Core.ComputerUse;

public sealed record ComputerUseStepResult(bool Success, string Detail, string Output = "")
{
    public static ComputerUseStepResult Ok(string detail, string output = "") => new(true, detail, output);

    public static ComputerUseStepResult Failed(string detail) => new(false, detail);
}

/// <summary>
/// Diseño D18 (Fase 7, nivel 4) — lo que de verdad toca el equipo. Una interfaz aparte de
/// <see cref="IComputerUseMethodProbe"/> por el mismo motivo por el que el proyecto separó lector y
/// escritor: quien solo puede consultar qué métodos hay no puede ejecutar ni por accidente.
///
/// **No decide nada.** Permisos, nivel de autonomía, snapshot y verificación son de
/// <see cref="ComputerUseCoordinator"/>. Aquí solo se actúa.
/// </summary>
public interface IComputerUseExecutor
{
    /// <summary>Lo que hay ahora en el portapapeles, para poder devolverlo. Null si no se pudo leer.</summary>
    string? ReadClipboard();

    ComputerUseStepResult SetClipboard(string text);

    /// <summary>Ejecuta un comando de <see cref="SafeShellCatalog"/>. Nunca uno compuesto fuera de ella.</summary>
    ComputerUseStepResult RunSafeCommand(SafeShellCommand command);
}
