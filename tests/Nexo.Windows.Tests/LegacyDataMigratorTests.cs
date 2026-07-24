using Nexo.Windows.Storage;

namespace Nexo.Windows.Tests;

public sealed class LegacyDataMigratorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"KohanaMigrationTests-{Guid.NewGuid():N}");

    [Fact]
    public void Migrate_CopiesPersonalDataButSkipsLogsAndTemp()
    {
        var source = Path.Combine(_root, "Nexo");
        var destination = Path.Combine(_root, "Kohana");
        Write(source, "settings.json", "{\"schemaVersion\":13}");
        Write(source, Path.Combine("Models", "voice.bin"), "model");
        Write(source, Path.Combine("Runtime", "Ollama", "ollama.exe"), "runtime");
        Write(source, Path.Combine("Logs", "old.log"), "private log");
        Write(source, Path.Combine("Temp", "capture.wav"), "temporary");

        var result = LegacyDataMigrator.Migrate(source, destination);

        Assert.True(result.WasNeeded);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.CopiedFiles);
        Assert.True(File.Exists(Path.Combine(destination, "settings.json")));
        Assert.False(File.Exists(Path.Combine(destination, "Models", "voice.bin")));
        Assert.False(File.Exists(Path.Combine(destination, "Runtime", "Ollama", "ollama.exe")));
        Assert.False(File.Exists(Path.Combine(destination, "Logs", "old.log")));
        Assert.False(File.Exists(Path.Combine(destination, "Temp", "capture.wav")));
        Assert.True(File.Exists(result.MarkerPath));
        Assert.True(File.Exists(Path.Combine(source, "settings.json")));
    }

    [Fact]
    public void Migrate_DoesNotOverwriteKohanaAndRunsOnlyOnceAfterSuccess()
    {
        var source = Path.Combine(_root, "Nexo");
        var destination = Path.Combine(_root, "Kohana");
        Write(source, "settings.json", "legacy");
        Write(destination, "settings.json", "current");

        var first = LegacyDataMigrator.Migrate(source, destination);
        var second = LegacyDataMigrator.Migrate(source, destination);

        Assert.True(first.Succeeded);
        Assert.Equal(0, first.CopiedFiles);
        Assert.Equal(1, first.SkippedFiles);
        Assert.Equal("current", File.ReadAllText(Path.Combine(destination, "settings.json")));
        Assert.True(second.WasAlreadyCompleted);
        Assert.Equal(first.MarkerPath, second.MarkerPath);
    }

    [Fact]
    public void Migrate_WithoutLegacyDirectory_IsNotNeeded()
    {
        var result = LegacyDataMigrator.Migrate(
            Path.Combine(_root, "missing"),
            Path.Combine(_root, "Kohana"));

        Assert.False(result.WasNeeded);
        Assert.True(result.Succeeded);
    }

    private static void Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // La limpieza de una carpeta temporal no debe ocultar el resultado.
        }
    }
}
