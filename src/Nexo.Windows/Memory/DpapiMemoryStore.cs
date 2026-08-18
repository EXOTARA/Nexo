using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Memory;

namespace Nexo.Windows.Memory;

/// <summary>
/// Diseño D9 (Fase 6) — guarda la memoria cifrada en reposo con DPAPI
/// (<see cref="ProtectedData"/>, <see cref="DataProtectionScope.CurrentUser"/>), la decisión ya
/// registrada en la sesión de roadmap: cifra de verdad y sin obligar al usuario a inventar y
/// recordar una contraseña, porque la clave la administra Windows y va atada a su cuenta.
///
/// Consecuencia asumida y deliberada: el archivo **no** es portable. Copiarlo a otro equipo o a
/// otra cuenta de Windows lo vuelve ilegible. Para memoria personal eso es lo correcto — que no se
/// pueda leer desde otra cuenta es la propiedad que se busca, no un defecto.
///
/// Si el archivo no se puede descifrar (perfil distinto, archivo corrupto), se preserva a un lado y
/// se empieza de cero en vez de tumbar el arranque: perder memoria opt-in es molesto, no poder
/// abrir Kohana es peor.
/// </summary>
public sealed class DpapiMemoryStore : IMemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public DpapiMemoryStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Memory;
    }

    public MemoryState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return new MemoryState();
            }

            try
            {
                var protectedBytes = File.ReadAllBytes(_filePath);
                var plainBytes = ProtectedData.Unprotect(
                    protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<MemoryState>(json, JsonOptions) ?? new MemoryState();
            }
            catch (Exception exception) when (
                exception is CryptographicException or IOException or UnauthorizedAccessException
                    or JsonException)
            {
                PreserveUnreadableFile();
                return new MemoryState();
            }
        }
    }

    public void Save(MemoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), optionalEntropy: null, DataProtectionScope.CurrentUser);

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedBytes);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }

    private void PreserveUnreadableFile()
    {
        try
        {
            File.Move(_filePath, _filePath + $".unreadable-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        }
        catch
        {
            // Un archivo ilegible no debe impedir que Kohana abra.
        }
    }
}
