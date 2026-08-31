using HeraldHelper.Infrastructure.Ocr;

namespace HeraldHelper.Tests;

public sealed class AdaptiveOcrEngineTests
{
    [Fact]
    public async Task ReadTextAsync_SelectsBestInitialOutputWithoutReadingWinnerTwice()
    {
        var shortEngine = new ScriptedOcrEngine("Short", "text");
        var chatEngine = new ScriptedOcrEngine("Chat", "You target [Alice]. You examine Alice.");
        var adaptive = new AdaptiveOcrEngine([shortEngine, chatEngine], reevaluateEveryReads: 30);

        var result = await adaptive.ReadTextAsync("frame.png", CancellationToken.None);

        Assert.Equal("You target [Alice]. You examine Alice.", result);
        Assert.Equal("Chat", adaptive.SelectedEngineName);
        Assert.Equal(1, shortEngine.CallCount);
        Assert.Equal(1, chatEngine.CallCount);
    }

    [Fact]
    public async Task ReadTextAsync_ReevaluatesAndSwitchesWhenAnotherEngineBecomesBetter()
    {
        var recoveringEngine = new ScriptedOcrEngine(
            "Recovering",
            "abc",
            "You target [Alice]. You examine Alice. She is a member of your realm.");
        var degradingEngine = new ScriptedOcrEngine(
            "Degrading",
            "You target [Bob]. You examine Bob.",
            "x");
        var adaptive = new AdaptiveOcrEngine([recoveringEngine, degradingEngine], reevaluateEveryReads: 1);

        var first = await adaptive.ReadTextAsync("frame-1.png", CancellationToken.None);
        var second = await adaptive.ReadTextAsync("frame-2.png", CancellationToken.None);

        Assert.Contains("Bob", first);
        Assert.Contains("Alice", second);
        Assert.Equal("Recovering", adaptive.SelectedEngineName);
        Assert.Equal(2, recoveringEngine.CallCount);
        Assert.Equal(2, degradingEngine.CallCount);
    }

    private sealed class ScriptedOcrEngine : IOcrEngine
    {
        private readonly Queue<string> _outputs;
        private string _lastOutput;

        public ScriptedOcrEngine(string name, params string[] outputs)
        {
            Name = name;
            _outputs = new Queue<string>(outputs);
            _lastOutput = outputs.LastOrDefault() ?? string.Empty;
        }

        public string Name { get; }
        public int CallCount { get; private set; }

        public Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_outputs.Count > 0)
            {
                _lastOutput = _outputs.Dequeue();
            }

            return Task.FromResult(_lastOutput);
        }
    }
}
