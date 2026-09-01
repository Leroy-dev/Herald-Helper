using System.ComponentModel;

namespace HeraldHelper.Desktop.Models;

public sealed class OverlaySettings : INotifyPropertyChanged
{
    private int _x = 1200;
    private int _y = 900;
    private int _timerX = 1580;
    private int _timerY = 900;
    private int _castX = 1200;
    private int _castY = 986;
    private int _fontSize = 20;
    private int _timerSize = 20;
    private string _targetColor = "#FFFFFF";
    private string _timerColor = "#FFFFFF";
    private string _outlineColor = "#000000";
    private bool _useRealmColors = true;
    private bool _showTarget = true;
    private bool _showTimers = true;
    private bool _showCastBar = true;
    private bool _dynamicCastSpeedEnabled;
    private bool _estimatedSpellDamageEnabled;
    private bool _ocrReplayEnabled;
    private string _targetFontFamily = "Segoe UI";
    private string _timerFontFamily = "Segoe UI";
    private string _castbarFontFamily = "Segoe UI";

    public int X { get => _x; set { _x = value; OnPropertyChanged(nameof(X)); } }
    public int Y { get => _y; set { _y = value; OnPropertyChanged(nameof(Y)); } }
    public int TimerX { get => _timerX; set { _timerX = value; OnPropertyChanged(nameof(TimerX)); } }
    public int TimerY { get => _timerY; set { _timerY = value; OnPropertyChanged(nameof(TimerY)); } }
    public int CastX { get => _castX; set { _castX = value; OnPropertyChanged(nameof(CastX)); } }
    public int CastY { get => _castY; set { _castY = value; OnPropertyChanged(nameof(CastY)); } }
    public int FontSize { get => _fontSize; set { _fontSize = value; OnPropertyChanged(nameof(FontSize)); } }
    public int TimerSize { get => _timerSize; set { _timerSize = value; OnPropertyChanged(nameof(TimerSize)); } }
    public string TargetColor { get => _targetColor; set { _targetColor = value; OnPropertyChanged(nameof(TargetColor)); } }
    public string TimerColor { get => _timerColor; set { _timerColor = value; OnPropertyChanged(nameof(TimerColor)); } }
    public string OutlineColor { get => _outlineColor; set { _outlineColor = value; OnPropertyChanged(nameof(OutlineColor)); } }
    public bool UseRealmColors { get => _useRealmColors; set { _useRealmColors = value; OnPropertyChanged(nameof(UseRealmColors)); } }
    public bool ShowTarget { get => _showTarget; set { _showTarget = value; OnPropertyChanged(nameof(ShowTarget)); } }
    public bool ShowTimers { get => _showTimers; set { _showTimers = value; OnPropertyChanged(nameof(ShowTimers)); } }
    public bool ShowCastBar { get => _showCastBar; set { _showCastBar = value; OnPropertyChanged(nameof(ShowCastBar)); } }
    public bool DynamicCastSpeedEnabled { get => _dynamicCastSpeedEnabled; set { _dynamicCastSpeedEnabled = value; OnPropertyChanged(nameof(DynamicCastSpeedEnabled)); } }
    public bool EstimatedSpellDamageEnabled { get => _estimatedSpellDamageEnabled; set { _estimatedSpellDamageEnabled = value; OnPropertyChanged(nameof(EstimatedSpellDamageEnabled)); } }
    public bool OcrReplayEnabled { get => _ocrReplayEnabled; set { _ocrReplayEnabled = value; OnPropertyChanged(nameof(OcrReplayEnabled)); } }
    public string TargetFontFamily { get => _targetFontFamily; set { _targetFontFamily = value; OnPropertyChanged(nameof(TargetFontFamily)); } }
    public string TimerFontFamily { get => _timerFontFamily; set { _timerFontFamily = value; OnPropertyChanged(nameof(TimerFontFamily)); } }
    public string CastbarFontFamily { get => _castbarFontFamily; set { _castbarFontFamily = value; OnPropertyChanged(nameof(CastbarFontFamily)); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
