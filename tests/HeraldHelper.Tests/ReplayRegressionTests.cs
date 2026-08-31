using System.Reflection;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class ReplayRegressionTests
{
    [Fact]
    public async Task ReplayRunner_RunsSampleFixtureWithoutDiffs()
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var fixturePath = Path.Combine(assemblyPath, "Fixtures", "sample.replay.json");
        var json = await File.ReadAllTextAsync(fixturePath);
        var fixture = ReplayRunner.LoadFixture(json);

        var castCatalog = new FakeCastCatalog();
        castCatalog.Add(new CastSpellInfo("Bolt", 2.5, null));

        var runner = new ReplayRunner(
            new FakeHeraldClientFactory(new FakeHeraldClient(null)),
            castCatalog,
            fixture);

        var success = await runner.RunAsync();

        Assert.True(success, string.Join(Environment.NewLine, runner.Diffs.SelectMany(d => d.Messages)));
    }

    private sealed class FakeCastCatalog : ICastSpellCatalog
    {
        private readonly Dictionary<string, CastSpellInfo> _entries = new(StringComparer.OrdinalIgnoreCase);

        public void Add(CastSpellInfo spell) => _entries[spell.SpellName] = spell;

        public CastSpellInfo? FindBySpellName(string spellName)
        {
            _entries.TryGetValue(spellName, out var info);
            return info;
        }
    }

    private sealed class FakeHeraldClient : IHeraldClient
    {
        private readonly TargetProfile? _profile;

        public FakeHeraldClient(TargetProfile? profile) => _profile = profile;

        public Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
        {
            return Task.FromResult(_profile);
        }
    }

    private sealed class FakeHeraldClientFactory : IHeraldClientFactory
    {
        private readonly IHeraldClient _client;

        public FakeHeraldClientFactory(IHeraldClient client) => _client = client;

        public IHeraldClient Resolve(ShardType shardType) => _client;
    }
}
