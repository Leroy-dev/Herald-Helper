using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Casting;

namespace HeraldHelper.Tests;

public sealed class OverrideAwareCastSpellCatalogTests
{
    [Fact]
    public void FindBySpellName_AppliesLiveCastTimeNameAndIconOverride()
    {
        IReadOnlyCollection<CastSpellOverride> overrides = [];
        var baseIcon = new IconSpriteRef("base.png", 0, 0, 32, 32, 0, 0);
        var overrideIcon = new IconSpriteRef("custom.png", 32, 64, 32, 32, 2, 1);
        var catalog = new OverrideAwareCastSpellCatalog(
            new StubCatalog(new CastSpellInfo(
                "Fireball", 3, baseIcon, BaseDamage: 200, DamageType: "Heat",
                ClassName: "Wizard", Level: 50, IsFixedCastTime: true)),
            () => overrides);

        Assert.Equal(3, catalog.FindBySpellName("Fireball")!.CastTimeSeconds);

        overrides =
        [
            new CastSpellOverride("Fireball", "My Fireball", 1.5, overrideIcon)
        ];
        var result = catalog.FindBySpellName("Fireball");

        Assert.NotNull(result);
        Assert.Equal("My Fireball", result!.SpellName);
        Assert.Equal(1.5, result.CastTimeSeconds);
        Assert.Equal(overrideIcon, result.Icon);
        Assert.Equal(200, result.BaseDamage);
        Assert.Equal("Heat", result.DamageType);
        Assert.True(result.IsFixedCastTime);
        Assert.Equal(result, catalog.FindBySpellName("My Fireball"));
    }

    [Fact]
    public void FindBySpellName_NullOrInstantOverrideSuppressesCastBarMetadata()
    {
        var catalog = new OverrideAwareCastSpellCatalog(
            new StubCatalog(new CastSpellInfo("Fireball", 3, null)),
            () => [new CastSpellOverride("Fireball", "Fireball", null, null)]);

        Assert.Null(catalog.FindBySpellName("Fireball"));
    }

    private sealed class StubCatalog : ICastSpellCatalog
    {
        private readonly CastSpellInfo? _result;

        public StubCatalog(CastSpellInfo? result)
        {
            _result = result;
        }

        public CastSpellInfo? FindBySpellName(string spellName)
        {
            return _result;
        }
    }
}
