using System.Text.Json;
using Nexo.Core.Audit;
using Nexo.Windows.Audit;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D13 — el Audit Log orientado al usuario. El modelo de confianza pide que cada acción con
/// efecto real diga qué se hizo, cuándo, con qué permiso y cómo revertirla, y que el registro no se
/// pueda maquillar.
/// </summary>
public sealed class SakuraAuditLogTests : IDisposable
{
    private readonly string _directory;
    private readonly string _auditPath;
    private readonly string _legacyPath;

    public SakuraAuditLogTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "kohana-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _auditPath = Path.Combine(_directory, "audit.json");
        _legacyPath = Path.Combine(_directory, "optimization-audit.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort: no debe hacer fallar la prueba.
        }
    }

    private JsonSakuraAuditLog CreateLog() => new(_auditPath, _legacyPath);

    private static AuditEntry Entry(string action, DateTimeOffset at, string revertHint = "") => new()
    {
        At = at,
        Capability = AuditCapability.Optimizacion,
        Action = action,
        Detail = "detalle",
        Permission = "confirmación del usuario",
        RevertHint = revertHint
    };

    [Fact]
    public void AnEmptyLog_ReadsAsEmpty() => Assert.Empty(CreateLog().Read());

    [Fact]
    public void WhatIsWritten_SurvivesAReopen()
    {
        // Un registro que se pierde al reiniciar no explica por qué el equipo está como está.
        var at = DateTimeOffset.Now;
        CreateLog().Append(Entry("Aplicado (Jugar)", at));

        var entry = Assert.Single(CreateLog().Read());
        Assert.Equal("Aplicado (Jugar)", entry.Action);
        Assert.Equal("confirmación del usuario", entry.Permission);
    }

    [Fact]
    public void TheNewestComesFirst()
    {
        var log = CreateLog();
        var now = DateTimeOffset.Now;

        log.Append(Entry("vieja", now.AddHours(-2)));
        log.Append(Entry("nueva", now));

        Assert.Equal("nueva", log.Read()[0].Action);
    }

    [Fact]
    public void TheLogIsTrimmedByAge_NeverSelectively()
    {
        var log = CreateLog();
        var now = DateTimeOffset.Now;

        for (var index = 0; index < AuditState.MaximumEntries + 25; index++)
        {
            log.Append(Entry($"accion-{index}", now.AddMinutes(index)));
        }

        var entries = log.Read();

        Assert.Equal(AuditState.MaximumEntries, entries.Count);

        // Se van las más viejas, no unas elegidas: la última escrita tiene que seguir ahí.
        Assert.Equal($"accion-{AuditState.MaximumEntries + 24}", entries[0].Action);
    }

    [Fact]
    public void AnEntryWithoutARevertHint_SaysSoInsteadOfStayingQuiet()
    {
        CreateLog().Append(Entry("Memoria borrada", DateTimeOffset.Now));

        var described = Assert.Single(CreateLog().Read()).Describe();

        Assert.Contains("Sin vuelta atrás automática", described);
    }

    [Fact]
    public void AnEntryWithARevertHint_ShowsIt()
    {
        CreateLog().Append(Entry("Aplicado", DateTimeOffset.Now, revertHint: "Deshacer la última optimización."));

        Assert.Contains(
            "Deshacer la última optimización",
            Assert.Single(CreateLog().Read()).Describe());
    }

    [Fact]
    public void ACorruptFile_DoesNotCrashSakura()
    {
        File.WriteAllText(_auditPath, "{ esto no es json válido");

        Assert.Empty(CreateLog().Read());
    }

    // ---------- Importación del registro de D11 ----------

    [Fact]
    public void TheOldOptimizationLog_IsImported_NotAbandoned()
    {
        // Una entrada de auditoría que desaparece porque el formato cambió es exactamente lo que un
        // registro no puede permitirse.
        var legacy = new
        {
            Entries = new[]
            {
                new
                {
                    At = DateTimeOffset.Now.AddDays(-1),
                    Scenario = "Jugar",
                    Action = "Aplicado",
                    Detail = "Listo. Puedes deshacerlo cuando quieras."
                }
            }
        };

        File.WriteAllText(_legacyPath, JsonSerializer.Serialize(legacy));

        var entry = Assert.Single(CreateLog().Read());

        Assert.Equal(AuditCapability.Optimizacion, entry.Capability);
        Assert.Contains("Jugar", entry.Action);
        Assert.Contains("deshacerlo", entry.Detail);
    }

    [Fact]
    public void OnceImported_TheOldLogIsNotReadAgain()
    {
        File.WriteAllText(
            _legacyPath,
            JsonSerializer.Serialize(new
            {
                Entries = new[]
                {
                    new { At = DateTimeOffset.Now, Scenario = "Jugar", Action = "Aplicado", Detail = "una vez" }
                }
            }));

        var log = CreateLog();
        log.Read();
        log.Append(Entry("posterior", DateTimeOffset.Now));

        // Dos entradas, no tres: la importación ocurre una sola vez.
        Assert.Equal(2, CreateLog().Read().Count);
    }
}
