using Nexo.Core.Branding;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Nexo.App.WindowsIntegration;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Drawing.Icon _applicationIcon;
    private bool _disposed;

    public TrayIconController(
        Action openRequested,
        Action peekRequested,
        Action exitRequested)
    {
        ArgumentNullException.ThrowIfNull(openRequested);
        ArgumentNullException.ThrowIfNull(peekRequested);
        ArgumentNullException.ThrowIfNull(exitRequested);

        _menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem($"Abrir {ProductIdentity.ProductName}");
        var peekItem = new Forms.ToolStripMenuItem("Mostrar Peek");
        var exitItem = new Forms.ToolStripMenuItem("Salir completamente");

        openItem.Click += (_, _) => openRequested();
        peekItem.Click += (_, _) => peekRequested();
        exitItem.Click += (_, _) => exitRequested();

        _menu.Items.Add(openItem);
        _menu.Items.Add(peekItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon,
            Text = $"{ProductIdentity.ProductName} — {ProductIdentity.AssistantDescription}",
            ContextMenuStrip = _menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => openRequested();
        _notifyIcon.BalloonTipClicked += (_, _) => openRequested();
    }

    public bool IsVisible
    {
        get => !_disposed && _notifyIcon.Visible;
        set
        {
            if (!_disposed)
            {
                _notifyIcon.Visible = value;
            }
        }
    }

    public void Notify(
        string title,
        string detail,
        TrayNotificationKind kind,
        bool showBalloon,
        bool playSound)
    {
        if (_disposed)
        {
            return;
        }

        if (showBalloon)
        {
            _notifyIcon.ShowBalloonTip(
                5000,
                title,
                detail,
                kind switch
                {
                    TrayNotificationKind.Warning => Forms.ToolTipIcon.Warning,
                    TrayNotificationKind.Error => Forms.ToolTipIcon.Error,
                    _ => Forms.ToolTipIcon.Info
                });
        }

        if (playSound)
        {
            _ = Task.Run(() =>
            {
                switch (kind)
                {
                    case TrayNotificationKind.Warning:
                    case TrayNotificationKind.Error:
                        System.Media.SystemSounds.Exclamation.Play();
                        break;
                    case TrayNotificationKind.Success:
                        System.Media.SystemSounds.Asterisk.Play();
                        break;
                    default:
                        System.Media.SystemSounds.Beep.Play();
                        break;
                }
            });
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
        _menu.Dispose();
    }

    private static Drawing.Icon LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                using var extracted = Drawing.Icon.ExtractAssociatedIcon(executablePath);
                if (extracted is not null)
                {
                    return (Drawing.Icon)extracted.Clone();
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}

public enum TrayNotificationKind
{
    Information,
    Success,
    Warning,
    Error
}
