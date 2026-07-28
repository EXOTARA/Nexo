using Nexo.App.Ambient;
using Nexo.Core.Ambient;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D4 — <see cref="AmbientRequestDisplayStateBuilder"/> es lógica pura (sin WPF), la misma
/// fuente que alimenta el Sakura Pill Host. No necesita <see cref="StaWpfFixture"/>, igual que
/// <c>FocusDisplayStateBuilderTests</c>.
/// </summary>
public sealed class AmbientRequestDisplayStateBuilderTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 28, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void NoActiveRequest_ReturnsHidden()
    {
        var state = AmbientRequestDisplayStateBuilder.Build(CreateManager());

        Assert.False(state.IsVisible);
        Assert.Null(state.RequestId);
    }

    [Fact]
    public void Listening_ShowsThePromptAndAllowsCancelOnly()
    {
        var manager = CreateManager();
        manager.Begin("¿qué hora es?", context: null, ReferenceNow);

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.True(state.IsVisible);
        Assert.Equal("Escuchando…", state.StatusText);
        Assert.Equal("¿qué hora es?", state.ShortText);
        Assert.True(state.CanCancel);
        Assert.False(state.CanDismiss);
        Assert.False(state.CanUndo);
        Assert.Empty(state.QuickActions);
    }

    [Fact]
    public void Thinking_KeepsThePromptVisibleAndAllowsCancelOnly()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.True(state.IsVisible);
        Assert.Equal("Pensando…", state.StatusText);
        Assert.Equal("resume esto", state.ShortText);
        Assert.True(state.CanCancel);
    }

    [Fact]
    public void Result_ExposesShortAndExpandedTextWithQuickActions()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));
        var quickAction = new AmbientQuickAction("copy", "Copiar", AmbientAutonomyLevel.Ver);
        manager.CompleteWithResult(
            new AmbientRequestResult("Corto", "Expandido", [quickAction], CanUndo: true),
            ReferenceNow.AddSeconds(2));

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.True(state.IsVisible);
        Assert.Equal("Listo", state.StatusText);
        Assert.Equal("Corto", state.ShortText);
        Assert.Equal("Expandido", state.ExpandedText);
        Assert.Single(state.QuickActions);
        Assert.False(state.CanCancel);
        Assert.True(state.CanDismiss);
        Assert.True(state.CanUndo);
    }

    [Fact]
    public void Result_WithUndoneFlag_NoLongerOffersUndo()
    {
        var manager = CreateManager();
        manager.Begin("resume esto", context: null, ReferenceNow);
        manager.BeginThinking(ReferenceNow.AddSeconds(1));
        var request = manager.CompleteWithResult(
            new AmbientRequestResult("Corto", null, [], CanUndo: true),
            ReferenceNow.AddSeconds(2)).Request!;
        manager.Undo(request.Id, ReferenceNow.AddSeconds(3));

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.False(state.CanUndo);
    }

    [Fact]
    public void Failed_ExposesTheErrorMessageAndAllowsDismissOnly()
    {
        var manager = CreateManager();
        manager.Begin("algo que falla", context: null, ReferenceNow);
        manager.Fail("no se pudo procesar", ReferenceNow.AddSeconds(1));

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.True(state.IsVisible);
        Assert.Equal("No se pudo completar", state.StatusText);
        Assert.Equal("no se pudo procesar", state.ErrorMessage);
        Assert.False(state.CanCancel);
        Assert.True(state.CanDismiss);
    }

    [Fact]
    public void AfterDismiss_ReturnsHiddenAgain()
    {
        var manager = CreateManager();
        manager.Begin("algo", context: null, ReferenceNow);
        manager.Fail("error", ReferenceNow.AddSeconds(1));
        manager.Dismiss(ReferenceNow.AddSeconds(2));

        var state = AmbientRequestDisplayStateBuilder.Build(manager);

        Assert.False(state.IsVisible);
    }

    private static AmbientRequestManager CreateManager()
    {
        var manager = new AmbientRequestManager(new FakeAmbientRequestHistoryStore());
        manager.Load();
        return manager;
    }

    private sealed class FakeAmbientRequestHistoryStore : IAmbientRequestHistoryStore
    {
        private AmbientRequestState _state = new();

        public AmbientRequestState Load() => _state.Copy();

        public void Save(AmbientRequestState state) => _state = state.Copy();
    }
}
