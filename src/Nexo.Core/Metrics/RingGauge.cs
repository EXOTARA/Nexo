namespace Nexo.Core.Metrics;

/// <summary>Un punto del arco, en coordenadas de dibujo.</summary>
public readonly record struct GaugePoint(double X, double Y);

/// <summary>Lo que hace falta para dibujar el arco de un medidor circular.</summary>
public readonly record struct GaugeArc(
    GaugePoint Start,
    GaugePoint End,
    bool IsLargeArc,
    bool IsEmpty);

/// <summary>
/// Diseño D42 — la geometría del medidor circular de las cápsulas de rendimiento.
///
/// Vive aquí y no en la vista porque son tres trampas de trigonometría que no se ven mirando la
/// pantalla hasta que aparece el porcentaje concreto que las destapa:
///
/// El arco empieza arriba y no a la derecha. El cero de las funciones trigonométricas está a la
/// derecha; un medidor que empieza ahí se lee torcido, así que hay que restar noventa grados.
///
/// La bandera de «arco largo» es obligatoria pasado el 50%. Sin ella, el dibujo toma siempre el
/// camino corto y un 80% se representa igual que un 20%: dos porcentajes muy distintos con la misma
/// imagen, y nadie lo nota hasta que el equipo está al ochenta por ciento.
///
/// El 100% no se puede dibujar como un arco. Su punto final coincide con el inicial y el trazo
/// desaparece justo cuando más importa. Se recorta un pelo por debajo para que el círculo se cierre
/// visualmente sin degenerar.
/// </summary>
public static class RingGauge
{
    /// <summary>
    /// Tope de barrido. Un arco de 360 grados exactos empieza y termina en el mismo punto, y ahí el
    /// dibujo no sabe qué camino tomar: desaparece.
    /// </summary>
    public const double MaximumSweepDegrees = 359.9;

    /// <summary>
    /// El arco centrado en <c>(radius, radius)</c>, es decir, dentro de una caja de lado 2·radius.
    /// </summary>
    public static GaugeArc Describe(double percent, double radius) =>
        Describe(percent, radius, new GaugePoint(radius, radius));

    /// <summary>
    /// El arco con centro explícito.
    ///
    /// Existe esta sobrecarga porque el centro y el radio del arco casi nunca salen de la misma
    /// cuenta: el carril es una elipse trazada, y su línea media pasa por
    /// <c>(lado - grosor) / 2</c>, no por la mitad del lado. Dar por hecho que centro y radio valen
    /// lo mismo es justo lo que dejaba el arco flotando arriba y a la derecha de su carril, un
    /// defecto que se ve a simple vista en cuanto los dos números dejan de coincidir — y dejan de
    /// coincidir en cuanto alguien cambia el tamaño de una tarjeta.
    /// </summary>
    public static GaugeArc Describe(double percent, double radius, GaugePoint center)
    {
        if (double.IsNaN(percent) || percent <= 0 || radius <= 0)
        {
            return new GaugeArc(
                new GaugePoint(center.X, center.Y - radius),
                new GaugePoint(center.X, center.Y - radius),
                IsLargeArc: false,
                IsEmpty: true);
        }

        var clamped = Math.Clamp(percent, 0, 100);
        var sweep = Math.Min(clamped / 100 * 360, MaximumSweepDegrees);

        // Las doce en punto: menos noventa grados respecto al cero trigonométrico, que está a las
        // tres.
        const double startDegrees = -90;
        var endDegrees = startDegrees + sweep;

        return new GaugeArc(
            PointOnCircle(startDegrees, radius, center),
            PointOnCircle(endDegrees, radius, center),
            IsLargeArc: sweep > 180,
            IsEmpty: false);
    }

    private static GaugePoint PointOnCircle(double degrees, double radius, GaugePoint center)
    {
        var radians = degrees * Math.PI / 180;

        return new GaugePoint(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }

    /// <summary>
    /// El radio de la línea media del carril para un medidor de lado <paramref name="size"/> con
    /// trazo de grosor <paramref name="thickness"/>. Es la cuenta que hace WPF al trazar una elipse
    /// que ocupa toda la caja, y tenerla aquí es lo que hace que el arco y su carril compartan
    /// circunferencia en vez de parecerse.
    /// </summary>
    public static double TrackRadius(double size, double thickness) =>
        Math.Max(0, (size - thickness) / 2);
}
