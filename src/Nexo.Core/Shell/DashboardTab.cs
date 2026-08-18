namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D43 — las pestañas del panel superior, en el orden de la referencia.
/// </summary>
public enum DashboardTab
{
    Panel = 0,
    Media = 1,
    Performance = 2
}

/// <summary>
/// Diseño D43 — qué pestaña se enseña al abrir Sistema.
///
/// La regla no es «la última que usaste» sino «la que tiene algo que contar»: si hay música
/// sonando, Media; si no, la que el usuario dejó abierta. Un panel que siempre vuelve a la primera
/// pestaña obliga a dos clics cada vez para llegar a lo mismo, y uno que recuerda a ciegas enseña
/// un reproductor vacío cuando no hay nada que reproducir.
/// </summary>
public static class DashboardTabPolicy
{
    public static DashboardTab Resolve(DashboardTab lastUsed, bool somethingIsPlaying)
    {
        if (somethingIsPlaying && lastUsed == DashboardTab.Media)
        {
            return DashboardTab.Media;
        }

        // Sin música, Media no se ofrece como punto de partida aunque fuera la última vista.
        return !somethingIsPlaying && lastUsed == DashboardTab.Media
            ? DashboardTab.Panel
            : lastUsed;
    }
}
