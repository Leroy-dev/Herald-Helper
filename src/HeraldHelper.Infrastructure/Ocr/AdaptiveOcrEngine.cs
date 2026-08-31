using System.Diagnostics;

namespace HeraldHelper.Infrastructure.Ocr;

public sealed class AdaptiveOcrEngine : IOcrEngine
{
    private const int DefaultReevaluateEveryReads = 30;
    private const int DefaultWeakReadsBeforeReevaluation = 3;
    private const int DefaultMinimumReadsBeforeWeakReevaluation = 5;
    private static readonly string[] ChatMarkers =
    [
        "you", "target", "examine", "member", "realm", "cast", "spell", "resist", "damage", "heal"
    ];

    private readonly IReadOnlyList<IOcrEngine> _engines;
    private readonly int _reevaluateEveryReads;
    private readonly int _weakReadsBeforeReevaluation;
    private readonly int _minimumReadsBeforeWeakReevaluation;
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private IOcrEngine? _selected;
    private int _readsSinceEvaluation;
    private int _consecutiveWeakReads;

    public string Name => _selected?.Name ?? "Adaptive";
    public string? SelectedEngineName => _selected?.Name;

    public AdaptiveOcrEngine(params IOcrEngine[] engines)
        : this(
            engines,
            DefaultReevaluateEveryReads,
            DefaultWeakReadsBeforeReevaluation,
            DefaultMinimumReadsBeforeWeakReevaluation)
    {
    }

    public AdaptiveOcrEngine(
        IEnumerable<IOcrEngine> engines,
        int reevaluateEveryReads,
        int weakReadsBeforeReevaluation = DefaultWeakReadsBeforeReevaluation,
        int minimumReadsBeforeWeakReevaluation = DefaultMinimumReadsBeforeWeakReevaluation)
    {
        _engines = engines.ToList();
        if (_engines.Count == 0)
        {
            throw new ArgumentException("At least one OCR engine is required.", nameof(engines));
        }

        _reevaluateEveryReads = Math.Max(1, reevaluateEveryReads);
        _weakReadsBeforeReevaluation = Math.Max(1, weakReadsBeforeReevaluation);
        _minimumReadsBeforeWeakReevaluation = Math.Max(1, minimumReadsBeforeWeakReevaluation);
    }

    public async Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_selected is null)
            {
                return await EvaluateEnginesAsync(imagePath, null, cancellationToken).ConfigureAwait(false);
            }

            string selectedText;
            try
            {
                selectedText = await _selected.ReadTextAsync(imagePath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return await EvaluateEnginesAsync(imagePath, null, cancellationToken).ConfigureAwait(false);
            }

            _readsSinceEvaluation++;
            _consecutiveWeakReads = IsWeak(selectedText) ? _consecutiveWeakReads + 1 : 0;
            var periodicEvaluationDue = _readsSinceEvaluation >= _reevaluateEveryReads;
            var weakEvaluationDue = _consecutiveWeakReads >= _weakReadsBeforeReevaluation
                && _readsSinceEvaluation >= _minimumReadsBeforeWeakReevaluation;
            if (!periodicEvaluationDue && !weakEvaluationDue)
            {
                return selectedText;
            }

            return await EvaluateEnginesAsync(
                    imagePath,
                    new EngineRead(_selected, selectedText, 0),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _readLock.Release();
        }
    }

    private async Task<string> EvaluateEnginesAsync(
        string imagePath,
        EngineRead? completedRead,
        CancellationToken cancellationToken)
    {
        EngineRead? best = null;
        Exception? lastError = null;

        foreach (var engine in _engines)
        {
            try
            {
                EngineRead candidate;
                if (completedRead is not null && ReferenceEquals(completedRead.Engine, engine))
                {
                    candidate = completedRead;
                }
                else
                {
                    var sw = Stopwatch.StartNew();
                    var text = await engine.ReadTextAsync(imagePath, cancellationToken).ConfigureAwait(false);
                    sw.Stop();
                    candidate = new EngineRead(engine, text, sw.ElapsedMilliseconds);
                }

                if (best is null || IsBetter(candidate, best))
                {
                    best = candidate;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("No OCR engine succeeded.", lastError);
        }

        _selected = best.Engine;
        _readsSinceEvaluation = 0;
        _consecutiveWeakReads = IsWeak(best.Text) ? 1 : 0;
        return best.Text;
    }

    private static bool IsBetter(EngineRead candidate, EngineRead currentBest)
    {
        var candidateScore = CalculateQualityScore(candidate.Text);
        var bestScore = CalculateQualityScore(currentBest.Text);
        return candidateScore > bestScore
            || (candidateScore == bestScore && candidate.ElapsedMilliseconds < currentBest.ElapsedMilliseconds);
    }

    private static int CalculateQualityScore(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var meaningful = text.Count(static c => !char.IsWhiteSpace(c));
        var lettersAndDigits = text.Count(static c => char.IsLetterOrDigit(c));
        var suspicious = text.Count(static c => c is '\uFFFD' or '\u25A1');
        var markerScore = ChatMarkers.Count(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)) * 20;
        return meaningful + lettersAndDigits + markerScore - (suspicious * 10);
    }

    private static bool IsWeak(string text)
    {
        return text.Count(static c => char.IsLetterOrDigit(c)) < 8;
    }

    private sealed record EngineRead(IOcrEngine Engine, string Text, long ElapsedMilliseconds);
}
