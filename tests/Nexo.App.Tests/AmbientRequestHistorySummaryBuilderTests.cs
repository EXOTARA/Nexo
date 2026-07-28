using Nexo.App.Ambient;
using Nexo.Core.Ambient;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D4.4 — <see cref="AmbientRequestHistorySummaryBuilder"/> es lógica pura (sin WPF) sobre
/// el historial real de <c>AmbientRequestManager</c>. No inventa datos: una entrada fallida muestra
/// su mensaje de error, nunca un resultado que no existió.
/// </summary>
public sealed class AmbientRequestHistorySummaryBuilderTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 28, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void EmptyHistory_ReturnsEmptyList()
    {
        var items = AmbientRequestHistorySummaryBuilder.Build([]);

        Assert.Empty(items);
    }

    [Fact]
    public void Entries_AreOrderedMostRecentFirst()
    {
        var history = new List<AmbientRequestHistoryEntry>
        {
            Entry("Primera", AmbientRequestStatus.Cancelled, ReferenceNow.AddMinutes(-10)),
            Entry("Segunda", AmbientRequestStatus.Result, ReferenceNow.AddMinutes(-5)),
            Entry("Tercera", AmbientRequestStatus.Failed, ReferenceNow)
        };

        var items = AmbientRequestHistorySummaryBuilder.Build(history);

        Assert.Equal(["Tercera", "Segunda", "Primera"], items.Select(item => item.Prompt));
    }

    [Fact]
    public void ResultEntry_ShowsResultShortTextNotErrorMessage()
    {
        var entry = new AmbientRequestHistoryEntry
        {
            Prompt = "resume esto",
            Status = AmbientRequestStatus.Result,
            CompletedAt = ReferenceNow,
            ResultShortText = "Resumen corto",
            ErrorMessage = null
        };

        var item = Assert.Single(AmbientRequestHistorySummaryBuilder.Build([entry]));

        Assert.Equal("Resumen corto", item.ResultText);
        Assert.Equal("Resuelta", item.StatusLabel);
    }

    [Fact]
    public void FailedEntry_ShowsErrorMessageAsResultText()
    {
        var entry = new AmbientRequestHistoryEntry
        {
            Prompt = "algo que falla",
            Status = AmbientRequestStatus.Failed,
            CompletedAt = ReferenceNow,
            ResultShortText = null,
            ErrorMessage = "no se pudo procesar"
        };

        var item = Assert.Single(AmbientRequestHistorySummaryBuilder.Build([entry]));

        Assert.Equal("no se pudo procesar", item.ResultText);
        Assert.Equal("Falló", item.StatusLabel);
    }

    [Fact]
    public void CancelledEntry_HasNoResultText()
    {
        var entry = new AmbientRequestHistoryEntry
        {
            Prompt = "algo cancelado",
            Status = AmbientRequestStatus.Cancelled,
            CompletedAt = ReferenceNow,
            ResultShortText = null,
            ErrorMessage = null
        };

        var item = Assert.Single(AmbientRequestHistorySummaryBuilder.Build([entry]));

        Assert.Null(item.ResultText);
        Assert.Equal("Cancelada", item.StatusLabel);
    }

    [Fact]
    public void UndoState_IsPassedThroughUnchanged()
    {
        var undoable = Entry("con deshacer", AmbientRequestStatus.Result, ReferenceNow, canUndo: true);
        var alreadyUndone = Entry("ya deshecho", AmbientRequestStatus.Result, ReferenceNow, canUndo: true, undone: true);
        var notUndoable = Entry("sin deshacer", AmbientRequestStatus.Result, ReferenceNow, canUndo: false);

        var items = AmbientRequestHistorySummaryBuilder
            .Build([undoable, alreadyUndone, notUndoable])
            .ToDictionary(item => item.Prompt);

        Assert.True(items["con deshacer"].CanUndo);
        Assert.False(items["con deshacer"].Undone);
        Assert.True(items["ya deshecho"].Undone);
        Assert.False(items["sin deshacer"].CanUndo);
    }

    [Fact]
    public void MoreThanMaxEntries_AreTruncatedToTheMostRecent()
    {
        var history = Enumerable.Range(0, AmbientRequestHistorySummaryBuilder.MaxRecentEntries + 5)
            .Select(index => Entry($"solicitud {index}", AmbientRequestStatus.Result, ReferenceNow.AddMinutes(index)))
            .ToList();

        var items = AmbientRequestHistorySummaryBuilder.Build(history);

        Assert.Equal(AmbientRequestHistorySummaryBuilder.MaxRecentEntries, items.Count);
    }

    private static AmbientRequestHistoryEntry Entry(
        string prompt,
        AmbientRequestStatus status,
        DateTimeOffset completedAt,
        bool canUndo = false,
        bool undone = false) => new()
    {
        Prompt = prompt,
        Status = status,
        CompletedAt = completedAt,
        ResultShortText = status == AmbientRequestStatus.Result ? "resultado" : null,
        ErrorMessage = status == AmbientRequestStatus.Failed ? "error" : null,
        CanUndo = canUndo,
        Undone = undone
    };
}
