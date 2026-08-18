using Nexo.Core.Ai;
using Nexo.Windows.Ai;
using Xunit;

namespace Nexo.Windows.Tests;

/// <summary>
/// Pruebas contra disco real y DPAPI real, no contra un doble. Este almacén guarda claves de pago:
/// un falso que devuelva lo que se le guardó demostraría que el diccionario funciona, no que el
/// cifrado, el archivo temporal y el reemplazo atómico funcionan — que es lo único que puede fallar.
/// </summary>
public sealed class DpapiAiApiKeyStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public DpapiAiApiKeyStoreTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "kohana-apikey-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "ai-credentials.dat");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Un temporal que no se pudo borrar no debe hacer fallar la prueba.
        }
    }

    [Fact]
    public void AKeyComesBackExactlyAsItWentIn()
    {
        var store = new DpapiAiApiKeyStore(_filePath);

        store.Write(AiProviderKind.Groq, "gsk_ejemplo-1234567890");

        Assert.Equal("gsk_ejemplo-1234567890", store.Read(AiProviderKind.Groq));
    }

    [Fact]
    public void TheKeyIsNotReadableInPlainTextOnDisk()
    {
        // La propiedad que justifica todo el archivo: quien abra el .dat no debe encontrar la clave.
        var store = new DpapiAiApiKeyStore(_filePath);
        store.Write(AiProviderKind.Anthropic, "sk-ant-ejemplo-secreto");

        var bytes = File.ReadAllBytes(_filePath);
        var asText = System.Text.Encoding.UTF8.GetString(bytes);
        var asUnicode = System.Text.Encoding.Unicode.GetString(bytes);

        Assert.DoesNotContain("sk-ant-ejemplo-secreto", asText, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-ant-ejemplo-secreto", asUnicode, StringComparison.Ordinal);
    }

    [Fact]
    public void EachProviderKeepsItsOwnKey()
    {
        // Cambiar de Groq a Gemini y volver no debe obligar a pegar la clave otra vez.
        var store = new DpapiAiApiKeyStore(_filePath);

        store.Write(AiProviderKind.Groq, "clave-groq");
        store.Write(AiProviderKind.Gemini, "clave-gemini");

        Assert.Equal("clave-groq", store.Read(AiProviderKind.Groq));
        Assert.Equal("clave-gemini", store.Read(AiProviderKind.Gemini));
    }

    [Fact]
    public void RemovingOneProviderLeavesTheOthersAlone()
    {
        var store = new DpapiAiApiKeyStore(_filePath);
        store.Write(AiProviderKind.Groq, "clave-groq");
        store.Write(AiProviderKind.Gemini, "clave-gemini");

        store.Remove(AiProviderKind.Groq);

        Assert.Null(store.Read(AiProviderKind.Groq));
        Assert.Equal("clave-gemini", store.Read(AiProviderKind.Gemini));
    }

    [Fact]
    public void WritingAnEmptyKeyRemovesItInsteadOfStoringBlank()
    {
        // Es el camino del botón "Borrar la clave" y del cuadro vaciado a mano. Guardar una cadena
        // vacía haría que la aplicación creyera tener credencial y fallara con un 401 confuso en vez
        // de decir que falta la clave.
        var store = new DpapiAiApiKeyStore(_filePath);
        store.Write(AiProviderKind.Groq, "clave-groq");

        store.Write(AiProviderKind.Groq, "   ");

        Assert.Null(store.Read(AiProviderKind.Groq));
    }

    [Fact]
    public void TheFileDisappearsWhenTheLastKeyIsRemoved()
    {
        // Sin claves no debe quedar un archivo cifrado vacío rondando en la carpeta de datos.
        var store = new DpapiAiApiKeyStore(_filePath);
        store.Write(AiProviderKind.Groq, "clave-groq");
        Assert.True(File.Exists(_filePath));

        store.Remove(AiProviderKind.Groq);

        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public void SurroundingWhitespaceIsTrimmedBeforeStoring()
    {
        // Pegar una clave desde una página web arrastra saltos de línea con una facilidad pasmosa, y
        // una cabecera Authorization con un salto dentro es un 401 sin explicación.
        var store = new DpapiAiApiKeyStore(_filePath);

        store.Write(AiProviderKind.Groq, "  clave-groq\r\n");

        Assert.Equal("clave-groq", store.Read(AiProviderKind.Groq));
    }

    [Fact]
    public void ReadingAProviderThatWasNeverStoredReturnsNull()
    {
        var store = new DpapiAiApiKeyStore(_filePath);

        Assert.Null(store.Read(AiProviderKind.OpenAI));
    }

    [Fact]
    public void AnUnreadableFileIsSetAsideInsteadOfBreakingStartup()
    {
        // Pasa de verdad: el archivo copiado desde otra cuenta de Windows no se puede descifrar.
        // Eso no puede impedir abrir Kohana ni configurar una clave nueva.
        File.WriteAllBytes(_filePath, [0x00, 0x01, 0x02, 0x03, 0x04]);
        var store = new DpapiAiApiKeyStore(_filePath);

        Assert.Null(store.Read(AiProviderKind.Groq));

        store.Write(AiProviderKind.Groq, "clave-nueva");
        Assert.Equal("clave-nueva", store.Read(AiProviderKind.Groq));

        var preserved = Directory.GetFiles(_directory, "*.unreadable-*");
        Assert.NotEmpty(preserved);
    }

    [Fact]
    public void ASecondStoreOverTheSameFileSeesWhatTheFirstWrote()
    {
        // Kohana no mantiene el almacén vivo entre sesiones: al reabrir se construye otro sobre el
        // mismo archivo. Si la lectura dependiera del estado en memoria, la clave se "perdería" en
        // cada reinicio.
        new DpapiAiApiKeyStore(_filePath).Write(AiProviderKind.OpenRouter, "clave-openrouter");

        Assert.Equal(
            "clave-openrouter",
            new DpapiAiApiKeyStore(_filePath).Read(AiProviderKind.OpenRouter));
    }

    [Fact]
    public void TheStoredKeyTakesPrecedenceOverTheEnvironmentVariable()
    {
        // Las dos vías conviven: la variable de entorno sigue funcionando para quien ya la tenía,
        // pero lo que alguien acaba de escribir en Kohana es lo que quiere usar ahora.
        var store = new DpapiAiApiKeyStore(_filePath);
        store.Write(AiProviderKind.Groq, "clave-escrita-en-kohana");

        var configuration = new AiProviderConfiguration(
            AiProviderKind.Groq,
            "https://api.groq.com/openai/v1",
            "llama-3.3-70b-versatile",
            "KOHANA_TEST_GROQ_KEY")
        {
            StoredApiKey = store.Read(AiProviderKind.Groq)
        };

        Environment.SetEnvironmentVariable(
            "KOHANA_TEST_GROQ_KEY", "clave-de-variable", EnvironmentVariableTarget.Process);
        try
        {
            Assert.Equal("clave-escrita-en-kohana", configuration.ReadApiKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "KOHANA_TEST_GROQ_KEY", null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void WithoutAStoredKeyTheEnvironmentVariableStillWorks()
    {
        var configuration = new AiProviderConfiguration(
            AiProviderKind.Groq,
            "https://api.groq.com/openai/v1",
            "llama-3.3-70b-versatile",
            "KOHANA_TEST_GROQ_KEY");

        Environment.SetEnvironmentVariable(
            "KOHANA_TEST_GROQ_KEY", "clave-de-variable", EnvironmentVariableTarget.Process);
        try
        {
            Assert.Equal("clave-de-variable", configuration.ReadApiKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "KOHANA_TEST_GROQ_KEY", null, EnvironmentVariableTarget.Process);
        }
    }
}
