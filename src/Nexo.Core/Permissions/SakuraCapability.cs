namespace Nexo.Core.Permissions;

/// <summary>
/// Diseño D16 — las capacidades que piden permiso. El modelo de confianza exige que sean
/// **independientes**: *"cada capacidad de la matriz tiene su propio permiso, independiente de las
/// demás — habilitar Lens no habilita Computer Use"*. Por eso son un enum y no banderas de un mismo
/// permiso general: un permiso general es como se acaba concediendo de más sin querer.
/// </summary>
public enum SakuraCapability
{
    /// <summary>Ver y explicar lo que hay en pantalla (Fase 2).</summary>
    Lens,

    /// <summary>Dictado global e inserción de texto en otras aplicaciones (Fase 3).</summary>
    Flow,

    /// <summary>Memoria personal entre sesiones (Fase 6).</summary>
    Memoria,

    /// <summary>Leer y modificar un proyecto autorizado (Fase 5).</summary>
    Proyecto,

    /// <summary>Cambiar ajustes del equipo (Fase 4).</summary>
    Optimizacion,

    /// <summary>Actuar sobre el equipo y otras aplicaciones (Fase 7).</summary>
    ComputerUse
}

/// <summary>
/// Diseño D16 — los tres niveles que nombra el roadmap de producto para la versión 0.11. No hay un
/// cuarto nivel "permitido para siempre y sin registro": lo permitido sigue quedando en el Audit Log.
/// </summary>
public enum PermissionLevel
{
    /// <summary>Ni se pregunta. Es el valor por omisión de lo más arriesgado.</summary>
    Bloqueado,

    /// <summary>Se pregunta cada vez. Es el valor por omisión de casi todo.</summary>
    Preguntar,

    /// <summary>Se hace sin preguntar… salvo que caiga en una confirmación obligatoria.</summary>
    Permitido
}

/// <summary>
/// Diseño D16 — las siete categorías que el modelo de confianza marca como confirmación obligatoria
/// *"sin importar el nivel de autonomía configurado"*. Existen como enum, y no como una comprobación
/// suelta dentro de cada capacidad, justamente porque tienen que ser imposibles de saltar: si cada
/// capacidad decidiera por su cuenta qué es "sensible", la primera que se olvidara sería un agujero.
/// </summary>
public enum MandatoryConfirmation
{
    DatosSensibles,

    Credenciales,

    Pagos,

    BorradoIrreversible,

    ElevacionAdministrativa,

    PublicacionExterna,

    ComandosDeSistemaAmplios
}

public static class MandatoryConfirmationText
{
    public static string Describe(MandatoryConfirmation category) => category switch
    {
        MandatoryConfirmation.DatosSensibles => "hay datos sensibles de por medio",
        MandatoryConfirmation.Credenciales => "hay credenciales o contraseñas de por medio",
        MandatoryConfirmation.Pagos => "tiene efecto sobre tu dinero",
        MandatoryConfirmation.BorradoIrreversible => "borra algo que no se puede recuperar",
        MandatoryConfirmation.ElevacionAdministrativa => "necesita permisos de administrador",
        MandatoryConfirmation.PublicacionExterna => "envía algo fuera de tu equipo",
        MandatoryConfirmation.ComandosDeSistemaAmplios => "cambia el sistema de forma amplia",
        _ => category.ToString()
    };
}
