namespace Nexo.Core.Settings;

/// <summary>
/// Diseño D31 — de dónde sale el acento de Sakura. Empezó siendo un sí/no ("¿seguir a Windows?") y
/// dejó de bastar en cuanto apareció el tercer origen: un booleano no puede representar tres
/// estados sin convertirse en dos banderas que pueden contradecirse entre sí.
/// </summary>
public enum AccentSource
{
    /// <summary>El color que la persona eligió en la galería de temas.</summary>
    Manual = 0,

    /// <summary>El color de acento vigente de Windows.</summary>
    Windows = 1,

    /// <summary>El color dominante del fondo de escritorio, extraído de la imagen.</summary>
    Wallpaper = 2
}
