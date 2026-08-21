using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// El JSON de ejemplo reproduce la forma real del <c>config.json</c> de Wallpaper Engine, incluido
/// que el primer nivel es el nombre de la cuenta de Windows —lo que obliga a buscar por forma y no
/// por una ruta fija.
/// </summary>
public sealed class LiveWallpaperConfigTests
{
    private const string RealShapeConfig = """
    {
      "Usuario": {
        "general": {
          "browser": { "lastselectedmonitor": "Monitor0" },
          "wallpaperconfig": {
            "selectedwallpapers": {
              "Monitor0": { "file": "C:/Steam/workshop/431960/3411496191/scene.pkg" }
            }
          }
        }
      },
      "defaultuser100000": {
        "general": { "user": { "monitordetection": "managed" } }
      }
    }
    """;

    [Fact]
    public void FindsTheSelectedWallpaperWhateverTheAccountIsCalled()
    {
        var found = LiveWallpaperConfig.ReadSelectedWallpapers(RealShapeConfig);

        Assert.Single(found);
        Assert.Equal("C:/Steam/workshop/431960/3411496191/scene.pkg", found[0]);
    }

    [Fact]
    public void FindsOneWallpaperPerMonitor()
    {
        const string twoMonitors = """
        {
          "Alguien": {
            "general": {
              "wallpaperconfig": {
                "selectedwallpapers": {
                  "Monitor0": { "file": "D:/a/scene.pkg" },
                  "Monitor1": { "file": "D:/b/video.mp4" }
                }
              }
            }
          }
        }
        """;

        var found = LiveWallpaperConfig.ReadSelectedWallpapers(twoMonitors);

        Assert.Equal(2, found.Count);
        Assert.Contains("D:/a/scene.pkg", found);
        Assert.Contains("D:/b/video.mp4", found);
    }

    [Fact]
    public void AConfigWithoutAnySelectedWallpaperYieldsNothing()
    {
        var found = LiveWallpaperConfig.ReadSelectedWallpapers("""{"Usuario":{"general":{}}}""");

        Assert.Empty(found);
    }

    [Fact]
    public void BrokenJsonIsTreatedAsNoWallpaperInsteadOfThrowing()
    {
        // Wallpaper Engine puede estar reescribiendo su config justo cuando Sakura lo lee. Que eso
        // tire la aplicación al arrancar sería mucho peor que quedarse sin este origen de color.
        var found = LiveWallpaperConfig.ReadSelectedWallpapers("{ esto no es json");

        Assert.Empty(found);
    }

    [Fact]
    public void AnEntryWithoutAFileFieldIsSkippedRatherThanCrashing()
    {
        const string missingFile = """
        {
          "Usuario": {
            "general": {
              "wallpaperconfig": {
                "selectedwallpapers": {
                  "Monitor0": { "otracosa": 1 },
                  "Monitor1": { "file": "D:/b/scene.pkg" }
                }
              }
            }
          }
        }
        """;

        var found = LiveWallpaperConfig.ReadSelectedWallpapers(missingFile);

        Assert.Single(found);
        Assert.Equal("D:/b/scene.pkg", found[0]);
    }

    [Theory]
    [InlineData("C:/Steam/workshop/431960/3411496191/scene.pkg", "C:/Steam/workshop/431960/3411496191")]
    [InlineData(@"C:\Steam\workshop\431960\1\video.mp4", "C:/Steam/workshop/431960/1")]
    public void ThePreviewLivesNextToTheWallpaperFile(string wallpaper, string expectedFolder)
    {
        Assert.Equal(expectedFolder, LiveWallpaperConfig.PreviewFolderOf(wallpaper));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("scene.pkg")]
    public void APathWithoutAFolderYieldsNothing(string wallpaper)
    {
        Assert.Null(LiveWallpaperConfig.PreviewFolderOf(wallpaper));
    }
}
