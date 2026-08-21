using Nexo.Core.Productization;
using Nexo.Windows.Productization;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D20 (Fase 9) — la copia previa a actualizar, sobre disco de verdad. Lo que importa aquí es
/// que verificar signifique algo: se compara el contenido, no el tamaño.
/// </summary>
public sealed class FileSystemDataBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _backupRoot;

    public FileSystemDataBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kohana-backup-tests", Guid.NewGuid().ToString("N"));
        _backupRoot = Path.Combine(_root, "backups");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    private FileSystemDataBackupService CreateService(DateTimeOffset? at = null) =>
        new(_backupRoot, _root, () => at ?? new DateTimeOffset(2026, 8, 2, 17, 0, 0, TimeSpan.Zero));

    private void WriteDataFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_root, fileName), content);

    private static IReadOnlyList<SakuraDataItem> Items(params string[] fileNames) =>
        [.. fileNames.Select(name => SakuraDataInventory.Find(name)!)];

    [Fact]
    public void ExistingFiles_AreCopiedAndVerified()
    {
        WriteDataFile("settings.json", "{ \"tema\": \"sakura\" }");

        var result = CreateService().CreateBackup(Items("settings.json"));

        Assert.True(result.Success);
        var outcome = Assert.Single(result.Files);
        Assert.True(outcome.Copied);
        Assert.True(outcome.Verified);
    }

    [Fact]
    public void AFileThatDoesNotExist_IsNotAFailure()
    {
        var result = CreateService().CreateBackup(Items("memory.dat"));

        Assert.True(result.Success);
        Assert.False(Assert.Single(result.Files).Copied);
    }

    [Fact]
    public void TheBackupHoldsTheSameContent()
    {
        WriteDataFile("tasks.json", "[{\"titulo\":\"comprar pan\"}]");

        var result = CreateService().CreateBackup(Items("tasks.json"));

        var copied = Path.Combine(_backupRoot, result.BackupId, "tasks.json");
        Assert.Equal("[{\"titulo\":\"comprar pan\"}]", File.ReadAllText(copied));
    }

    [Fact]
    public void RestoringPutsTheContentBack()
    {
        WriteDataFile("tasks.json", "original");
        var service = CreateService();
        var backup = service.CreateBackup(Items("tasks.json"));

        WriteDataFile("tasks.json", "estropeado");
        var restored = service.Restore(backup.BackupId);

        Assert.True(restored.Success);
        Assert.Equal("original", File.ReadAllText(Path.Combine(_root, "tasks.json")));
    }

    [Fact]
    public void RestoringDoesNotInventFilesThatWereNeverThere()
    {
        // Restaurar es volver a como estaba, no rellenar huecos.
        WriteDataFile("tasks.json", "original");
        var service = CreateService();
        var backup = service.CreateBackup(Items("tasks.json"));

        service.Restore(backup.BackupId);

        Assert.False(File.Exists(Path.Combine(_root, "memory.dat")));
    }

    [Fact]
    public void RestoringAnUnknownBackup_Fails() =>
        Assert.False(CreateService().Restore("no-existe").Success);

    [Fact]
    public void RestoringWithNoIdentifier_Fails() =>
        Assert.False(CreateService().Restore(string.Empty).Success);

    [Fact]
    public void BackupsAreListedNewestFirst()
    {
        WriteDataFile("settings.json", "a");

        CreateService(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero))
            .CreateBackup(Items("settings.json"));
        CreateService(new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero))
            .CreateBackup(Items("settings.json"));

        var backups = CreateService().ListBackups();

        Assert.Equal(2, backups.Count);
        Assert.StartsWith("20260802", backups[0]);
    }

    [Fact]
    public void WithNoBackupsYet_TheListIsEmpty() =>
        Assert.Empty(CreateService().ListBackups());

    [Fact]
    public void TheWholeInventoryCanBeBackedUp()
    {
        // No hace falta que existan todos: lo que importa es que ninguno haga fallar la copia.
        WriteDataFile("settings.json", "a");
        WriteDataFile("audit.json", "[]");

        var result = CreateService().CreateBackup(SakuraDataInventory.ToBackUp);

        Assert.True(result.Success);
        Assert.Equal(SakuraDataInventory.All.Count, result.Files.Count);
    }
}
