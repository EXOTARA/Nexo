using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Ai;
using Nexo.Core.Audit;
using Nexo.Core.Flow;
using Nexo.Core.Settings;
using Nexo.Core.Skills;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D15 (Fase 8) — el criterio de terminado de la fase pide dos packs completos hechos
/// **solo** con capacidades ya implementadas, y la regla que la sostiene es que un pack hereda los
/// permisos de lo que combina y no introduce excepciones. Casi todo lo que se prueba aquí es que un
/// pack no concede nada por su cuenta.
/// </summary>
public sealed class SkillPackTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 14, 0, 0, TimeSpan.FromHours(-6));

    private sealed class FakeSnapshotStore : ISkillPackSnapshotStore
    {
        public SkillPackSnapshot? Current { get; private set; }

        public SkillPackSnapshot? Load() => Current?.Copy();

        public void Save(SkillPackSnapshot? snapshot) => Current = snapshot?.Copy();
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private static (SkillPackCoordinator Coordinator, FakeSnapshotStore Snapshots, FakeAuditLog Audit) Build()
    {
        var snapshots = new FakeSnapshotStore();
        var audit = new FakeAuditLog();

        return (new SkillPackCoordinator(snapshots, audit, () => Now), snapshots, audit);
    }

    // ---------- El catálogo ----------

    [Fact]
    public void TheSixPacksTheRoadmapNamesExist()
    {
        // El criterio de terminado de la fase pedía al menos dos (D15). D23 completa los seis que
        // el roadmap nombra: Dev, Study, Support, Creator, Access y Meeting.
        Assert.Equal(6, SkillPackCatalog.All.Count);
        Assert.Equal(
            Enum.GetValues<SkillPackId>().Length,
            SkillPackCatalog.All.Select(pack => pack.Id).Distinct().Count());

        foreach (var id in Enum.GetValues<SkillPackId>())
        {
            Assert.NotNull(SkillPackCatalog.Get(id));
        }
    }

    [Fact]
    public void EveryPackOnlyTouchesSettingsThatAlreadyExisted()
    {
        // La regla de la fase: los packs combinan capacidades ya implementadas, no inventan
        // ninguna. Si un ajuste no se puede leer y escribir sobre unas preferencias normales, es que
        // el pack se inventó algo.
        var preferences = new ShellPreferences();

        Assert.All(SkillPackCatalog.All.SelectMany(pack => pack.Settings), setting =>
        {
            var before = setting.Read(preferences);
            setting.Write(preferences, setting.DesiredValue);
            Assert.Equal(setting.DesiredValue, setting.Read(preferences));

            setting.Write(preferences, before);
            Assert.Equal(before, setting.Read(preferences));
        });
    }

    [Fact]
    public void EveryPackIsReversible()
    {
        // Cada pack, no solo los dos primeros: activar y desactivar tiene que devolver exactamente
        // lo que había.
        foreach (var pack in SkillPackCatalog.All)
        {
            var (coordinator, _, _) = Build();
            var preferences = new ShellPreferences();

            var before = pack.Settings.ToDictionary(
                setting => setting.Id,
                setting => setting.Read(preferences));

            coordinator.Apply(pack, preferences);
            coordinator.Revert(preferences);

            foreach (var setting in pack.Settings)
            {
                Assert.Equal(before[setting.Id], setting.Read(preferences));
            }
        }
    }

    [Fact]
    public void TherAreTwoCompletePacks()
    {
        // El criterio de terminado de la fase: al menos dos.
        Assert.True(SkillPackCatalog.All.Count >= 2);

        Assert.All(SkillPackCatalog.All, pack =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pack.Name));
            Assert.False(string.IsNullOrWhiteSpace(pack.Purpose));
            Assert.NotEmpty(pack.Settings);
            Assert.NotEmpty(pack.Requirements);
        });
    }

    [Fact]
    public void EveryRequirement_SaysWhyItMattersAndWhereToEnableIt() =>
        Assert.All(SkillPackCatalog.All.SelectMany(pack => pack.Requirements), requirement =>
        {
            Assert.False(string.IsNullOrWhiteSpace(requirement.WhyItMatters));
            Assert.False(string.IsNullOrWhiteSpace(requirement.HowToEnable));
        });

    // ---------- Un pack no concede permisos ----------

    [Fact]
    public void ActivatingAPack_NeverTurnsOnMemory_VisionOrProjectAccess()
    {
        // Es la regla de la fase: un pack hereda permisos, no los introduce. Encenderlos aquí sería
        // concederlos sin pedirlos, disfrazado de comodidad.
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences { VisionEnabled = false };
        preferences.Memory.Enabled = false;

        coordinator.Apply(SkillPackCatalog.Dev, preferences);

        Assert.False(preferences.Memory.Enabled);
        Assert.False(preferences.VisionEnabled);
        Assert.False(preferences.Workspace.HasAuthorizedFolder);
    }

    [Fact]
    public void ThePreview_ListsWhatIsMissing_NotJustWhatIsGained()
    {
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences { VisionEnabled = false, AiProvider = AiProviderKind.Disabled };

        var plan = coordinator.Preview(SkillPackCatalog.Study, preferences);

        Assert.NotEmpty(plan.UnmetRequirements);
        Assert.Contains(plan.UnmetRequirements, requirement => requirement.Title.Contains("Lens"));
    }

    [Fact]
    public void SatisfiedRequirements_AreNotListedAsMissing()
    {
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences { VisionEnabled = true, AiProvider = AiProviderKind.Ollama };
        preferences.Memory.Enabled = true;

        var plan = coordinator.Preview(SkillPackCatalog.Study, preferences);

        Assert.Empty(plan.UnmetRequirements);
    }

    // ---------- Vista previa ----------

    [Fact]
    public void ThePreview_OnlyListsWhatWouldActuallyChange()
    {
        // Enseñar "Dictado activado → Dictado activado" llenaría la vista previa de ruido.
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences { FlowEnabled = true, FlowMode = FlowMode.Codigo };

        var plan = coordinator.Preview(SkillPackCatalog.Dev, preferences);

        Assert.DoesNotContain(plan.Changes, change => change.SettingId == "flow.enabled");
        Assert.DoesNotContain(plan.Changes, change => change.SettingId == "flow.mode");
    }

    [Fact]
    public void PreviewingChangesNothing()
    {
        var (coordinator, snapshots, _) = Build();
        var preferences = new ShellPreferences { FlowMode = FlowMode.Texto };

        coordinator.Preview(SkillPackCatalog.Dev, preferences);

        Assert.Equal(FlowMode.Texto, preferences.FlowMode);
        Assert.Null(snapshots.Current);
    }

    // ---------- Activar ----------

    [Fact]
    public void ActivatingDev_LeavesTheSettingsThePackDescribes()
    {
        var (coordinator, _, audit) = Build();
        var preferences = new ShellPreferences
        {
            FlowMode = FlowMode.Texto,
            ShowSystemModule = false,
            HardwarePerformanceMode = HardwarePerformanceMode.Eco
        };

        var result = coordinator.Apply(SkillPackCatalog.Dev, preferences);

        Assert.True(result.Success);
        Assert.Equal(FlowMode.Codigo, preferences.FlowMode);
        Assert.True(preferences.ShowSystemModule);
        Assert.Equal(HardwarePerformanceMode.Maximum, preferences.HardwarePerformanceMode);
        Assert.Contains(audit.Entries, entry => entry.Action.Contains("Pack activado"));
    }

    [Fact]
    public void ActivatingAPackAlreadyInPlace_ChangesNothing()
    {
        var (coordinator, snapshots, _) = Build();
        var preferences = new ShellPreferences();
        coordinator.Apply(SkillPackCatalog.Study, preferences);

        var again = coordinator.Apply(SkillPackCatalog.Study, preferences);

        Assert.False(again.Success);
        Assert.Contains("ya está como debe estar", again.Detail);
        Assert.NotNull(snapshots.Current);
    }

    [Fact]
    public void TheSnapshotIsTakenBeforeAnythingChanges()
    {
        var (coordinator, snapshots, _) = Build();
        var preferences = new ShellPreferences { FlowMode = FlowMode.Texto, SpeakVoiceResponses = false };

        coordinator.Apply(SkillPackCatalog.Dev, preferences);

        Assert.Equal("Texto", snapshots.Current!.PreviousValues["flow.mode"]);
    }

    // ---------- Desactivar ----------

    [Fact]
    public void DeactivatingReturnsEverySettingToWhatItWas()
    {
        var (coordinator, snapshots, _) = Build();
        var preferences = new ShellPreferences
        {
            FlowEnabled = false,
            FlowMode = FlowMode.Correo,
            ShowSystemModule = false,
            ShowCaptureModule = false,
            HardwarePerformanceMode = HardwarePerformanceMode.Eco
        };

        coordinator.Apply(SkillPackCatalog.Dev, preferences);
        var result = coordinator.Revert(preferences);

        Assert.True(result.Success);
        Assert.False(preferences.FlowEnabled);
        Assert.Equal(FlowMode.Correo, preferences.FlowMode);
        Assert.False(preferences.ShowSystemModule);
        Assert.False(preferences.ShowCaptureModule);
        Assert.Equal(HardwarePerformanceMode.Eco, preferences.HardwarePerformanceMode);
        Assert.Null(snapshots.Current);
    }

    [Fact]
    public void WithNoPackActive_ThereIsNothingToDeactivate()
    {
        var (coordinator, _, _) = Build();

        Assert.False(coordinator.Revert(new ShellPreferences()).Success);
    }

    [Fact]
    public void SwitchingPacks_RestoresTheUsersSettings_NotTheOtherPacks()
    {
        // Si no, el snapshot del segundo guardaría los ajustes del primero como si fueran los de la
        // persona, y desactivar no devolvería nada parecido a lo que tenía.
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences
        {
            FlowMode = FlowMode.Correo,
            SpeakVoiceResponses = false,
            PeekEnabled = true
        };

        coordinator.Apply(SkillPackCatalog.Study, preferences);
        coordinator.Apply(SkillPackCatalog.Dev, preferences);
        coordinator.Revert(preferences);

        Assert.Equal(FlowMode.Correo, preferences.FlowMode);
        Assert.False(preferences.SpeakVoiceResponses);
        Assert.True(preferences.PeekEnabled);
    }

    [Fact]
    public void OnlyOnePackIsActiveAtATime()
    {
        var (coordinator, _, _) = Build();
        var preferences = new ShellPreferences();

        coordinator.Apply(SkillPackCatalog.Study, preferences);
        coordinator.Apply(SkillPackCatalog.Dev, preferences);

        Assert.Equal(SkillPackId.Dev, coordinator.ActivePackId);
    }

    [Fact]
    public void ASnapshotOfAPackThatNoLongerExists_IsDiscardedOutLoud()
    {
        var (coordinator, snapshots, _) = Build();
        snapshots.Save(new SkillPackSnapshot { PackId = "PackInventado", AppliedAt = Now });

        var result = coordinator.Revert(new ShellPreferences());

        Assert.False(result.Success);
        Assert.Contains("ya no existe", result.Detail);

        // Pero no se queda bloqueando la activación de otros packs.
        Assert.Null(snapshots.Current);
    }

    [Fact]
    public void DeactivatingIsAudited()
    {
        var (coordinator, _, audit) = Build();
        var preferences = new ShellPreferences();

        coordinator.Apply(SkillPackCatalog.Study, preferences);
        coordinator.Revert(preferences);

        Assert.Contains(audit.Entries, entry => entry.Action.Contains("Pack desactivado"));
    }
}
