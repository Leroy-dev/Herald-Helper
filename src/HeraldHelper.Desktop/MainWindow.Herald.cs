using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

public partial class MainWindow
{
    internal void RefreshHeraldResults()
    {
        if (HeraldView is null)
        {
            return;
        }

        var fragment = HeraldView.HeraldSearchText?.Text;
        var server = (HeraldView.HeraldShardCombo?.SelectedItem as System.Windows.Controls.ComboBoxItem)
            ?.Content?.ToString() switch
        {
            "Eden" => "eden",
            "Blackthorn" => "blackthorn",
            _ => null
        };

        try
        {
            var rows = _store.SearchTargetProfiles(server, fragment);
            HeraldView.HeraldResultsGrid.ItemsSource = rows;
            HeraldView.HeraldResultText.Text = rows.Count == 0
                ? "no matches"
                : $"{rows.Count} player(s)";
        }
        catch (Exception ex)
        {
            HeraldView.HeraldResultText.Text = $"search failed: {ex.Message}";
        }
    }
}
