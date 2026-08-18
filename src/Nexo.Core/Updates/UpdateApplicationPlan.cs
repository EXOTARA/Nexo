namespace Nexo.Core.Updates;

/// <summary>Cada paso de aplicar una actualización, en el orden en que ocurre.</summary>
public enum UpdateStep
{
    /// <summary>Comprobar que lo descargado es lo publicado.</summary>
    VerifyPackage,

    /// <summary>Desempaquetar al lado de la instalación, sin tocarla.</summary>
    StageBesideInstall,

    /// <summary>Comprobar que lo desempaquetado arranca de verdad.</summary>
    VerifyStaged,

    /// <summary>Apartar la instalación actual, conservándola entera.</summary>
    SetAsidePrevious,

    /// <summary>Poner lo nuevo en su sitio.</summary>
    PromoteStaged,

    /// <summary>Borrar lo apartado, una vez que lo nuevo ha arrancado bien.</summary>
    DiscardPrevious
}

/// <summary>Qué hacer cuando un paso falla.</summary>
public enum UpdateRecovery
{
    /// <summary>Dejarlo estar: no se ha tocado nada de la instalación.</summary>
    NothingToUndo,

    /// <summary>Borrar lo desempaquetado y quedarse como estaba.</summary>
    DiscardStaged,

    /// <summary>Devolver lo apartado a su sitio: la instalación está a medias.</summary>
    RestorePrevious
}

/// <summary>
/// Diseño D62 — el orden en que se aplica una actualización, y qué se deshace en cada punto.
///
/// El roadmap lo dice sin rodeos: *«un actualizador mal diseñado puede romper instalaciones
/// existentes»*. Lo que hace peligroso a un actualizador no es fallar —fallará— sino fallar **a
/// mitad**, dejando media instalación vieja y media nueva. Eso no se arregla reintentando: se evita
/// decidiendo de antemano qué se puede deshacer en cada punto, que es lo que hay aquí.
///
/// El orden no es casual. **Todo lo que puede fallar sin consecuencias se hace primero**, mientras
/// la instalación sigue intacta: verificar, desempaquetar al lado, comprobar que lo desempaquetado
/// sirve. Solo cuando ya no queda nada por comprobar se toca lo instalado, y ese tramo se hace con
/// movimientos de carpeta —rápidos y en el mismo disco— en vez de copiando archivo por archivo, que
/// es lo que deja una instalación a medias si se corta la luz.
///
/// **Lo viejo no se borra al terminar de copiar, sino cuando lo nuevo ha arrancado.** Un paquete
/// puede estar íntegro, desempaquetarse bien y aun así no arrancar en este equipo concreto; si para
/// entonces ya se borró lo anterior, no queda a dónde volver.
/// </summary>
public static class UpdateApplicationPlan
{
    /// <summary>Los pasos, en orden. Lo reversible primero; lo irreversible, lo último.</summary>
    public static IReadOnlyList<UpdateStep> Steps { get; } =
    [
        UpdateStep.VerifyPackage,
        UpdateStep.StageBesideInstall,
        UpdateStep.VerifyStaged,
        UpdateStep.SetAsidePrevious,
        UpdateStep.PromoteStaged,
        UpdateStep.DiscardPrevious
    ];

    /// <summary>
    /// Qué hay que deshacer si <paramref name="failed"/> no sale bien.
    ///
    /// Se pregunta por el paso que falló y no por «cuánto llevamos hecho» a propósito: el estado a
    /// deshacer depende de dónde se estaba, y llevar esa cuenta aparte es cómo se acaba deshaciendo
    /// de más o de menos.
    /// </summary>
    public static UpdateRecovery RecoveryFor(UpdateStep failed) => failed switch
    {
        // Nada se ha tocado todavía: lo descargado se tira y ya está.
        UpdateStep.VerifyPackage => UpdateRecovery.NothingToUndo,

        // Hay una carpeta al lado que sobra, pero la instalación no se ha rozado.
        UpdateStep.StageBesideInstall or UpdateStep.VerifyStaged => UpdateRecovery.DiscardStaged,

        // A partir de aquí la instalación está movida y hay que devolverla.
        UpdateStep.SetAsidePrevious or UpdateStep.PromoteStaged => UpdateRecovery.RestorePrevious,

        // Lo nuevo ya está puesto y funcionando; que no se pueda borrar lo viejo es un estorbo en
        // disco, no una avería. Volver atrás aquí sería desinstalar una versión que va bien.
        _ => UpdateRecovery.NothingToUndo
    };

    /// <summary>
    /// Si a partir de este paso la instalación ha dejado de estar intacta. Marca la frontera entre
    /// «cancelar sale gratis» y «cancelar cuesta deshacer».
    /// </summary>
    public static bool TouchesInstallation(UpdateStep step) =>
        step is UpdateStep.SetAsidePrevious or UpdateStep.PromoteStaged or UpdateStep.DiscardPrevious;

    /// <summary>
    /// Si se puede cancelar sin consecuencias estando en ese paso.
    ///
    /// Importa para la interfaz: un botón de cancelar que sigue ahí cuando ya no se puede cancelar
    /// es peor que no tenerlo, porque promete algo que no va a cumplir.
    /// </summary>
    public static bool CanCancelAt(UpdateStep step) => !TouchesInstallation(step);

    /// <summary>Cómo se le cuenta a la persona lo que está pasando.</summary>
    public static string Describe(UpdateStep step) => step switch
    {
        UpdateStep.VerifyPackage => "Comprobando la descarga",
        UpdateStep.StageBesideInstall => "Preparando la versión nueva",
        UpdateStep.VerifyStaged => "Comprobando que la versión nueva sirve",
        UpdateStep.SetAsidePrevious => "Guardando la versión actual",
        UpdateStep.PromoteStaged => "Instalando la versión nueva",
        _ => "Terminando"
    };
}
