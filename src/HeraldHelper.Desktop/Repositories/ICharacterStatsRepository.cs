using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Repositories;

public interface ICharacterStatsRepository
{
    CharacterStatsSnapshot? LoadCharacterStats(ShardType shard, string characterName);
    void SaveCharacterStats(CharacterStatsSnapshot stats);
}
