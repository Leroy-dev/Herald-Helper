using System.ComponentModel;

namespace HeraldHelper.Desktop.Models;

public sealed class HeraldHelperSettings : INotifyPropertyChanged
{
    private OverlaySettings _overlay = new();
    private AuthSettings _auth = new();
    private DiagnosticsSettings _diagnostics = new();
    private AppearanceSettings _appearance = new();

    public OverlaySettings Overlay
    {
        get => _overlay;
        set
        {
            _overlay = value;
            OnPropertyChanged(nameof(Overlay));
        }
    }

    public AuthSettings Auth
    {
        get => _auth;
        set
        {
            _auth = value;
            OnPropertyChanged(nameof(Auth));
        }
    }

    public DiagnosticsSettings Diagnostics
    {
        get => _diagnostics;
        set
        {
            _diagnostics = value;
            OnPropertyChanged(nameof(Diagnostics));
        }
    }

    public AppearanceSettings Appearance
    {
        get => _appearance;
        set
        {
            _appearance = value;
            OnPropertyChanged(nameof(Appearance));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class AuthSettings : INotifyPropertyChanged
{
    private int _autoRefreshMinutes = 25;
    private bool _onlineSyncEnabled;

    public int AutoRefreshMinutes { get => _autoRefreshMinutes; set { _autoRefreshMinutes = value; OnPropertyChanged(nameof(AutoRefreshMinutes)); } }

    public bool OnlineSyncEnabled { get => _onlineSyncEnabled; set { _onlineSyncEnabled = value; OnPropertyChanged(nameof(OnlineSyncEnabled)); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class DiagnosticsSettings : INotifyPropertyChanged
{
    private int _maxResponseLogLines = 500;
    private bool _sendDiagnosticLogs;

    public int MaxResponseLogLines { get => _maxResponseLogLines; set { _maxResponseLogLines = value; OnPropertyChanged(nameof(MaxResponseLogLines)); } }

    public bool SendDiagnosticLogs { get => _sendDiagnosticLogs; set { _sendDiagnosticLogs = value; OnPropertyChanged(nameof(SendDiagnosticLogs)); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class AppearanceSettings : INotifyPropertyChanged
{
    private string _theme = "Dark";
    private string _accentColor = "#007ACC";

    public string Theme { get => _theme; set { _theme = value; OnPropertyChanged(nameof(Theme)); } }
    public string AccentColor { get => _accentColor; set { _accentColor = value; OnPropertyChanged(nameof(AccentColor)); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
