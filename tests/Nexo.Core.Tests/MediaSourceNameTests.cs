using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D43 — el nombre del reproductor bajo la carátula. Windows lo entrega en tres formatos
/// distintos y ninguno se puede enseñar tal cual.
/// </summary>
public sealed class MediaSourceNameTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("chrome.exe", "Google Chrome")]
    [InlineData("msedge.exe", "Microsoft Edge")]
    public void Describe_KnownExecutable_UsesItsRealName(string id, string expected) =>
        Assert.Equal(expected, MediaSourceName.Describe(id));

    [Fact]
    public void Describe_StorePackage_DropsThePublisherHash()
    {
        // El identificador de la aplicación de la tienda de Spotify trae el hash del editor y el
        // punto de entrada. Enseñarlo entero sería una línea de sesenta caracteres bajo el título.
        Assert.Equal(
            "Spotify",
            MediaSourceName.Describe("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"));
    }

    [Fact]
    public void Describe_UnknownExecutable_ReadsAsAName()
    {
        // De un reproductor que no está en la lista sale lo mejor que se puede sacar sin inventar:
        // el ejecutable sin extensión y con mayúscula. «Musicbee.exe» no es un nombre; «Musicbee» sí.
        Assert.Equal("Musicbee", MediaSourceName.Describe("MusicBee.exe"));
    }

    [Fact]
    public void Describe_WithoutId_IsEmpty()
    {
        // Vacío y no «Desconocido»: quien lo pinta decide si oculta la etiqueta, y una etiqueta que
        // dice «Desconocido» ocupa el mismo sitio sin aportar nada.
        Assert.Equal(string.Empty, MediaSourceName.Describe(null));
        Assert.Equal(string.Empty, MediaSourceName.Describe("   "));
    }
}
