using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Nexo.Core.Audio;

namespace Nexo.Windows.Audio;

public sealed class WindowsAudioMixerService : IAudioMixerService
{
    public AudioMixerSnapshot ReadSnapshot()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            var endpointVolume = device.AudioEndpointVolume;
            var sessions = ReadSessions(device);

            return new AudioMixerSnapshot(
                true,
                device.FriendlyName,
                ToPercent(endpointVolume.MasterVolumeLevelScalar),
                endpointVolume.Mute,
                sessions);
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioMixerSnapshot.Unavailable(
                "No encontré un dispositivo de salida de audio disponible.");
        }
    }

    public AudioActionResult SetMasterVolume(double percent)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var clamped = ClampPercent(percent);

            device.AudioEndpointVolume.MasterVolumeLevelScalar = ToScalar(clamped);
            if (clamped > 0)
            {
                device.AudioEndpointVolume.Mute = false;
            }

            return AudioActionResult.Success(
                "Volumen general actualizado",
                $"El volumen general quedó al {clamped:0}%.",
                clamped);
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioActionResult.Unavailable("No pude acceder al dispositivo de audio principal.");
        }
    }

    public AudioActionResult SetMasterMuted(bool muted)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = muted;

            return AudioActionResult.Success(
                muted ? "Audio general silenciado" : "Audio general activado",
                muted ? "Se silenció la salida principal." : "Se reactivó la salida principal.");
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioActionResult.Unavailable("No pude acceder al dispositivo de audio principal.");
        }
    }

    public AudioActionResult SetSessionVolume(string sessionId, double percent) =>
        ChangeSessionById(
            sessionId,
            session =>
            {
                var clamped = ClampPercent(percent);
                session.SimpleAudioVolume.Volume = ToScalar(clamped);
                if (clamped > 0)
                {
                    session.SimpleAudioVolume.Mute = false;
                }

                return clamped;
            },
            "Volumen actualizado");

    public AudioActionResult SetSessionMuted(string sessionId, bool muted) =>
        ChangeSessionById(
            sessionId,
            session =>
            {
                session.SimpleAudioVolume.Mute = muted;
                return ToPercent(session.SimpleAudioVolume.Volume);
            },
            muted ? "Aplicación silenciada" : "Sonido activado");

    public AudioActionResult SetApplicationVolume(string target, double percent)
    {
        var clamped = ClampPercent(percent);
        return ChangeMatchingSessions(
            target,
            session =>
            {
                session.SimpleAudioVolume.Volume = ToScalar(clamped);
                if (clamped > 0)
                {
                    session.SimpleAudioVolume.Mute = false;
                }

                return clamped;
            },
            displayName => $"{displayName} quedó al {clamped:0}%");
    }

    public AudioActionResult ScaleApplicationVolume(string target, double factor)
    {
        var safeFactor = Math.Clamp(factor, 0, 2);
        return ChangeMatchingSessions(
            target,
            session =>
            {
                var current = ToPercent(session.SimpleAudioVolume.Volume);
                var updated = ClampPercent(current * safeFactor);
                session.SimpleAudioVolume.Volume = ToScalar(updated);
                if (updated > 0)
                {
                    session.SimpleAudioVolume.Mute = false;
                }

                return updated;
            },
            displayName => $"Se ajustó el volumen de {displayName}");
    }

    public AudioActionResult ChangeApplicationVolume(string target, double deltaPoints) =>
        ChangeMatchingSessions(
            target,
            session =>
            {
                var current = ToPercent(session.SimpleAudioVolume.Volume);
                var updated = ClampPercent(current + deltaPoints);
                session.SimpleAudioVolume.Volume = ToScalar(updated);
                if (updated > 0)
                {
                    session.SimpleAudioVolume.Mute = false;
                }

                return updated;
            },
            displayName => $"Se ajustó el volumen de {displayName}");

    public AudioActionResult SetApplicationMuted(string target, bool muted) =>
        ChangeMatchingSessions(
            target,
            session =>
            {
                session.SimpleAudioVolume.Mute = muted;
                return ToPercent(session.SimpleAudioVolume.Volume);
            },
            displayName => muted
                ? $"{displayName} quedó silenciado"
                : $"Se reactivó el sonido de {displayName}");

    public AudioActionResult LowerAllExcept(string target, double factor)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            var targetExists = false;
            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                var identity = ReadIdentity(session, index);
                if (AudioTargetMatcher.IsMatch(target, identity.ProcessName, identity.DisplayName))
                {
                    targetExists = true;
                    break;
                }
            }

            if (!targetExists)
            {
                return AudioActionResult.NotFound(ToDisplayName(target));
            }

            var changed = 0;
            var safeFactor = Math.Clamp(factor, 0, 1);
            manager.RefreshSessions();
            sessions = manager.Sessions;

            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                if (session.State == AudioSessionState.AudioSessionStateExpired)
                {
                    continue;
                }

                var identity = ReadIdentity(session, index);
                if (AudioTargetMatcher.IsMatch(target, identity.ProcessName, identity.DisplayName))
                {
                    continue;
                }

                var current = ToPercent(session.SimpleAudioVolume.Volume);
                var updated = ClampPercent(current * safeFactor);
                session.SimpleAudioVolume.Volume = ToScalar(updated);
                changed++;
            }

            return AudioActionResult.Success(
                "Volúmenes ajustados",
                changed == 0
                    ? $"No había otras sesiones activas aparte de {ToDisplayName(target)}."
                    : $"Se bajaron {changed} sesiones y se mantuvo {ToDisplayName(target)}.");
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioActionResult.Unavailable("No pude acceder a las sesiones de audio de Windows.");
        }
    }

    private static IReadOnlyList<AudioSessionSnapshot> ReadSessions(MMDevice device)
    {
        var result = new List<AudioSessionSnapshot>();
        var manager = device.AudioSessionManager;
        manager.RefreshSessions();
        var sessions = manager.Sessions;

        for (var index = 0; index < sessions.Count; index++)
        {
            try
            {
                using var session = sessions[index];
                if (session.State == AudioSessionState.AudioSessionStateExpired)
                {
                    continue;
                }

                var identity = ReadIdentity(session, index);
                result.Add(new AudioSessionSnapshot(
                    identity.SessionId,
                    identity.ProcessId,
                    identity.ProcessName,
                    identity.DisplayName,
                    ToPercent(session.SimpleAudioVolume.Volume),
                    session.SimpleAudioVolume.Mute,
                    session.State == AudioSessionState.AudioSessionStateActive,
                    identity.IsSystemSounds));
            }
            catch (Exception exception) when (IsExpectedAudioException(exception))
            {
                // Una sesión puede desaparecer mientras se enumera. Se ignora y se continúa.
            }
        }

        return result
            .OrderByDescending(session => session.IsActive)
            .ThenBy(session => session.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private AudioActionResult ChangeSessionById(
        string sessionId,
        Func<AudioSessionControl, double> change,
        string title)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                var identity = ReadIdentity(session, index);
                if (!identity.SessionId.Equals(sessionId, StringComparison.Ordinal))
                {
                    continue;
                }

                var volume = change(session);
                return AudioActionResult.Success(
                    title,
                    $"{identity.DisplayName} · {volume:0}%",
                    volume);
            }

            return AudioActionResult.NotFound("esa aplicación");
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioActionResult.Unavailable("La sesión de audio dejó de estar disponible.");
        }
    }

    private AudioActionResult ChangeMatchingSessions(
        string target,
        Func<AudioSessionControl, double> change,
        Func<string, string> detailFactory)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return AudioActionResult.NotFound("la aplicación indicada");
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            var matched = 0;
            var totalVolume = 0d;
            string? firstDisplayName = null;

            for (var index = 0; index < sessions.Count; index++)
            {
                using var session = sessions[index];
                if (session.State == AudioSessionState.AudioSessionStateExpired)
                {
                    continue;
                }

                var identity = ReadIdentity(session, index);
                if (!AudioTargetMatcher.IsMatch(target, identity.ProcessName, identity.DisplayName))
                {
                    continue;
                }

                var updatedVolume = change(session);
                matched++;
                totalVolume += updatedVolume;
                firstDisplayName ??= identity.DisplayName;
            }

            if (matched == 0)
            {
                return AudioActionResult.NotFound(ToDisplayName(target));
            }

            var averageVolume = totalVolume / matched;
            var displayName = firstDisplayName ?? ToDisplayName(target);
            return AudioActionResult.Success(
                "Audio actualizado",
                $"{detailFactory(displayName)} · {averageVolume:0}%",
                averageVolume);
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return AudioActionResult.Unavailable("No pude acceder a las sesiones de audio de Windows.");
        }
    }

    private static SessionIdentity ReadIdentity(AudioSessionControl session, int index)
    {
        var isSystemSounds = TryRead(() => session.IsSystemSoundsSession, false);
        var rawProcessId = session.GetProcessID;
        var processId = rawProcessId > int.MaxValue ? 0 : (int)rawProcessId;
        var processName = isSystemSounds ? "system sounds" : string.Empty;
        var displayName = TryRead(() => session.DisplayName, string.Empty);

        if (isSystemSounds)
        {
            displayName = "Sonidos del sistema";
        }
        else if (processId > 0)
        {
            TryReadProcessIdentity(processId, ref processName, ref displayName);
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            processName = displayName;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = ToDisplayName(processName);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Sesión de audio";
        }

        var sessionId = TryRead(
            () => session.GetSessionInstanceIdentifier,
            $"{processId}:{index}:{session.GetHashCode()}");

        return new SessionIdentity(
            sessionId,
            processId,
            processName,
            FriendlyDisplayName(displayName, processName),
            isSystemSounds);
    }

    private static void TryReadProcessIdentity(
        int processId,
        ref string processName,
        ref string displayName)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            processName = process.ProcessName;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = process.MainWindowTitle;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                try
                {
                    displayName = process.MainModule?.FileVersionInfo.FileDescription ?? string.Empty;
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Algunos procesos protegidos no permiten leer MainModule.
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // El proceso pudo cerrarse entre la enumeración y esta lectura.
        }
    }

    private static string FriendlyDisplayName(string displayName, string processName)
    {
        var normalizedProcess = AudioTargetMatcher.Normalize(processName);
        return normalizedProcess switch
        {
            "msedge" => "Microsoft Edge",
            "applicationframehost" when !string.IsNullOrWhiteSpace(displayName) => displayName,
            "discord" => "Discord",
            "spotify" => "Spotify",
            "zen" => "Zen",
            "firefox" => "Firefox",
            "chrome" => "Google Chrome",
            _ => displayName
        };
    }

    private static T TryRead<T>(Func<T> reader, T fallback)
    {
        try
        {
            return reader();
        }
        catch (Exception exception) when (IsExpectedAudioException(exception))
        {
            return fallback;
        }
    }

    private static bool IsExpectedAudioException(Exception exception) =>
        exception is COMException or
        InvalidOperationException or
        ArgumentException or
        UnauthorizedAccessException or
        System.ComponentModel.Win32Exception;

    private static double ToPercent(float scalar) =>
        Math.Round(Math.Clamp(scalar, 0f, 1f) * 100d, 1);

    private static float ToScalar(double percent) =>
        (float)(ClampPercent(percent) / 100d);

    private static double ClampPercent(double percent) =>
        Math.Clamp(percent, 0d, 100d);

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "la aplicación";
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private sealed record SessionIdentity(
        string SessionId,
        int ProcessId,
        string ProcessName,
        string DisplayName,
        bool IsSystemSounds);
}
