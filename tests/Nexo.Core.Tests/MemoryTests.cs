using Nexo.Core.Memory;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D9 (Fase 6) — el criterio de terminado de la fase es que los controles de exclusión y
/// retención funcionen ANTES de que exista almacenamiento. Por eso casi todo lo que se prueba aquí
/// es lo que Sakura se NIEGA a recordar.
/// </summary>
public sealed class MemoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(-6));

    private static MemorySettings AllOn(int retentionDays = 30) => new()
    {
        Enabled = true,
        RememberPreferences = true,
        RememberConversation = true,
        RememberHabits = true,
        RetentionDays = retentionDays
    };

    private static MemoryManager CreateManager()
    {
        var manager = new MemoryManager(new FakeMemoryStore());
        manager.Load();
        return manager;
    }

    // ---------- Apagado por omisión ----------

    [Fact]
    public void Memory_IsOffByDefault_AndSoIsEveryCategory()
    {
        var settings = new MemorySettings();

        Assert.False(settings.Enabled);
        Assert.False(settings.IsCategoryEnabled(MemoryCategory.Preferencias));
        Assert.False(settings.IsCategoryEnabled(MemoryCategory.Conversacion));
        Assert.False(settings.IsCategoryEnabled(MemoryCategory.Habitos));
    }

    [Fact]
    public void WithMemoryDisabled_NothingIsRemembered()
    {
        var manager = CreateManager();
        var settings = new MemorySettings { Enabled = false, RememberPreferences = true };

        var result = manager.Remember(MemoryCategory.Preferencias, "prefiero respuestas cortas", settings, Now);

        Assert.False(result.Success);
        Assert.Empty(manager.GetAll(AllOn(), Now));
    }

    [Fact]
    public void EnablingOneCategory_DoesNotEnableTheOthers()
    {
        var manager = CreateManager();
        var settings = new MemorySettings { Enabled = true, RememberPreferences = true };

        Assert.True(manager.Remember(MemoryCategory.Preferencias, "prefiero café", settings, Now).Success);
        Assert.False(manager.Remember(MemoryCategory.Conversacion, "hablamos del reporte", settings, Now).Success);
        Assert.False(manager.Remember(MemoryCategory.Habitos, "abre Sakura temprano", settings, Now).Success);
    }

    [Fact]
    public void TurningMemoryOff_AlsoTurnsOffTheCategories()
    {
        // Si no, volver a activar la memoria resucitaría permisos que la persona creía revocados.
        var settings = new MemorySettings
        {
            Enabled = false,
            RememberPreferences = true,
            RememberConversation = true,
            RememberHabits = true
        };

        settings.Normalize();

        Assert.False(settings.RememberPreferences);
        Assert.False(settings.RememberConversation);
        Assert.False(settings.RememberHabits);
    }

    // ---------- Exclusiones ----------

    [Fact]
    public void AnExcludedWord_BlocksTheWholeSentence()
    {
        var manager = CreateManager();
        var settings = AllOn();
        settings.Exclusions = ["clínica"];

        var result = manager.Remember(
            MemoryCategory.Conversacion, "tengo cita en la clínica el martes", settings, Now);

        Assert.False(result.Success);
        Assert.Contains("exclusión", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(manager.GetAll(settings, Now));
    }

    [Fact]
    public void Exclusions_IgnoreCase()
    {
        var settings = AllOn();
        settings.Exclusions = ["Clínica"];

        Assert.NotNull(settings.MatchExclusion("voy a la CLÍNICA"));
    }

    [Fact]
    public void Normalize_DropsBlankAndDuplicateExclusions()
    {
        var settings = new MemorySettings
        {
            Enabled = true,
            Exclusions = ["clínica", "  ", "CLÍNICA", "banco"]
        };

        settings.Normalize();

        Assert.Equal(2, settings.Exclusions.Count);
    }

    // ---------- Contenido sensible ----------

    [Fact]
    public void SensitiveLookingText_IsNeverRemembered_EvenWithEverythingEnabled()
    {
        // Reutiliza el mismo detector que protege a Lens: una contraseña dictada sin querer no
        // debe acabar guardada aunque la memoria esté completamente activada.
        var manager = CreateManager();

        var result = manager.Remember(
            MemoryCategory.Conversacion, "mi password: hunter2secreto", AllOn(), Now);

        Assert.False(result.Success);
        Assert.Empty(manager.GetAll(AllOn(), Now));
    }

    [Fact]
    public void BlankText_IsRejected()
    {
        var manager = CreateManager();

        Assert.False(manager.Remember(MemoryCategory.Preferencias, "   ", AllOn(), Now).Success);
    }

    // ---------- Retención ----------

    [Fact]
    public void EntriesOlderThanTheRetentionWindow_AreDropped()
    {
        var manager = CreateManager();
        var settings = AllOn(retentionDays: 7);

        manager.Remember(MemoryCategory.Preferencias, "vieja", settings, Now.AddDays(-30));
        manager.Remember(MemoryCategory.Preferencias, "reciente", settings, Now);

        var all = manager.GetAll(settings, Now);

        Assert.Single(all);
        Assert.Equal("reciente", all[0].Text);
    }

    [Fact]
    public void ShorteningRetention_TakesEffectOnRead_WithoutWaitingForANewWrite()
    {
        // Acortar la retención es una orden, no una preferencia a futuro.
        var manager = CreateManager();
        var generous = AllOn(retentionDays: 90);
        manager.Remember(MemoryCategory.Preferencias, "de hace 30 días", generous, Now.AddDays(-30));

        Assert.Single(manager.GetAll(generous, Now));
        Assert.Empty(manager.GetAll(AllOn(retentionDays: 7), Now));
    }

    [Fact]
    public void RetentionDays_IsClampedToASaneRange()
    {
        var tooLow = new MemorySettings { Enabled = true, RetentionDays = 0 };
        var tooHigh = new MemorySettings { Enabled = true, RetentionDays = 99999 };

        tooLow.Normalize();
        tooHigh.Normalize();

        Assert.Equal(MemorySettings.MinimumRetentionDays, tooLow.RetentionDays);
        Assert.Equal(MemorySettings.MaximumRetentionDays, tooHigh.RetentionDays);
    }

    [Fact]
    public void ThereIsAlwaysAHardCapOnStoredEntries()
    {
        var manager = CreateManager();
        var settings = AllOn(retentionDays: 365);

        for (var i = 0; i < MemorySettings.MaximumEntries + 25; i++)
        {
            manager.Remember(MemoryCategory.Preferencias, $"recuerdo {i}", settings, Now.AddSeconds(i));
        }

        Assert.Equal(MemorySettings.MaximumEntries, manager.GetAll(settings, Now.AddDays(1)).Count);
    }

    // ---------- Consulta y olvido ----------

    [Fact]
    public void Search_IsLiteralAndCaseInsensitive()
    {
        var manager = CreateManager();
        var settings = AllOn();
        manager.Remember(MemoryCategory.Preferencias, "prefiero respuestas cortas", settings, Now);
        manager.Remember(MemoryCategory.Preferencias, "uso Visual Studio", settings, Now);

        var found = manager.Search("VISUAL", settings, Now);

        Assert.Single(found);
        Assert.Equal("uso Visual Studio", found[0].Text);
    }

    [Fact]
    public void Forget_RemovesJustThatEntry()
    {
        var manager = CreateManager();
        var settings = AllOn();
        manager.Remember(MemoryCategory.Preferencias, "una", settings, Now);
        manager.Remember(MemoryCategory.Preferencias, "otra", settings, Now.AddSeconds(1));
        var target = manager.GetAll(settings, Now.AddSeconds(2))[0];

        Assert.True(manager.Forget(target.Id).Success);
        Assert.Single(manager.GetAll(settings, Now.AddSeconds(2)));
    }

    [Fact]
    public void ForgetEverything_WorksEvenWithMemoryDisabled()
    {
        // Revocar debe funcionar siempre: si apagar la memoria bloqueara el borrado, lo ya
        // guardado quedaría atrapado justo cuando la persona quiere deshacerse de ello.
        var manager = CreateManager();
        manager.Remember(MemoryCategory.Preferencias, "algo", AllOn(), Now);

        var result = manager.ForgetEverything();

        Assert.True(result.Success);
        Assert.Empty(manager.GetAll(AllOn(), Now));
    }

    private sealed class FakeMemoryStore : IMemoryStore
    {
        private MemoryState _state = new();

        public MemoryState Load() => _state.Copy();

        public void Save(MemoryState state) => _state = state.Copy();
    }
}
