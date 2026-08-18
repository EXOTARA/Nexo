namespace Nexo.Core.Time;

/// <summary>Una casilla del calendario del panel.</summary>
/// <param name="Date">El día que representa, sea o no del mes que se mira.</param>
/// <param name="InMonth">
/// Falso para los días de relleno del mes anterior o del siguiente. Se pintan apagados en vez de
/// dejar huecos: sin ellos la primera y la última semana quedan cojas y cuesta leer la rejilla.
/// </param>
/// <param name="IsToday">Marca el día de hoy, que va resaltado.</param>
public readonly record struct CalendarDay(DateOnly Date, bool InMonth, bool IsToday);

/// <summary>
/// Diseño D43 — la rejilla del calendario del panel.
///
/// Siempre son seis semanas de siete días, cuarenta y dos casillas, aunque el mes quepa en cinco.
/// Un calendario que cambia de alto según el mes hace saltar todo lo que tiene debajo al pasar de
/// página, y eso se nota mucho más que la fila de relleno.
///
/// Es lógica pura y probada porque tiene dos trampas que solo aparecen con la fecha exacta que las
/// destapa: un mes que empieza justo en el primer día de la semana no lleva relleno delante —y el
/// resto sobrante da negativo si se calcula a la ligera—, y un febrero de 28 días que empieza en
/// domingo ocupa exactamente cuatro semanas, con lo que las dos últimas filas son todas del mes
/// siguiente.
/// </summary>
public static class MonthGrid
{
    public const int Columns = 7;
    public const int Rows = 6;

    public static IReadOnlyList<CalendarDay> Build(
        DateOnly monthAnchor,
        DateOnly today,
        DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
    {
        var first = new DateOnly(monthAnchor.Year, monthAnchor.Month, 1);

        // El resto en C# puede dar negativo cuando el minuendo es menor, y aquí lo es siempre que
        // el mes empieza antes del primer día de la semana configurado. El +7 lo evita.
        var lead = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        var start = first.AddDays(-lead);

        var days = new List<CalendarDay>(Columns * Rows);
        for (var i = 0; i < Columns * Rows; i++)
        {
            var date = start.AddDays(i);
            days.Add(new CalendarDay(date, date.Month == first.Month && date.Year == first.Year, date == today));
        }

        return days;
    }

    /// <summary>Las cabeceras de columna, en el orden en que se dibuja la rejilla.</summary>
    public static IReadOnlyList<DayOfWeek> WeekdayOrder(DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
    {
        var order = new DayOfWeek[Columns];
        for (var i = 0; i < Columns; i++)
        {
            order[i] = (DayOfWeek)(((int)firstDayOfWeek + i) % 7);
        }

        return order;
    }
}
