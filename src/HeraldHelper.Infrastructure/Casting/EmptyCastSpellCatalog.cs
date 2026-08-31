using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Casting;

internal sealed class EmptyCastSpellCatalog : ICastSpellCatalog
{
    public CastSpellInfo? FindBySpellName(string spellName) => null;
}
