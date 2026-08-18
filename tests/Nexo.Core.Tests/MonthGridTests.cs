using Nexo.Core.Time;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D43 — la rejilla del calendario del panel. Las tres pruebas de fecha concreta cubren las
/// trampas que no se ven mirando un mes cualquiera.
/// </summary>
public sealed class MonthGridTests
{
    [Fact]
    public void Build_AlwaysReturnsSixWeeks()
    {
        // Febrero de 2026 tiene 28 días y empieza en domingo: cabe en cuatro semanas exactas. Es el
        // mes que destapa una rejilla que se encoge, y con ella todo lo que tiene debajo saltando.
        var grid = MonthGrid.Build(new DateOnly(2026, 2, 15), new DateOnly(2026, 2, 15));

        Assert.Equal(42, grid.Count);
    }

    [Fact]
    public void Build_WithoutLeadingDays_StartsOnTheFirst()
    {
        // Febrero de 2026 empieza en domingo, que es el primer día de la semana por omisión: aquí
        // el relleno delantero es cero, y calcularlo con un resto sin corregir daría -7.
        var grid = MonthGrid.Build(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1));

        Assert.Equal(new DateOnly(2026, 2, 1), grid[0].Date);
        Assert.True(grid[0].InMonth);
    }

    [Fact]
    public void Build_FillsLeadingAndTrailingDaysFromNeighbouringMonths()
    {
        // Agosto de 2026 empieza en sábado: seis casillas de julio delante.
        var grid = MonthGrid.Build(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 17));

        Assert.Equal(new DateOnly(2026, 7, 26), grid[0].Date);
        Assert.False(grid[0].InMonth);
        Assert.Equal(new DateOnly(2026, 8, 1), grid[6].Date);
        Assert.True(grid[6].InMonth);
        Assert.False(grid[^1].InMonth);
    }

    [Fact]
    public void Build_MarksTodayExactlyOnce()
    {
        var today = new DateOnly(2026, 8, 17);
        var grid = MonthGrid.Build(today, today);

        Assert.Single(grid, day => day.IsToday);
        Assert.Equal(today, grid.First(day => day.IsToday).Date);
    }

    [Fact]
    public void Build_LookingAtAnotherMonth_MarksNoToday()
    {
        // El día de hoy solo se resalta en su mes. Al pasar a septiembre no puede quedar un 17
        // encendido, que es lo que pasa si se compara solo el número de día.
        var grid = MonthGrid.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 17));

        Assert.DoesNotContain(grid, day => day.IsToday && day.InMonth);
    }

    [Fact]
    public void WeekdayOrder_StartsOnTheConfiguredDay()
    {
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
             DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            MonthGrid.WeekdayOrder(DayOfWeek.Monday));
    }

    [Fact]
    public void Build_WithMondayFirst_ShiftsTheLeadingFill()
    {
        // El mismo mes leído con la semana empezando en lunes lleva un relleno distinto: agosto de
        // 2026 empieza en sábado, así que quedan cinco días de julio delante en vez de seis.
        var grid = MonthGrid.Build(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1), DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2026, 7, 27), grid[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 1), grid[5].Date);
    }
}
