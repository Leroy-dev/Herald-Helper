using HeraldHelper.Domain.Models;

namespace HeraldHelper.Application.Contracts;

public interface ICastSpellCatalog
{
    CastSpellInfo? FindBySpellName(string spellName);

    CastSpellInfo? FindBySpellName(string spellName, string? className, int? level)
    {
        return FindBySpellName(spellName);
    }
}
