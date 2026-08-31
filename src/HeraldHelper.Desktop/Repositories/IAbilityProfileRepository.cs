using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Desktop.Repositories;

public interface IAbilityProfileRepository
{
    List<AbilityEditorRow> LoadAbilityProfile(ShardType shard, string characterName, string className);
    void SaveAbilityProfile(ShardType shard, string characterName, string className, IEnumerable<AbilityEditorRow> rows);
    List<AbilityProfileOverride> LoadAbilityProfileOverrides();
    void DeleteAllAbilityProfiles();
    void RestoreAbilityProfileOverrides(IEnumerable<AbilityProfileOverride> overrides);
}
