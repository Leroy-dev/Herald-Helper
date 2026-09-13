using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace HeraldHelper.Desktop;

public partial class OcrReplayWindow : Window
{
    private static readonly string DefaultRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HeraldHelper",
        "ocr-replay");

    private readonly string _root;
    private readonly List<ReplayRecord> _records = [];

    public OcrReplayWindow(string? root = null)
    {
        InitializeComponent();
        _root = root ?? DefaultRoot;
        LoadRecords();
    }

    private void LoadRecords()
    {
        _records.Clear();
        if (Directory.Exists(_root))
        {
            foreach (var dir in Directory.EnumerateDirectories(_root)
                         .OrderByDescending(d => new DirectoryInfo(d).Name, StringComparer.OrdinalIgnoreCase)
                         .Take(150))
            {
                var recordPath = Path.Combine(dir, "record.json");
                if (File.Exists(recordPath))
                {
                    _records.Add(new ReplayRecord(dir, recordPath));
                }
            }
        }

        RecordList.ItemsSource = _records;
        NoCaptureText.Visibility = _records.Count == 0 ? Visibility.Visible : Visibility.Visible;
        if (_records.Count == 0)
        {
            NoCaptureText.Text = $"No replay records under {_root}.";
            RecordInfoText.Text = "nothing recorded";
        }
    }

    private void RecordList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordList.SelectedItem is not ReplayRecord record)
        {
            return;
        }

        try
        {
            var json = JsonDocument.Parse(File.ReadAllText(record.RecordPath)).RootElement;
            var captures = json.TryGetProperty("captures", out var caps) && caps.ValueKind == JsonValueKind.Array
                ? caps.EnumerateArray().Select(c => new ReplayCapture(
                    record.Directory,
                    c.TryGetProperty("image", out var img) ? img.GetString() ?? string.Empty : string.Empty,
                    c.TryGetProperty("Label", out var lbl) ? lbl.GetString() ?? string.Empty : string.Empty,
                    c.TryGetProperty("EngineName", out var eng) ? eng.GetString() ?? string.Empty : string.Empty,
                    c.TryGetProperty("OcrText", out var txt) ? txt.GetString() ?? string.Empty : string.Empty))
                  .ToList()
                : [];

            var shard = json.TryGetProperty("shard", out var s) ? s.GetString() : "?";
            var who = json.TryGetProperty("characterName", out var c) ? c.GetString() : "?";
            var when = json.TryGetProperty("capturedUtc", out var t) ? t.GetString() : "?";
            RecordInfoText.Text = $"{when} · {shard} · {who}";

            CaptureCombo.ItemsSource = captures;
            CaptureCombo.SelectedIndex = captures.Count > 0 ? 0 : -1;
            ParseResultBox.Text = json.TryGetProperty("parseResult", out var pr)
                ? JsonSerializer.Serialize(pr, new JsonSerializerOptions { WriteIndented = true })
                : "(no parse result)";
        }
        catch (Exception ex)
        {
            RecordInfoText.Text = $"could not load record: {ex.Message}";
        }
    }

    private void CaptureCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureCombo.SelectedItem is not ReplayCapture capture)
        {
            return;
        }

        OcrTextBox.Text = capture.OcrText;
        var imagePath = Path.Combine(capture.Directory, capture.ImageFile);
        if (File.Exists(imagePath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();
            CaptureImage.Source = bitmap;
            NoCaptureText.Visibility = Visibility.Collapsed;
        }
        else
        {
            CaptureImage.Source = null;
            NoCaptureText.Text = $"{capture.ImageFile} missing";
            NoCaptureText.Visibility = Visibility.Visible;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadRecords();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_root);
        Process.Start("explorer.exe", _root);
    }

    private sealed record ReplayRecord(string Directory, string RecordPath)
    {
        public string Title => System.IO.Path.GetFileName(Directory);
        public string Subtitle => RecordPath;
    }

    private sealed record ReplayCapture(string Directory, string ImageFile, string Label, string EngineName, string OcrText);
}
