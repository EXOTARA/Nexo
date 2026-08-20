using Nexo.Core.ComputerUse;
using Nexo.Core.Skills;
using Nexo.Core.Workspace;
using Nexo.Windows.ComputerUse;
using Nexo.Windows.Skills;
using Nexo.Windows.Workspace;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D14/D15/D18 — los tres almacenes JSON que solo se habían probado a través de dobles en
/// Core. Siguen el mismo patrón atómico ya probado en <c>JsonSakuraAuditLog</c> y
/// <c>FileSystemDataBackupService</c>, pero "sigue el mismo patrón" no es lo mismo que "se probó":
/// es exactamente el tipo de suposición que este repaso existe para no dar por buena.
/// </summary>
public sealed class WorkspaceCheckpointAndSnapshotStoreTests : IDisposable
{
    private readonly string _directory;

    public WorkspaceCheckpointAndSnapshotStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "kohana-store-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    private string PathFor(string fileName) => Path.Combine(_directory, fileName);

    // ---------- JsonWorkspaceCheckpointStore ----------

    [Fact]
    public void Checkpoint_WrittenAndReread_KeepsItsContent()
    {
        var store = new JsonWorkspaceCheckpointStore(PathFor("checkpoints.json"));
        var checkpoint = new WorkspaceCheckpoint
        {
            At = DateTimeOffset.Now,
            RelativePath = "src/App.cs",
            PreviousExisted = true,
            PreviousContent = "antes",
            WrittenContent = "después",
            Description = "prueba"
        };

        store.Save(checkpoint);
        var reloaded = Assert.Single(store.Read());

        Assert.Equal("antes", reloaded.PreviousContent);
        Assert.Equal("después", reloaded.WrittenContent);
    }

    [Fact]
    public void Checkpoint_Removed_DoesNotComeBackAfterReopeningTheStore()
    {
        var path = PathFor("checkpoints.json");
        var checkpoint = new WorkspaceCheckpoint { At = DateTimeOffset.Now, RelativePath = "a.txt" };

        new JsonWorkspaceCheckpointStore(path).Save(checkpoint);
        new JsonWorkspaceCheckpointStore(path).Remove(checkpoint.Id);

        Assert.Empty(new JsonWorkspaceCheckpointStore(path).Read());
    }

    [Fact]
    public void OldCheckpoints_AreTrimmedPastTheLimit()
    {
        var path = PathFor("checkpoints.json");
        var store = new JsonWorkspaceCheckpointStore(path);

        for (var index = 0; index < WorkspaceCheckpointState.MaximumCheckpoints + 5; index++)
        {
            store.Save(new WorkspaceCheckpoint
            {
                At = DateTimeOffset.Now.AddMinutes(index),
                RelativePath = $"archivo{index}.txt"
            });
        }

        Assert.Equal(WorkspaceCheckpointState.MaximumCheckpoints, store.Read().Count);
    }

    // ---------- JsonSkillPackSnapshotStore ----------

    [Fact]
    public void SkillPackSnapshot_WrittenAndReread_KeepsThePreviousValues()
    {
        var store = new JsonSkillPackSnapshotStore(PathFor("skill-pack.json"));
        var snapshot = new SkillPackSnapshot
        {
            PackId = nameof(SkillPackId.Dev),
            AppliedAt = DateTimeOffset.Now,
            PreviousValues = { ["flow.mode"] = "Texto" }
        };

        store.Save(snapshot);
        var reloaded = store.Load();

        Assert.NotNull(reloaded);
        Assert.Equal("Texto", reloaded!.PreviousValues["flow.mode"]);
    }

    [Fact]
    public void SkillPackSnapshot_SavingNull_DeletesTheFile()
    {
        var path = PathFor("skill-pack.json");
        var store = new JsonSkillPackSnapshotStore(path);
        store.Save(new SkillPackSnapshot { PackId = nameof(SkillPackId.Study) });

        store.Save(null);

        Assert.False(File.Exists(path));
        Assert.Null(store.Load());
    }

    // ---------- JsonComputerUseSnapshotStore ----------

    [Fact]
    public void ComputerUseSnapshot_WrittenAndReread_KeepsWhatWasInTheClipboard()
    {
        var store = new JsonComputerUseSnapshotStore(PathFor("computer-use.json"));
        var snapshot = new ComputerUseSnapshot
        {
            At = DateTimeOffset.Now,
            Description = "prueba",
            PreviousClipboard = "lo que había",
            WrittenClipboard = "lo que puso Sakura"
        };

        store.Save(snapshot);
        var reloaded = Assert.Single(store.Read());

        Assert.Equal("lo que había", reloaded.PreviousClipboard);
    }

    [Fact]
    public void ComputerUseSnapshots_AreCappedAtTheMaximum()
    {
        var path = PathFor("computer-use.json");
        var store = new JsonComputerUseSnapshotStore(path);

        for (var index = 0; index < JsonComputerUseSnapshotStore.MaximumSnapshots + 3; index++)
        {
            store.Save(new ComputerUseSnapshot { At = DateTimeOffset.Now.AddMinutes(index) });
        }

        Assert.Equal(JsonComputerUseSnapshotStore.MaximumSnapshots, store.Read().Count);
    }

    [Fact]
    public void ComputerUseSnapshot_Removed_DoesNotComeBackAfterReopeningTheStore()
    {
        var path = PathFor("computer-use.json");
        var snapshot = new ComputerUseSnapshot { At = DateTimeOffset.Now };

        new JsonComputerUseSnapshotStore(path).Save(snapshot);
        new JsonComputerUseSnapshotStore(path).Remove(snapshot.Id);

        Assert.Empty(new JsonComputerUseSnapshotStore(path).Read());
    }

    // ---------- Los tres: un archivo corrupto no debe tumbar Sakura ----------

    [Theory]
    [InlineData("checkpoints.json")]
    [InlineData("skill-pack.json")]
    [InlineData("computer-use.json")]
    public void ACorruptFile_IsTreatedAsEmpty_NotAsACrash(string fileName)
    {
        var path = PathFor(fileName);
        File.WriteAllText(path, "{ esto no es json válido");

        // Cada store debe leer "vacío" en vez de propagar la excepción de deserialización.
        switch (fileName)
        {
            case "checkpoints.json":
                Assert.Empty(new JsonWorkspaceCheckpointStore(path).Read());
                break;
            case "skill-pack.json":
                Assert.Null(new JsonSkillPackSnapshotStore(path).Load());
                break;
            case "computer-use.json":
                Assert.Empty(new JsonComputerUseSnapshotStore(path).Read());
                break;
        }
    }
}
