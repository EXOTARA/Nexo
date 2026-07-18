namespace Nexo.Core.Settings;

public enum SidebarPosition
{
    Left,
    Right
}

public sealed class ShellPreferences
{
    public SidebarPosition Position { get; set; } = SidebarPosition.Right;

    public double Width { get; set; } = 420;

    public double Opacity { get; set; } = 0.96;

    public string AccentColor { get; set; } = "#8B6CFF";

    public bool AnimationsEnabled { get; set; } = true;

    public bool ShowAudioModule { get; set; } = true;

    public bool ShowCaptureModule { get; set; } = true;

    public bool ShowSystemModule { get; set; } = true;

    public void Normalize()
    {
        Width = Math.Clamp(Width, 380, 520);
        Opacity = Math.Clamp(Opacity, 0.82, 1.0);

        if (string.IsNullOrWhiteSpace(AccentColor))
        {
            AccentColor = "#8B6CFF";
        }
    }
}
