namespace Nexo.Core.Shell;

/// <summary>Cómo se señala el elemento activo del rail de navegación.</summary>
public enum NavigationSelectionStyle
{
    /// <summary>No es el activo: nada que señalar.</summary>
    None,

    /// <summary>
    /// Plegado. Solo hay un icono y no cabe nada más, así que el activo se señala haciéndolo
    /// brillar: una pastilla rellena en una franja de 56 píxeles ocupa casi todo el ancho y
    /// convierte una pista en un bloque de color.
    /// </summary>
    Glow,

    /// <summary>
    /// Desplegado. Con el nombre al lado hay sitio para una pastilla que abraza icono y texto, que
    /// es lo que deja claro dónde acaba el elemento activo cuando hay varios seguidos.
    /// </summary>
    Pill
}

/// <summary>
/// Diseño D36 — la regla del boceto: «cuando no esté desplegado simplemente brillaría la pestaña
/// seleccionada».
///
/// Vive aquí y no en la vista porque es una decisión de diseño, no de dibujo: el rail se pliega y se
/// despliega desde varios sitios —el botón, restaurar apariencia, el arranque— y cada uno tenía que
/// acordarse de aplicar el estilo correcto. Con la regla en un solo sitio, no hay forma de que dos
/// caminos discrepen.
/// </summary>
public static class NavigationSelection
{
    public static NavigationSelectionStyle For(bool isSelected, bool railExpanded)
    {
        if (!isSelected)
        {
            return NavigationSelectionStyle.None;
        }

        return railExpanded
            ? NavigationSelectionStyle.Pill
            : NavigationSelectionStyle.Glow;
    }
}
