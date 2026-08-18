using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class SensitiveContentRedactorTests
{
    [Theory]
    [InlineData("Número de tarjeta: 4111 1111 1111 1111")]
    [InlineData("4111111111111111")]
    public void Redact_CardNumberPassingLuhn_IsRedacted(string text)
    {
        var result = SensitiveContentRedactor.Redact(text);

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
        Assert.DoesNotContain("4111", result);
    }

    [Fact]
    public void Redact_DigitsThatFailLuhn_AreLeftAlone()
    {
        // Mismo largo que una tarjeta real pero no pasa la suma de Luhn.
        const string text = "Número de referencia: 1234 5678 9012 3456";

        var result = SensitiveContentRedactor.Redact(text);

        Assert.Equal(text, result);
    }

    [Theory]
    [InlineData("Password: hunter2")]
    [InlineData("password=hunter2")]
    [InlineData("Contraseña: MiClaveSecreta1")]
    [InlineData("clave=abc123")]
    public void Redact_PasswordLabeledField_RedactsOnlyTheValue(string text)
    {
        var result = SensitiveContentRedactor.Redact(text);

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
        // La etiqueta ("Password", "Contraseña", "clave") sigue visible: solo se oculta el valor.
        Assert.Matches("(?i)(password|contraseña|clave)", result);
    }

    /// <summary>
    /// Diseño D10 — la forma hablada. La memoria y el dictado guardan prosa, no campos de
    /// formulario: sin esto, "mi contraseña es hunter2" se guardaba tal cual.
    /// </summary>
    [Theory]
    [InlineData("mi contraseña es hunter2")]
    [InlineData("la clave es abc123")]
    [InlineData("mi password era hunter2")]
    public void Redact_SpokenPasswordForm_IsAlsoRedacted(string text)
    {
        var result = SensitiveContentRedactor.Redact(text);

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
        Assert.True(SensitiveContentRedactor.ContainsSensitiveContent(text));
    }

    [Fact]
    public void Redact_Ssn_IsRedacted()
    {
        const string text = "SSN: 123-45-6789";

        var result = SensitiveContentRedactor.Redact(text);

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
        Assert.DoesNotContain("123-45-6789", result);
    }

    [Theory]
    [InlineData("sk-abcdef1234567890abcdef1234567890")]
    [InlineData("ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    [InlineData("AKIAABCDEFGHIJKLMNOP")]
    public void Redact_KnownSecretPrefix_IsRedacted(string token)
    {
        var result = SensitiveContentRedactor.Redact($"Token: {token}");

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
        Assert.DoesNotContain(token, result);
    }

    [Fact]
    public void Redact_RandomLookingMixedCaseToken_IsRedacted()
    {
        const string text = "Api key: gA3dE9fG2hJ4kL7mN0pQrS5t";

        var result = SensitiveContentRedactor.Redact(text);

        Assert.Contains(SensitiveContentRedactor.Placeholder, result);
    }

    [Theory]
    [InlineData("Hola, esta es una ventana normal sin nada sensible.")]
    [InlineData("https://ejemplo.com/una/ruta/bastante/larga/pero/normal")]
    [InlineData("El archivo se guardó correctamente en el escritorio.")]
    public void Redact_OrdinaryText_IsLeftUnchanged(string text)
    {
        var result = SensitiveContentRedactor.Redact(text);

        Assert.Equal(text, result);
    }

    [Fact]
    public void ContainsSensitiveContent_ReflectsWhetherRedactionChangedTheText()
    {
        Assert.True(SensitiveContentRedactor.ContainsSensitiveContent("Password: hunter2"));
        Assert.False(SensitiveContentRedactor.ContainsSensitiveContent("Hola, buenos días."));
    }

    [Fact]
    public void ContainsSensitiveContent_WithNullOrEmpty_ReturnsFalse()
    {
        Assert.False(SensitiveContentRedactor.ContainsSensitiveContent(null));
        Assert.False(SensitiveContentRedactor.ContainsSensitiveContent(string.Empty));
    }

    [Fact]
    public void Redact_OcrResult_RedactsEachLineAndRebuildsFullText()
    {
        var ocrResult = OcrResult.Success(
            "Usuario: adler\nPassword: hunter2",
            [
                new OcrTextLine("Usuario: adler", 0, 0, 100, 20),
                new OcrTextLine("Password: hunter2", 0, 20, 100, 20)
            ]);

        var redacted = SensitiveContentRedactor.Redact(ocrResult);

        Assert.Equal("Usuario: adler", redacted.Lines[0].Text);
        Assert.Contains(SensitiveContentRedactor.Placeholder, redacted.Lines[1].Text);
        Assert.Contains(SensitiveContentRedactor.Placeholder, redacted.FullText);
        Assert.DoesNotContain("hunter2", redacted.FullText);
    }

    [Fact]
    public void Redact_FailedOcrResult_IsReturnedUnchanged()
    {
        var failed = OcrResult.Failed("motivo");

        var redacted = SensitiveContentRedactor.Redact(failed);

        Assert.Same(failed, redacted);
    }

    [Fact]
    public void Redact_UiAutomationElements_RedactsNamesButKeepsBoundsAndNullNames()
    {
        var elements = new[]
        {
            new UiAutomationElement("Password: hunter2", "ControlType.Edit", 0, 0, 50, 20),
            new UiAutomationElement(null, "ControlType.Pane", 0, 20, 50, 20)
        };

        var redacted = SensitiveContentRedactor.Redact(elements);

        Assert.Contains(SensitiveContentRedactor.Placeholder, redacted[0].Name);
        Assert.Equal(50, redacted[0].Width);
        Assert.Null(redacted[1].Name);
    }
}
