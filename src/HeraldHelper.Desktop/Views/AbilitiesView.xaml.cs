using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HeraldHelper.Desktop.Views;

public partial class AbilitiesView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public AbilitiesView()
    {
        InitializeComponent();
        // Free-text effect codes silently fall back to Stun — a dropdown
        // can't produce an invalid one.
        EffectTypeColumn.ItemsSource = new[]
        {
            new { Code = "s", Label = "Stun (s)" },
            new { Code = "m", Label = "Mezz (m)" },
            new { Code = "r", Label = "Root (r)" }
        };
    }

    private void AbilityFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        Main?.AbilityFilter_TextChanged(sender, e);
    }
    private void AbilityProfileClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Main?.AbilityProfileClassCombo_SelectionChanged(sender, e);
    }
    private void AbilityProfileServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Main?.AbilityProfileServerCombo_SelectionChanged(sender, e);
    }
    private void AddAbility_Click(object sender, RoutedEventArgs e)
    {
        Main?.AddAbility_Click(sender, e);
    }
    private void AddAbilityFromCatalog_Click(object sender, RoutedEventArgs e)
    {
        Main?.AddAbilityFromCatalog_Click(sender, e);
    }
    private void AbilitiesGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        Main?.MarkAbilitiesDirty();
    }
    private void ReloadAbilities_Click(object sender, RoutedEventArgs e)
    {
        Main?.ReloadAbilities_Click(sender, e);
    }
    private void ReloadAbilityCatalogs_Click(object sender, RoutedEventArgs e)
    {
        Main?.ReloadAbilityCatalogs_Click(sender, e);
    }
    private void RemoveAbility_Click(object sender, RoutedEventArgs e)
    {
        Main?.RemoveAbility_Click(sender, e);
    }
    private void SaveAbilities_Click(object sender, RoutedEventArgs e)
    {
        Main?.SaveAbilities_Click(sender, e);
    }
    private void UpdateCatalogsOnline_Click(object sender, RoutedEventArgs e)
    {
        Main?.UpdateCatalogsOnline_Click(sender, e);
    }
    private void AbilityTestLine_Click(object sender, RoutedEventArgs e)
    {
        Main?.AbilityTestLine_Click(sender, e);
    }
    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        Main?.ExportProfile_Click(sender, e);
    }
    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        Main?.ImportProfile_Click(sender, e);
    }
}
