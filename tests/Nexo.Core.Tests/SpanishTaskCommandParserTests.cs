using Nexo.Core.Tasks;

namespace Nexo.Core.Tests;

public sealed class SpanishTaskCommandParserTests
{
    private readonly SpanishTaskCommandParser _parser = new();
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 19, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Parse_ReminderTomorrowWithNumericTime_CreatesReminder()
    {
        var result = _parser.Parse(
            "Recuérdame entregar la tarea mañana a las 8",
            ReferenceNow);

        Assert.Equal(TaskCommandType.Create, result.Type);
        Assert.Equal("entregar la tarea", result.Title);
        Assert.True(result.ReminderEnabled);
        Assert.Equal(new DateTime(2026, 7, 20, 8, 0, 0), result.DueAt?.DateTime);
    }

    [Fact]
    public void Parse_TaskForWeekday_PreservesTitleAndPriority()
    {
        var result = _parser.Parse(
            "Agrega comprar pasta térmica para el lunes prioridad alta",
            ReferenceNow);

        Assert.Equal(TaskCommandType.Create, result.Type);
        Assert.Equal("comprar pasta térmica", result.Title);
        Assert.Equal(TaskPriority.High, result.Priority);
        Assert.Equal(DayOfWeek.Monday, result.DueAt?.DayOfWeek);
        Assert.False(result.ReminderEnabled);
    }

    [Theory]
    [InlineData("¿Qué tengo pendiente hoy?", TaskCommandType.ListToday)]
    [InlineData("Muestra mis tareas", TaskCommandType.ListPending)]
    [InlineData("Abre tareas", TaskCommandType.OpenTasks)]
    public void Parse_ListAndNavigationCommands_AreRecognized(
        string text,
        TaskCommandType expected)
    {
        var result = _parser.Parse(text, ReferenceNow);

        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public void Parse_CompleteTask_ExtractsSearchText()
    {
        var result = _parser.Parse(
            "Marca como terminada la tarea de matrices",
            ReferenceNow);

        Assert.Equal(TaskCommandType.Complete, result.Type);
        Assert.Equal("matrices", result.Title);
    }

    [Fact]
    public void Parse_DeleteReminder_ExtractsSearchText()
    {
        var result = _parser.Parse(
            "Cancela el recordatorio de comprar alcohol",
            ReferenceNow);

        Assert.Equal(TaskCommandType.Delete, result.Type);
        Assert.Equal("comprar alcohol", result.Title);
    }

    [Theory]
    [InlineData("Kohana recuérdame llamar a mamá hoy a las ocho pm")]
    [InlineData("Oye Kohana recuérdame llamar a mamá hoy a las ocho pm")]
    [InlineData("Nexo recuérdame llamar a mamá hoy a las ocho pm")]
    public void Parse_SpokenHourWord_UsesCorrectHour(string input)
    {
        var result = _parser.Parse(input, ReferenceNow);

        Assert.Equal(TaskCommandType.Create, result.Type);
        Assert.Equal("llamar a mamá", result.Title);
        Assert.Equal(20, result.DueAt?.Hour);
    }

    [Fact]
    public void Parse_TimeWithoutDate_UsesTodayWhenStillFuture()
    {
        var result = _parser.Parse(
            "Recuérdame tomar agua a las 18:30",
            ReferenceNow);

        Assert.Equal(new DateTime(2026, 7, 19, 18, 30, 0), result.DueAt?.DateTime);
    }

    [Fact]
    public void Parse_TimeWithoutDate_UsesTomorrowWhenAlreadyPast()
    {
        var result = _parser.Parse(
            "Recuérdame tomar agua a las 8",
            ReferenceNow);

        Assert.Equal(new DateTime(2026, 7, 20, 8, 0, 0), result.DueAt?.DateTime);
    }

    [Fact]
    public void Parse_ReminderWithoutDate_KeepsReminderIntent()
    {
        var result = _parser.Parse("Recuérdame llamar a mamá", ReferenceNow);

        Assert.Equal(TaskCommandType.Create, result.Type);
        Assert.True(result.ReminderEnabled);
        Assert.Null(result.DueAt);
    }

    [Fact]
    public void Parse_UnrelatedQuestion_ReturnsNone()
    {
        var result = _parser.Parse("Explícame la memoria RAM", ReferenceNow);

        Assert.Equal(TaskCommandType.None, result.Type);
    }
}
