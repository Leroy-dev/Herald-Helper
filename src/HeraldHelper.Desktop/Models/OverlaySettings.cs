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
    private int _resistsX = 1200;
    private int _resistsY = 830;
    private int _groupX = 40;
    private int _groupY = 300;
    private int _groupSize = 14;
    private int _selfCcX = 1200;
    private int _selfCcY = 740;
    private int _selfCcSize = 32;
    private int _peelX = 1580;
    private int _peelY = 700;
    private int _peelSize = 16;
    private int _buffX = 40;
    private int _buffY = 700;
    private int _buffSize = 14;
    private int _fontSize = 20;
    private int _timerSize = 20;
    private int _resistsSize = 20;
    private string _targetColor = "#FFFFFF";
    private string _timerColor = "#FFFFFF";
    private string _outlineColor = "#000000";
    private bool _useRealmColors = true;
    private bool _showTarget = true;
    private bool _showTimers = true;
    private bool _showCastBar = true;
    private bool _showResists;
    private bool _showGroup;
    private bool _showSelfCc = true;
    private bool _showPeel = true;
    private bool _showBuffs = true;
    private bool _soundsEnabled;
    private bool _soundSelfCc = true;
    private bool _soundPeel = true;
    private bool _soundInterrupt = true;
    private double _overlayOpacity = 1.0;
    private bool _showGuild = true;
    private bool _showClass = true;
    private bool _showLevel = true;
    private bool _showRealmRank = true;
    private bool _showSoloKills = true;
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
    public int ResistsX { get => _resistsX; set { _resistsX = value; OnPropertyChanged(nameof(ResistsX)); } }
    public int ResistsY { get => _resistsY; set { _resistsY = value; OnPropertyChanged(nameof(ResistsY)); } }
    public int GroupX { get => _groupX; set { _groupX = value; OnPropertyChanged(nameof(GroupX)); } }
    public int GroupY { get => _groupY; set { _groupY = value; OnPropertyChanged(nameof(GroupY)); } }
    public int GroupSize { get => _groupSize; set { _groupSize = value; OnPropertyChanged(nameof(GroupSize)); } }
    public int SelfCcX { get => _selfCcX; set { _selfCcX = value; OnPropertyChanged(nameof(SelfCcX)); } }
    public int SelfCcY { get => _selfCcY; set { _selfCcY = value; OnPropertyChanged(nameof(SelfCcY)); } }
    public int SelfCcSize { get => _selfCcSize; set { _selfCcSize = value; OnPropertyChanged(nameof(SelfCcSize)); } }
    public int PeelX { get => _peelX; set { _peelX = value; OnPropertyChanged(nameof(PeelX)); } }
    public int PeelY { get => _peelY; set { _peelY = value; OnPropertyChanged(nameof(PeelY)); } }
    public int PeelSize { get => _peelSize; set { _peelSize = value; OnPropertyChanged(nameof(PeelSize)); } }
    public int BuffX { get => _buffX; set { _buffX = value; OnPropertyChanged(nameof(BuffX)); } }
    public int BuffY { get => _buffY; set { _buffY = value; OnPropertyChanged(nameof(BuffY)); } }
    public int BuffSize { get => _buffSize; set { _buffSize = value; OnPropertyChanged(nameof(BuffSize)); } }
    public int FontSize { get => _fontSize; set { _fontSize = value; OnPropertyChanged(nameof(FontSize)); } }
    public int TimerSize { get => _timerSize; set { _timerSize = value; OnPropertyChanged(nameof(TimerSize)); } }
    public int ResistsSize { get => _resistsSize; set { _resistsSize = value; OnPropertyChanged(nameof(ResistsSize)); } }
    public string TargetColor { get => _targetColor; set { _targetColor = value; OnPropertyChanged(nameof(TargetColor)); } }
    public string TimerColor { get => _timerColor; set { _timerColor = value; OnPropertyChanged(nameof(TimerColor)); } }
    public string OutlineColor { get => _outlineColor; set { _outlineColor = value; OnPropertyChanged(nameof(OutlineColor)); } }
    public bool UseRealmColors { get => _useRealmColors; set { _useRealmColors = value; OnPropertyChanged(nameof(UseRealmColors)); } }
    public bool ShowTarget { get => _showTarget; set { _showTarget = value; OnPropertyChanged(nameof(ShowTarget)); } }
    public bool ShowTimers { get => _showTimers; set { _showTimers = value; OnPropertyChanged(nameof(ShowTimers)); } }
    public bool ShowCastBar { get => _showCastBar; set { _showCastBar = value; OnPropertyChanged(nameof(ShowCastBar)); } }
    public bool ShowResists { get => _showResists; set { _showResists = value; OnPropertyChanged(nameof(ShowResists)); } }
    public bool ShowGroup { get => _showGroup; set { _showGroup = value; OnPropertyChanged(nameof(ShowGroup)); } }
    public bool ShowSelfCc { get => _showSelfCc; set { _showSelfCc = value; OnPropertyChanged(nameof(ShowSelfCc)); } }
    public bool ShowPeel { get => _showPeel; set { _showPeel = value; OnPropertyChanged(nameof(ShowPeel)); } }
    public bool ShowBuffs { get => _showBuffs; set { _showBuffs = value; OnPropertyChanged(nameof(ShowBuffs)); } }
    public bool SoundsEnabled { get => _soundsEnabled; set { _soundsEnabled = value; OnPropertyChanged(nameof(SoundsEnabled)); } }
    public bool SoundSelfCc { get => _soundSelfCc; set { _soundSelfCc = value; OnPropertyChanged(nameof(SoundSelfCc)); } }
    public bool SoundPeel { get => _soundPeel; set { _soundPeel = value; OnPropertyChanged(nameof(SoundPeel)); } }
    public bool SoundInterrupt { get => _soundInterrupt; set { _soundInterrupt = value; OnPropertyChanged(nameof(SoundInterrupt)); } }
    public double OverlayOpacity { get => _overlayOpacity; set { _overlayOpacity = Math.Clamp(value, 0.3, 1.0); OnPropertyChanged(nameof(OverlayOpacity)); } }
    public bool ShowGuild { get => _showGuild; set { _showGuild = value; OnPropertyChanged(nameof(ShowGuild)); } }
    public bool ShowClass { get => _showClass; set { _showClass = value; OnPropertyChanged(nameof(ShowClass)); } }
    public bool ShowLevel { get => _showLevel; set { _showLevel = value; OnPropertyChanged(nameof(ShowLevel)); } }
    public bool ShowRealmRank { get => _showRealmRank; set { _showRealmRank = value; OnPropertyChanged(nameof(ShowRealmRank)); } }
    public bool ShowSoloKills { get => _showSoloKills; set { _showSoloKills = value; OnPropertyChanged(nameof(ShowSoloKills)); } }
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
