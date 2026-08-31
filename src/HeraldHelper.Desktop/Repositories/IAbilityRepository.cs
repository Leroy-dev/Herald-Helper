namespace HeraldHelper.Desktop.Repositories;

public interface IAbilityRepository
{
    List<AbilityEditorRow> LoadAbilities();
    void SaveAbilities(IEnumerable<AbilityEditorRow> rows);
}
