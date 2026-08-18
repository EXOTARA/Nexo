using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexo.Core.Ai;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Ai;

/// <summary>
/// Claves de API cifradas en reposo con DPAPI, igual que la memoria personal
/// (<c>DpapiMemoryStore</c>) y por la misma razón: cifra de verdad y la llave la administra Windows
/// atada a la cuenta, sin pedirle a nadie que invente otra contraseña.
///
/// Consecuencia deliberada: el archivo no es portable. Copiarlo a otro equipo o a otra cuenta lo
/// vuelve ilegible — que la clave de pago de alguien no se pueda leer desde otra cuenta es la
/// propiedad que se busca.
///
/// Va en su propio archivo, separado de <c>settings.json</c>, porque los ajustes se comparten en
/// diagnósticos y capturas de pantalla. Una clave que vive en el mismo archivo que el color de
/// acento acaba pegada en un mensaje de soporte.
/// </summary>
public sealed class DpapiAiApiKeyStore : IAiApiKeyStore
{
    private readonly object _sync = new();
    private readonly string _filePath;

    public DpapiAiApiKeyStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.AiCredentials;
    }

    public string? Read(AiProviderKind provider)
    {
        lock (_sync)
        {
            var keys = LoadCore();
            return keys.TryGetValue(KeyOf(provider), out var apiKey) &&
                   !string.IsNullOrWhiteSpace(apiKey)
                ? apiKey
                : null;
        }
    }

    public void Write(AiProviderKind provider, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        lock (_sync)
        {
            var keys = LoadCore();
            var trimmed = apiKey.Trim();

            if (trimmed.Length == 0)
            {
                if (!keys.Remove(KeyOf(provider)))
                {
                    return;
                }
            }
            else
            {
                keys[KeyOf(provider)] = trimmed;
            }

            SaveCore(keys);
        }
    }

    public void Remove(AiProviderKind provider)
    {
        lock (_sync)
        {
            var keys = LoadCore();
            if (keys.Remove(KeyOf(provider)))
            {
                SaveCore(keys);
            }
        }
    }

    private Dictionary<string, string> LoadCore()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_filePath);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is CryptographicException or IOException or UnauthorizedAccessException
                or JsonException)
        {
            // Un archivo de otra cuenta de Windows o corrupto no debe impedir abrir Kohana ni
            // configurar una clave nueva. Se aparta y se empieza de cero.
            TryPreserveUnreadableFile();
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCore(Dictionary<string, string> keys)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (keys.Count == 0)
        {
            TryDelete();
            return;
        }

        var json = JsonSerializer.Serialize(keys);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json), optionalEntropy: null, DataProtectionScope.CurrentUser);

        var temporaryPath = _filePath + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedBytes);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private void TryDelete()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Quedará vacío en el próximo guardado.
        }
    }

    private void TryPreserveUnreadableFile()
    {
        try
        {
            File.Move(
                _filePath,
                _filePath + $".unreadable-{DateTime.Now:yyyyMMdd-HHmmss}",
                overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Un archivo ilegible no debe impedir que Kohana abra.
        }
    }

    private static string KeyOf(AiProviderKind provider) => provider.ToString();
}
