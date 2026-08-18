using System.Globalization;

namespace Nexo.Core.Media;

/// <summary>
/// Diseño D43 — de qué programa sale la música, dicho como lo diría una persona.
///
/// Windows identifica al reproductor con su identificador de aplicación, que unas veces es un
/// ejecutable —«Spotify.exe»— y otras un identificador de tienda de sesenta caracteres. Ninguno de
/// los dos se puede enseñar tal cual bajo una carátula.
/// </summary>
public static class MediaSourceName
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spotify.exe"] = "Spotify",
        ["spotifyab.spotifymusic"] = "Spotify",
        ["msedge.exe"] = "Microsoft Edge",
        ["chrome.exe"] = "Google Chrome",
        ["firefox.exe"] = "Firefox",
        ["brave.exe"] = "Brave",
        ["opera.exe"] = "Opera",
        ["vlc.exe"] = "VLC",
        ["foobar2000.exe"] = "foobar2000",
        ["wmplayer.exe"] = "Reproductor de Windows Media",
        ["microsoft.zunemusic"] = "Media Player",
        ["itunes.exe"] = "iTunes",
        ["applemusic.exe"] = "Apple Music",
        ["tidal.exe"] = "TIDAL",
        ["deezer.exe"] = "Deezer",
        ["amazonmusic.exe"] = "Amazon Music",
        ["discord.exe"] = "Discord",
        ["mpc-hc64.exe"] = "MPC-HC"
    };

    public static string Describe(string? appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId))
        {
            return string.Empty;
        }

        var id = appUserModelId.Trim();
        if (Known.TryGetValue(id, out var exact))
        {
            return exact;
        }

        // Los identificadores de la tienda vienen como «Editor.Paquete_hash!App». La parte útil es
        // el nombre del paquete, y el hash del editor no dice nada a nadie.
        var bang = id.IndexOf('!');
        if (bang > 0)
        {
            id = id[..bang];
        }

        var underscore = id.IndexOf('_');
        if (underscore > 0)
        {
            id = id[..underscore];
        }

        if (Known.TryGetValue(id, out var byPackage))
        {
            return byPackage;
        }

        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^4];
        }

        var dot = id.LastIndexOf('.');
        if (dot >= 0 && dot < id.Length - 1)
        {
            id = id[(dot + 1)..];
        }

        return id.Length == 0
            ? string.Empty
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.ToLowerInvariant());
    }
}
