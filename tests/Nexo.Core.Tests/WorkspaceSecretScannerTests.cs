using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D12 (Fase 5) — lo que se prueba aquí es lo que NO debe salir del equipo cuando Kohana
/// explica un proyecto.
/// </summary>
public sealed class WorkspaceSecretScannerTests
{
    [Theory]
    [InlineData("var apiKey = \"sk-abcdef1234567890\";")]
    [InlineData("API_KEY=abcdef1234567890")]
    [InlineData("client_secret: 9f8e7d6c5b4a3210")]
    [InlineData("const accessToken = 'ghp_abcdefghijklmnop'")]
    public void SecretLookingAssignments_AreRedacted(string line)
    {
        var result = WorkspaceSecretScanner.Redact(line);

        Assert.Contains(WorkspaceSecretScanner.Placeholder, result);
        Assert.True(WorkspaceSecretScanner.ContainsSecret(line));
    }

    [Fact]
    public void TheVariableNameSurvives_OnlyTheValueGoes()
    {
        // Para explicar un proyecto sirve saber que existe ApiKey; su valor no aporta nada.
        var result = WorkspaceSecretScanner.Redact("var apiKey = \"sk-abcdef1234567890\";");

        Assert.Contains("apiKey", result);
        Assert.DoesNotContain("sk-abcdef1234567890", result);
    }

    [Theory]
    [InlineData("apiKey = \"\"")]
    [InlineData("token: null")]
    [InlineData("password = x")]
    public void EmptyPlaceholders_AreNotSecrets(string line) =>
        // Redactarlos solo escondería que el hueco está vacío.
        Assert.False(WorkspaceSecretScanner.ContainsSecret(line));

    [Fact]
    public void ConnectionStringsWithCredentials_AreRedacted()
    {
        const string line = "Server=db;Database=kohana;User Id=sa;Password=Sakura2026;";

        var result = WorkspaceSecretScanner.Redact(line);

        Assert.DoesNotContain("Sakura2026", result);
        Assert.Contains("Database=kohana", result);
    }

    [Fact]
    public void UrlsWithEmbeddedCredentials_AreRedacted()
    {
        const string line = "postgres://usuario:clavesecreta@servidor:5432/basedatos";

        Assert.DoesNotContain("clavesecreta", WorkspaceSecretScanner.Redact(line));
    }

    [Fact]
    public void APrivateKeyBlock_IsCoveredWhole_NotJustItsHeader()
    {
        var block = string.Join(
            Environment.NewLine,
            "-----BEGIN RSA PRIVATE KEY-----",
            "MIIEowIBAAKCAQEAxGrq8Bl9",
            "hQIDAQABAoIBAAqwerty1234",
            "-----END RSA PRIVATE KEY-----");

        var result = WorkspaceSecretScanner.Redact(block);

        Assert.DoesNotContain("MIIEowIBAAKCAQEAxGrq8Bl9", result);
        Assert.Contains(WorkspaceSecretScanner.Placeholder, result);
    }

    [Fact]
    public void OrdinaryCode_IsLeftAlone()
    {
        const string code = "public int Sumar(int a, int b) => a + b;";

        Assert.Equal(code, WorkspaceSecretScanner.Redact(code));
        Assert.False(WorkspaceSecretScanner.ContainsSecret(code));
    }
}
