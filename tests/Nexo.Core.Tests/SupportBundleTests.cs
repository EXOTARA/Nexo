using Nexo.Core.Audit;
using Nexo.Core.Diagnostics;
using Nexo.Core.Productization;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D21 (Fase 9) — el paquete de soporte sale del equipo y llega a otra persona, así que se
/// prueba como un envío: lo que se redacta, lo que no entra, y que se diga qué se dejó fuera.
/// </summary>
public sealed class SupportBundleTests
{
    private static NexoDiagnosticSnapshot Diagnostics(params DiagnosticItem[] items) => new(
        DateTimeOffset.Now,
        "0.11.0-beta",
        "Windows 11",
        ".NET 10",
        @"C:\Users\adler\AppData\Local\Kohana",
        items);

    private static DiagnosticItem Item(string detail) =>
        new("Prueba", DiagnosticStatus.Ready, detail);

    private static string Build(NexoDiagnosticSnapshot diagnostics, params AuditEntry[] entries) =>
        SupportBundleBuilder.Build(diagnostics, entries, _ => false);

    // ---------- Lo que se redacta ----------

    [Fact]
    public void TheUserFolderNameNeverAppears()
    {
        // Sin esto, cada línea con una ruta revela cómo se llama la persona.
        var bundle = Build(Diagnostics(Item(@"Modelo en C:\Users\adler\AppData\Local\Kohana\models")));

        Assert.DoesNotContain("adler", bundle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersonalDataInADiagnosticDetail_IsRedacted()
    {
        var bundle = Build(Diagnostics(Item("El usuario escribió: mi contraseña es Sakura2026")));

        Assert.DoesNotContain("Sakura2026", bundle);
    }

    [Fact]
    public void CodeSecretsInADiagnosticDetail_AreRedactedToo()
    {
        // Las dos herramientas, no una: cada una ve lo que la otra no.
        var bundle = Build(Diagnostics(Item("Configuración: ApiKey=sk-abcdef1234567890")));

        Assert.DoesNotContain("sk-abcdef1234567890", bundle);
    }

    [Fact]
    public void SecretsInsideAnAuditEntry_AreRedacted()
    {
        var entry = new AuditEntry
        {
            At = DateTimeOffset.Now,
            Capability = AuditCapability.Proyecto,
            Action = "Archivo modificado",
            Detail = "appsettings.json — Password=Sakura2026;"
        };

        Assert.DoesNotContain("Sakura2026", Build(Diagnostics(), entry));
    }

    // ---------- Lo que sí entra ----------

    [Fact]
    public void VersionsAndSubsystemStatusAreIncluded()
    {
        var bundle = Build(Diagnostics(Item("Whisper listo")));

        Assert.Contains("0.11.0-beta", bundle);
        Assert.Contains("Whisper listo", bundle);
    }

    [Fact]
    public void TheInventorySaysWhichFilesExist_NotWhatIsInThem()
    {
        var bundle = SupportBundleBuilder.Build(
            Diagnostics(), [], path => path.EndsWith("settings.json", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("settings.json: existe", bundle);
        Assert.Contains("memory.dat: no existe", bundle);
    }

    [Fact]
    public void RecentActionsAreIncluded_ButCapped()
    {
        var entries = Enumerable.Range(0, SupportBundleBuilder.AuditEntriesIncluded + 20)
            .Select(index => new AuditEntry
            {
                At = DateTimeOffset.Now.AddMinutes(-index),
                Capability = AuditCapability.Optimizacion,
                Action = $"accion-{index}",
                Detail = "detalle"
            })
            .ToArray();

        var bundle = Build(Diagnostics(), entries);

        Assert.Contains("accion-0", bundle);
        Assert.DoesNotContain($"accion-{SupportBundleBuilder.AuditEntriesIncluded + 10}", bundle);
    }

    [Fact]
    public void WithNoActivity_ItSaysSo() =>
        Assert.Contains("(ninguna)", Build(Diagnostics()));

    // ---------- Lo que se deja fuera, dicho ----------

    [Fact]
    public void TheBundleListsWhatItLeftOut()
    {
        // Un paquete que omite en silencio obliga a confiar; uno que enumera lo omitido se puede
        // revisar, y la persona debería poder revisar lo que manda.
        var bundle = Build(Diagnostics());

        Assert.Contains("Qué NO se incluye", bundle);
        Assert.Contains("memoria personal", bundle);
        Assert.Contains("conversaciones", bundle);
        Assert.Contains("proyecto", bundle);
    }

    [Fact]
    public void ItInvitesYouToReadItBeforeSendingIt() =>
        Assert.Contains("antes de mandárselo", Build(Diagnostics()));

    // ---------- Informe de privacidad ----------

    [Fact]
    public void ThePrivacyReportSeparatesYourDataFromOperationalData()
    {
        var report = PrivacyReportBuilder.Build(@"C:\Users\adler\AppData\Local\Kohana", _ => false, _ => 0);

        Assert.Contains("Datos tuyos", report);
        Assert.Contains("Datos de funcionamiento", report);
        Assert.DoesNotContain("adler", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePrivacyReportSaysHowToDeleteThings()
    {
        var report = PrivacyReportBuilder.Build(@"C:\Kohana", _ => false, _ => 0);

        Assert.Contains("Cómo borrarlo", report);
        Assert.Contains("Olvidar todo", report);
    }

    [Fact]
    public void ThePrivacyReportSaysWhatIsEncrypted()
    {
        var report = PrivacyReportBuilder.Build(@"C:\Kohana", _ => true, _ => 2048);

        Assert.Contains("cifrado", report);
    }

    [Fact]
    public void ThePrivacyReportNeverShowsContent()
    {
        // Enseñar los datos para demostrar que se guardan sería contradictorio.
        var report = PrivacyReportBuilder.Build(@"C:\Kohana", _ => true, _ => 100);

        Assert.Contains("No incluye el contenido de nada", report);
    }

    [Fact]
    public void ThePrivacyReportCoversTheWholeInventory()
    {
        var report = PrivacyReportBuilder.Build(@"C:\Kohana", _ => true, _ => 10);

        Assert.All(KohanaDataInventory.All, item => Assert.Contains(item.FileName, report));
    }
}
