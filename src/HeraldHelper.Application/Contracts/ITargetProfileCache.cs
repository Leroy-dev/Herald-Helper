using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface ITargetProfileCache
{
    TargetProfile? Load(ShardType shardType, string targetName);

    void Save(ShardType shardType, TargetProfile profile);
}
