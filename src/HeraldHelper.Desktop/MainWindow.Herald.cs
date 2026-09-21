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
            HeraldView.HeraldResultsGrid.ItemsSource = rows.Select(r => new HeraldRow(r)).ToList();
            HeraldView.HeraldResultText.Text = rows.Count == 0
                ? "no matches"
                : $"{rows.Count} player(s)";
        }
        catch (Exception ex)
        {
            HeraldView.HeraldResultText.Text = $"search failed: {ex.Message}";
        }
    }

    internal void HeraldRemove_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (HeraldView?.HeraldResultsGrid?.SelectedItem is not HeraldRow row)
        {
            return;
        }

        if (System.Windows.MessageBox.Show(
                this,
                $"Forget the cached {row.Server} profile for {row.Name}?",
                "Remove Cached Player",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        if (Enum.TryParse<Domain.Enums.ShardType>(row.Server, true, out var shard))
        {
            _store.Delete(shard, row.Name);
        }
        RefreshHeraldResults();
    }

    /// <summary>Grid projection — adds the realm derived from the class.</summary>
    internal sealed class HeraldRow(TargetProfileRow row)
    {
        public string Server => row.Server;
        public string Name => row.Name;
        public string? Guild => row.Guild;
        public string? Class => row.Class;
        public string Realm => ClassRealmResolver.Resolve(row.Class) is { } r && r != HeraldHelper.Desktop.Realm.Unknown ? r.ToString() : string.Empty;
        public int? Level => row.Level;
        public string? RealmRank => row.RealmRank;
        public int? SoloKills => row.SoloKills;
        public string UpdatedUtc => row.UpdatedUtc;
    }
}
