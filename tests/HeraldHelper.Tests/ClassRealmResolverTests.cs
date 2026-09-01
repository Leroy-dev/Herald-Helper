using System.Windows.Media;
using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class ClassRealmResolverTests
{
    [Theory]
    [InlineData("Armsman", Realm.Albion)]
    [InlineData("Bard", Realm.Hibernia)]
    [InlineData("Berserker", Realm.Midgard)]
    [InlineData("Druid", Realm.Hibernia)]
    [InlineData("Healer", Realm.Midgard)]
    [InlineData("Paladin", Realm.Albion)]
    [InlineData("UnknownClass", Realm.Unknown)]
    [InlineData(null, Realm.Unknown)]
    public void Resolve_MapsClassToRealm(string? className, Realm expected)
    {
        Assert.Equal(expected, ClassRealmResolver.Resolve(className));
    }

    [Fact]
    public void ResolveColor_MapsRealmsToExpectedColors()
    {
        var albion = ClassRealmResolver.ResolveColor(Realm.Albion);
        var midgard = ClassRealmResolver.ResolveColor(Realm.Midgard);
        var hibernia = ClassRealmResolver.ResolveColor(Realm.Hibernia);
        var unknown = ClassRealmResolver.ResolveColor(Realm.Unknown);

        Assert.Equal(Colors.White, unknown);
        Assert.True(albion.R > 200 && albion.G < 100 && albion.B < 100);
        Assert.True(hibernia.R < 100 && hibernia.G > 150 && hibernia.B < 100);
        Assert.True(midgard.R < 100 && midgard.G < 150 && midgard.B > 150);
    }
}
