namespace Nexo.Core.Settings;

public enum SidebarPosition
{
    Left,
    Right
}

public sealed class ShellPreferences
{
    public int SchemaVersion { get; set; }
    public SidebarPosition Position { get; set; } = SidebarPosition.Right;

    public double Width { get; set; } = 420;

    public double Opacity { get; set; } = 0.96;

    public string AccentColor { get; set; } = "#8B6CFF";

    public bool AnimationsEnabled { get; set; } = true;

    public bool ShowAudioModule { get; set; } = true;

    public bool ShowCaptureModule { get; set; } = true;

    public bool ShowSystemModule { get; set; } = true;

    public bool PeekEnabled { get; set; } = true;

    public bool ShowCpuInPeek { get; set; } = true;

    public bool ShowMemoryInPeek { get; set; } = true;

    public bool ShowGpuInPeek { get; set; } = true;

    public bool ShowDiskInPeek { get; set; }

    public bool ShowTopProcessInPeek { get; set; } = true;

    public bool SaveConversationHistory { get; set; }

    public int RecentConversationMessageLimit { get; set; } = 8;

    public void Normalize()
    {
        if (SchemaVersion < 2)
        {
            ShowGpuInPeek = true;
            ShowDiskInPeek = false;
            SchemaVersion = 2;
        }

        if (SchemaVersion < 3)
        {
            RecentConversationMessageLimit = 8;
            SchemaVersion = 3;
        }

        Width = Math.Clamp(Width, 380, 520);
        Opacity = Math.Clamp(Opacity, 0.82, 1.0);
        RecentConversationMessageLimit = Math.Clamp(RecentConversationMessageLimit, 4, 30);

        if (string.IsNullOrWhiteSpace(AccentColor))
        {
            AccentColor = "#8B6CFF";
        }
    }
}
