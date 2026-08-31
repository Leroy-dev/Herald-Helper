using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Application.Contracts;

public interface IHeraldClientFactory
{
    IHeraldClient Resolve(ShardType shardType);
}
