using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Application.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Parsing;

public sealed class AbilitiesChatEventParser : IChatEventParser
{
    private const int MaxAbilityTargetDistanceChars = 180;
    private const string NextMessageBoundary = @"(?=(?:\s+you\s+(?:exam\w*|begin|cast|attempt|move|prepare|target|resist|hit)\b)|(?:\s+[A-Z][A-Za-z'\-]+\s+casts?\s+a\s+spell\b)|[\r\n\.\!\?\:\;\]\[]|$)";
    private static readonly Regex TargetBracketRegex = new(@"you\s+target\s*\[(?<name>[^\]\r\n]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TargetPlainRegex = new(
        @"you\s+target\s+(?<name>(?!\[).*?)" + NextMessageBoundary,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BeginCastingRegex = new(
        @"you\s+begin\s+casting\s+(?<name>(?:a|an|the)\s+.+?|.+?)\s+spell" + NextMessageBoundary,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BeginPlayingRegex = new(
        @"you\s+begin\s+playing\s+(?<name>.+?)" + NextMessageBoundary,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CastCompletedRegex = new(
        @"you\s+cast\s+(?<name>(?:a|an|the)\s+.+?|.+?)\s+spell" + NextMessageBoundary,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MemberExamineRegex = new(
        @"you\s+exam\w*.*?\bis\s+a\s+member\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex NonMemberExamineRegex = new(
        @"you\s+exam\w*.*?(?:\bis\s+not\s+a\s+member\b|\b(?:aggressive|friendly|neutral)\s+towards\s+you\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly string[] CastInterruptedMarkers =
    [
        "you move and interrupt your spellcast",
        "your spell is interrupted",
        "casting has been interrupted",
        "you are interrupted",
        "you move and interrupt your spell",
        "you fumble the spell",
        "you lose your concentration",
        "cannot concentrate enough to cast"
    ];
    private readonly IReadOnlyDictionary<string, AbilityDefinition> _abilityByToken;
    private readonly IReadOnlyList<(string Token, AbilityDefinition Ability, int WordCount)> _fuzzyTokens;
    private readonly Regex? _abilityMatcher;

    public AbilitiesChatEventParser(IEnumerable<AbilityDefinition> abilities)
    {
        var byToken = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            AddAbilityToken(byToken, ability.Name, ability);
            foreach (var alias in ability.Aliases ?? [])
            {
                AddAbilityToken(byToken, alias, ability);
            }
        }

        _abilityByToken = byToken;
        _fuzzyTokens = byToken
            .Where(x => x.Key.Length >= 6)
            .Select(x => (x.Key, x.Value, x.Key.Count(c => c == ' ') + 1))
            .OrderByDescending(x => x.Key.Length)
            .ToList();
        if (byToken.Count > 0)
        {
            var alternatives = string.Join("|", byToken.Keys
                .OrderByDescending(x => x.Length)
                .Select(Regex.Escape));
            _abilityMatcher = new Regex(
                $@"(?<![\p{{L}}\p{{N}}])(?<ability>{alternatives})(?![\p{{L}}\p{{N}}])",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }

    public ChatParseResult Parse(string ocrText, string? fallbackTargetName = null)
    {
        var normalizedOcrText = NormalizeOcrText(ocrText);
        var targetMentions = ParseTargetMentions(normalizedOcrText);
        var visibleTargetEvents = targetMentions
            .Select((mention, index) => new TargetEvent(
                mention.Name,
                ResolveTargetMembership(normalizedOcrText, targetMentions, index),
                mention.OccurrenceOrdinal))
            .ToList();
        var targetEvent = visibleTargetEvents.LastOrDefault();
        var hits = new List<AbilityHit>();
        var hitMentions = ParseAbilityMentions(normalizedOcrText);
        var visibleCastEvents = ParseCastEvents(normalizedOcrText);
        var castEvent = visibleCastEvents.LastOrDefault();
        var hitOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var mention in hitMentions)
        {
            var targetName = ResolveTargetForIndex(targetMentions, mention.Index);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = fallbackTargetName?.Trim() ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                continue;
            }

            // Failure text lives in the mention's own sentence or the ones
            // after it ("Foo resists your Slam!") — a "must wait … again"
            // line from an earlier attempt must not suppress a landed hit.
            var landed = !ContainsFailureKeyword(WindowFromLineStart(normalizedOcrText, mention.Index, mention.Ability.Name.Length));
            var hitKey = $"{targetName}|{mention.Ability.Name}|{mention.Ability.SkillCode}|{mention.Ability.EffectType}|{landed}";
            hitOrdinals.TryGetValue(hitKey, out var previousOrdinal);
            var occurrenceOrdinal = previousOrdinal + 1;
            hitOrdinals[hitKey] = occurrenceOrdinal;
            hits.Add(new AbilityHit(
                targetName,
                mention.Ability.Name,
                mention.Ability.SkillCode,
                mention.Ability.EffectType,
                mention.Ability.DurationSeconds,
                landed,
                occurrenceOrdinal));
        }

        return new ChatParseResult(targetEvent, hits, castEvent, visibleTargetEvents, visibleCastEvents);
    }

    private static IReadOnlyList<CastEvent> ParseCastEvents(string ocrText)
    {
        var events = new List<(int Index, CastEvent Event)>();

        foreach (Match match in BeginCastingRegex.Matches(ocrText))
        {
            var spellName = CleanupCastSpellName(match.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(spellName))
            {
                events.Add((match.Index, new CastEvent(CastEventType.Started, spellName)));
            }
        }

        foreach (Match match in BeginPlayingRegex.Matches(ocrText))
        {
            var spellName = CleanupCastSpellName(match.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(spellName))
            {
                events.Add((match.Index, new CastEvent(CastEventType.Started, spellName)));
            }
        }

        foreach (Match match in CastCompletedRegex.Matches(ocrText))
        {
            var spellName = CleanupCastSpellName(match.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(spellName))
            {
                events.Add((match.Index, new CastEvent(CastEventType.Completed, spellName)));
            }
        }

        var lowered = ocrText.ToLowerInvariant();
        foreach (var marker in CastInterruptedMarkers)
        {
            var startIndex = 0;
            while (startIndex < lowered.Length)
            {
                var index = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                events.Add((index, new CastEvent(CastEventType.Interrupted, null)));
                startIndex = index + marker.Length;
            }
        }

        if (events.Count == 0)
        {
            return [];
        }

        var result = new List<CastEvent>();
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in events.OrderBy(x => x.Index))
        {
            var key = $"{item.Event.EventType}|{item.Event.SpellName ?? string.Empty}";
            ordinals.TryGetValue(key, out var previousOrdinal);
            var occurrenceOrdinal = previousOrdinal + 1;
            ordinals[key] = occurrenceOrdinal;
            result.Add(item.Event with { OccurrenceOrdinal = occurrenceOrdinal });
        }

        return result;
    }

    private static List<TargetMention> ParseTargetMentions(string ocrText)
    {
        var rawMentions = new List<(string Name, int Index)>();
        foreach (Match m in TargetBracketRegex.Matches(ocrText))
        {
            var name = CleanupName(m.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                rawMentions.Add((name, m.Index));
            }
        }

        foreach (Match m in TargetPlainRegex.Matches(ocrText))
        {
            var name = CleanupName(m.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                rawMentions.Add((name, m.Index));
            }
        }

        rawMentions.Sort((a, b) => a.Index.CompareTo(b.Index));
        var mentions = new List<TargetMention>(rawMentions.Count);
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var mention in rawMentions)
        {
            ordinals.TryGetValue(mention.Name, out var previousOrdinal);
            var occurrenceOrdinal = previousOrdinal + 1;
            ordinals[mention.Name] = occurrenceOrdinal;
            mentions.Add(new TargetMention(mention.Name, mention.Index, occurrenceOrdinal));
        }

        return mentions;
    }

    private List<AbilityMention> ParseAbilityMentions(string ocrText)
    {
        var mentions = new List<AbilityMention>();
        if (_abilityMatcher is null)
        {
            return mentions;
        }

        foreach (Match match in _abilityMatcher.Matches(ocrText))
        {
            var token = NormalizeAbilityToken(match.Groups["ability"].Value);
            if (_abilityByToken.TryGetValue(token, out var ability))
            {
                mentions.Add(new AbilityMention(ability, match.Index));
            }
        }

        AddFuzzyAbilityMentions(ocrText, mentions);

        return mentions.OrderBy(x => x.Index).ToList();
    }

    private void AddFuzzyAbilityMentions(string ocrText, List<AbilityMention> mentions)
    {
        if (_fuzzyTokens.Count == 0)
        {
            return;
        }
        var words = Regex.Matches(ocrText, @"[\p{L}\p{N}'-]+").Cast<Match>().ToList();
        foreach (var (token, ability, wordCount) in _fuzzyTokens)
        {
            var maxDistance = token.Length >= 14 ? 2 : 1;
            for (var index = 0; index + wordCount <= words.Count; index++)
            {
                var first = words[index];
                var last = words[index + wordCount - 1];
                var candidate = ocrText[first.Index..(last.Index + last.Length)];
                if (Math.Abs(candidate.Length - token.Length) > maxDistance ||
                    mentions.Any(x => ReferenceEquals(x.Ability, ability) && Math.Abs(x.Index - first.Index) <= 2))
                {
                    continue;
                }
                if (BoundedEditDistance(candidate, token, maxDistance) <= maxDistance)
                {
                    mentions.Add(new AbilityMention(ability, first.Index));
                }
            }
        }
    }

    private static int BoundedEditDistance(string left, string right, int maximum)
    {
        if (Math.Abs(left.Length - right.Length) > maximum)
        {
            return maximum + 1;
        }
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }
            if (rowMinimum > maximum)
            {
                return maximum + 1;
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static void AddAbilityToken(
        Dictionary<string, AbilityDefinition> byToken,
        string rawToken,
        AbilityDefinition ability)
    {
        var token = NormalizeAbilityToken(rawToken);
        if (token.Length >= 3)
        {
            byToken.TryAdd(token, ability);
        }
    }

    private static string NormalizeAbilityToken(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string ResolveTargetForIndex(IReadOnlyList<TargetMention> mentions, int index)
    {
        for (var i = mentions.Count - 1; i >= 0; i--)
        {
            if (mentions[i].Index <= index)
            {
                if (index - mentions[i].Index > MaxAbilityTargetDistanceChars)
                {
                    return string.Empty;
                }

                return mentions[i].Name;
            }
        }

        return string.Empty;
    }

    private static TargetMembership ResolveTargetMembership(
        string ocrText,
        IReadOnlyList<TargetMention> targetMentions,
        int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= targetMentions.Count)
        {
            return TargetMembership.Unknown;
        }

        var segmentStart = targetMentions[targetIndex].Index;
        var segmentEnd = targetIndex + 1 < targetMentions.Count
            ? targetMentions[targetIndex + 1].Index
            : ocrText.Length;
        var segment = ocrText[segmentStart..segmentEnd];

        if (NonMemberExamineRegex.IsMatch(segment))
        {
            return TargetMembership.NonMember;
        }

        return MemberExamineRegex.IsMatch(segment)
            ? TargetMembership.Member
            : TargetMembership.Unknown;
    }

    private static string CleanupName(string raw)
    {
        return Regex.Replace(raw.Trim().Trim('.', ',', ';', ':', '!', '?', '"', '[', ']'), @"\s+", " ");
    }

    private static string CleanupCastSpellName(string raw)
    {
        var cleaned = CleanupName(raw);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        cleaned = Regex.Replace(cleaned, @"^(?:a|an|the)\s+", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+spell$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+as\s+a\s+follow\s+up$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+(?:you|target|examine|ignorea|ignored)\b.*$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    private static string NormalizeOcrText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('|', 'I');

        normalized = Regex.Replace(normalized, @"\[\d{2}:\d{2}:\d{2}(?:\.\d{3})?\]", " ");
        normalized = Regex.Replace(normalized, @"(?i)(?<!\s)(you\s+(?:target|exam\w*|begin|cast|attempt|move|prepare|resist|hit)\b)", " $1");
        normalized = Regex.Replace(normalized, @"(?i)(spellcast)(?!\s)", "$1 ");
        normalized = Regex.Replace(normalized, @"(?i)(?<!\s)([A-Z][A-Za-z'\-]+\s+casts?\s+a\s+spell\b)", " $1");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }

    private static bool ContainsFailureKeyword(string line)
    {
        return line.Contains("resists", StringComparison.OrdinalIgnoreCase)
            || line.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
            || line.Contains("again", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>From the start of the mention's sentence (bounded 64 chars
    /// back for the merge case) to 96 chars past the ability token.</summary>
    private static string WindowFromLineStart(string text, int index, int tokenLength)
    {
        var start = index;
        for (var i = index - 1; i >= Math.Max(0, index - 64); i--)
        {
            if (text[i] is '.' or '!' or '?')
            {
                start = i + 1;
                break;
            }
        }

        var end = Math.Min(text.Length, index + tokenLength + 96);
        return text[start..end];
    }

    private sealed record TargetMention(string Name, int Index, int OccurrenceOrdinal);
    private sealed record AbilityMention(AbilityDefinition Ability, int Index);

    private static IReadOnlyList<AbilityDefinition> LoadAbilities(string abilitiesFilePath)
    {
        var list = new List<AbilityDefinition>();
        foreach (var raw in File.ReadLines(abilitiesFilePath))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var parts = raw.Split('#');
            if (parts.Length != 4)
            {
                continue;
            }

            if (!int.TryParse(parts[2].Trim(), out var seconds))
            {
                continue;
            }

            var effectType = ParseEffectTypeCode(parts[3]);
            list.Add(new AbilityDefinition(parts[0].Trim(), parts[1].Trim(), seconds, effectType));
        }

        return list;
    }

    public static ControlEffectType ParseEffectTypeCode(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "m" => ControlEffectType.Mezz,
            "s" => ControlEffectType.Stun,
            "r" => ControlEffectType.Root,
            _ => ControlEffectType.Stun
        };
    }
}
