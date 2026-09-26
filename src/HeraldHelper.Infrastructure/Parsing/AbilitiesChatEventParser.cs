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
    /// "You target [X]" and "You enter combat mode and target [X]" both
    /// name the current target — the combat-mode variant is what melee
    /// swings print, so it must drive CC attribution too.
    private static readonly Regex TargetBracketRegex = new(@"you\s+(?:enter\s+combat\s+mode\s+and\s+)?target\s*\[(?<name>[^\]\r\n]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TargetPlainRegex = new(
        @"you\s+(?:enter\s+combat\s+mode\s+and\s+)?target\s+(?<name>(?!\[).*?)" + NextMessageBoundary,
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
    private static readonly Regex IncomingMeleeRegex = new(
        @"(?<name>[A-Z][A-Za-z'\-]+)\s+(?<verb>attacks?|hits?|shoots|slashes|slices|stabs|crushes|smites|criticals?|critical\s+hits?)\s+you(?:\s+for\s+(?<dmg>\d+)\s+\w*?\s*damage)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IncomingMissRegex = new(
        @"(?<name>[A-Z][A-Za-z'\-]+)\s+(?:misses|fails?\s+to\s+hit|cannot\s+hit)\s+you",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IncomingSpellRegex = new(
        @"(?<name>[A-Z][A-Za-z'\-]+)\s+casts?\s+a\s+spell\s+on\s+you",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KillRegex = new(
        @"you\s+(?:have\s+)?(?:slain|killed|slay)\s+(?<name>[A-Z][A-Za-z'\-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DeathKillerRegex = new(
        @"(?:you\s+have\s+been\s+killed\s+by\s+(?<name>[A-Z][A-Za-z'\-]+)|(?<name>[A-Z][A-Za-z'\-]+)\s+(?:has\s+just\s+)?kills?\s+you)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DeathPlainRegex = new(
        @"you\s+die\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RealmAbilityUseRegex = new(
        @"you\s+(?:use|activate)\s+(?<name>[A-Z][A-Za-z'\- ]{2,40}?)(?=\s*(?:[\.\!]|$|\s+on\b|\s+at\b))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly (string Marker, ControlEffectType Effect)[] SelfCcMarkers =
    [
        ("you are stunned", ControlEffectType.Stun),
        ("you are mesmerized", ControlEffectType.Mezz),
        ("you are put to sleep", ControlEffectType.Mezz),
        ("you fall into a deep sleep", ControlEffectType.Mezz),
        ("you are rooted", ControlEffectType.Root),
        ("you are snared", ControlEffectType.Snare),
        ("you cannot move", ControlEffectType.Stun),
        ("you are entangled", ControlEffectType.Root),
        ("you are paralyzed", ControlEffectType.Stun),
        ("you are nearsighted", ControlEffectType.Nearsight),
        ("you are entranced", ControlEffectType.Mezz),
        // Effect Message1 strings (OpenDAoC spell DB) — the first-person
        // line the target actually receives per spell type. SpeedDecrease
        // with Value=99 is a root ("feet frozen"); partial-speed snares and
        // damage+snare spells print the bonds/hindered lines instead.
        ("combat skills are hampered by blindness", ControlEffectType.Nearsight),
        ("your feet are frozen to the ground", ControlEffectType.Root),
        ("rocks rise from the ground and obstruct your movement", ControlEffectType.Root),
        ("constricting bonds surround your body", ControlEffectType.Snare),
        ("a blast of energy hinders you", ControlEffectType.Snare),
        ("a flash of light bursts in front of you", ControlEffectType.Stun),
        ("your movement is slowed", ControlEffectType.Snare),
        ("you are enveloped by numbing cold", ControlEffectType.Snare)
    ];
    /// Effect-expire (Message3) strings — the OpenDAoC spell DB values for
    /// CC types. For SELF effects these arrive reliably (the target is you),
    /// so they end the self-CC banner early; for enemies they only render
    /// in proximity, which is why target timers don't use them.
    private static readonly (string Marker, ControlEffectType Effect)[] SelfCcExpireMarkers =
    [
        ("you recover from the stun", ControlEffectType.Stun),
        ("you are no longer entranced", ControlEffectType.Mezz),
        ("you recover from the mesmerize", ControlEffectType.Mezz),
        ("your vision returns to normal", ControlEffectType.Nearsight),
        ("the bonds holding you break", ControlEffectType.Snare),
        ("the constricting bonds around you fall away", ControlEffectType.Snare),
        ("you can move normally again", ControlEffectType.Snare),
        ("the energy hindering you dissipates", ControlEffectType.Snare)
    ];
    /// Broadcast effect lines (spell Message2) — a NEARBY player's name in
    /// third person. Same strings as SelfCcMarkers but with "{0} is …" —
    /// these fire for groupmates and enemies alike; the consumer resolves
    /// the name. Longer variants share the base pattern ("is stunned by a
    /// barrage of color" still matches "is stunned").
    private static readonly (Regex Pattern, ControlEffectType Effect)[] BroadcastCcApplyPatterns =
    [
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) begins moving more slowly", ControlEffectType.Snare),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) cannot seem to move", ControlEffectType.Stun),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) is entranced", ControlEffectType.Mezz),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) is mesmerized", ControlEffectType.Mezz),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) is stunned", ControlEffectType.Stun),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) is surrounded by constricting bonds", ControlEffectType.Snare),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) stumbles, unable to see", ControlEffectType.Nearsight),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?)'s feet are frozen to the ground", ControlEffectType.Root),
        BroadcastRegex(@"rocks rise from the ground and trip (?<name>[^.!\r\n]+)", ControlEffectType.Root),
    ];
    /// Broadcast expire lines (spell Message4) — "{Name} recovers from…".
    /// "{0}'s attacks return to normal" is MeleeHasteDebuff (not CC) and
    /// is deliberately absent.
    private static readonly (Regex Pattern, ControlEffectType Effect)[] BroadcastCcExpirePatterns =
    [
        BroadcastRegex(@"the blindness recedes from (?<name>[^.!\r\n]+)", ControlEffectType.Nearsight),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) can move normally again", ControlEffectType.Snare),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) is no longer entranced", ControlEffectType.Mezz),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) recovers from the mesmerize", ControlEffectType.Mezz),
        BroadcastRegex(@"(?<name>[A-Z][A-Za-z0-9'\- ]{1,39}?) recovers from the stun", ControlEffectType.Stun),
    ];
    private static (Regex Pattern, ControlEffectType Effect) BroadcastRegex(string pattern, ControlEffectType effect)
    {
        return (new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), effect);
    }

    private static readonly string[] CastInterruptedMarkers =
    [
        "you move and interrupt your spellcast",
        "your spell is interrupted",
        "casting has been interrupted",
        "you are interrupted",
        "you move and interrupt your spell",
        "you fumble the spell",
        "you lose your concentration",
        "cannot concentrate enough to cast",
        "your spell is cancelled",
        "your spell is canceled",
        "you can't cast while",
        "you cant cast while",
        "you are fumbling for your words",
        "you do not have enough power and your spell was canceled",
        "you are too tired to hold your shot",
        // CheckBeginCast rejections — the cast never started, so any
        // pending castbar entry must clear. (SpellHandler.CheckBeginCast)
        "you don't have enough power to cast",
        "you have exhausted all of your power",
        "that target is too far away",
        "your target is not visible",
        "you can't see your target",
        "you must select a target for this spell",
        "you are not wielding the right type of instrument",
        "you can't cast while sitting",
        "seconds to cast a spell",
        "you are already playing a song"
    ];
    /// "{0} is attacking you and your {1} is interrupted!" — the interrupt
    /// tail must be present; the prefix alone also matches pet attacks.
    private static readonly Regex AttackInterruptRegex = new(
        @"is attacking you and your .{1,25}? is interrupted",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// "{name} resists the effect! (34.0%)" / "resists the charm!" — the
    /// target of YOUR spell shrugged it off. Older format "X resists your
    /// Slam!" stays covered by the per-mention window check.
    private static readonly Regex ResistTargetRegex = new(
        @"(?<name>[A-Za-z][A-Za-z'\- ]{1,40}?)\s+resists\s+the\s+(?:effect|charm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// "Your spell has no effect on the {name}." — the spell went off but
    /// the target shrugged it entirely (SpellHandler.CheckTarget).
    private static readonly Regex SpellNoEffectRegex = new(
        @"your\s+spell\s+has\s+no\s+effect\s+on\s+(?:the\s+)?(?<name>[A-Za-z][A-Za-z0-9'\- ]{1,40}?)(?=[\.\!]|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// CC was negated without a target name — applies to the current target.
    private static readonly string[] ImmuneMarkers =
    [
        "your target is immune to this effect",
        "your target is enraged and resists the spell",
        "your item effect intercepts the",
        "ceremonial bracer intercept",
        // AbstractCCSpellHandler: charging / too-fast targets are CC-immune.
        "your target is moving too fast"
    ];
    /// "{name} can't have that effect again yet!" (Eden immunity) and
    /// "{name} already has this effect!" — the application was rejected
    /// because the CC is still running; the prior timer stays valid.
    private static readonly Regex FailedApplicationNamedRegex = new(
        @"(?<name>[A-Za-z][A-Za-z0-9'\- ]{1,40}?)\s+(?:can't\s+have\s+that\s+effect\s+again|already\s+has\s+(?:this|that)\s+effect|is\s+too\s+strong\s+for\s+you\s+to\s+charm|can't\s+be\s+charmed|is\s+currently\s+being\s+controlled|is\s+moving\s+to[o]?\s+fast\s+for\s+this\s+spell)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] FailedApplicationMarkers =
    [
        "your target already has that effect",
        "you can't charm that target",
        "this spell does not charm that type of monster"
    ];
    /// Style lifecycle (server sends these only in the named phases):
    ///   prepare → button press queues the style (no swing yet)
    ///   perform perfectly → the swing hit AND the style fired
    ///   fail to execute → swing hit but the style did not fire
    ///   no longer preparing → queued style cancelled
    private static readonly Regex StylePrepareRegex = new(
        @"you\s+(?:prepare\s+to\s+perform|are\s+now\s+preparing\s+to\s+perform|automatically\s+attempt)\s+(?:a\s+|an\s+)?(?<name>.+?)(?:\s+style)?(?=\s+as\s+a\s+backup|[\.\!]|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleExecuteRegex = new(
        @"you\s+perform\s+your\s+(?<name>.+?)\s+perfectly\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PetStyleExecuteRegex = new(
        @"your\s+(?<pet>.+?)\s+performs\s+its\s+(?<name>.+?)\s+perfectly\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleFailRegex = new(
        @"you\s+fail\s+to\s+execute\s+your\s+(?<name>.+?)\s+perfectly\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleCancelRegex = new(
        @"you\s+are\s+no\s+longer\s+preparing\s+to\s+use\s+your\s+(?<name>.+?)\s+style\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// Swing deflected: "{name} blocks/parries/evades your attack!"
    private static readonly Regex SwingDeflectedRegex = new(
        @"(?<name>[A-Za-z][A-Za-z'\- ]{1,40}?)\s+(?:blocks|parries|evades)\s+your\s+attack",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] SwingFailedMarkers =
    [
        "you miss",
        "you were strafing in combat and miss",
        "you fumble the attack",
        "was absorbed by a magical barrier",
        "steps in front of"
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
        var negationEvents = ParseNegationEvents(normalizedOcrText);
        var contextSpans = BuildMentionContextSpans(normalizedOcrText);
        var resistedNames = new HashSet<string>(
            negationEvents.Where(x => x.Kind == NegationKind.Resisted && x.TargetName is not null)
                          .Select(x => NormalizeTargetName(x.TargetName!)),
            StringComparer.OrdinalIgnoreCase);
        var failedAppNames = new HashSet<string>(
            negationEvents.Where(x => x.Kind == NegationKind.FailedApplication && x.TargetName is not null)
                          .Select(x => NormalizeTargetName(x.TargetName!)),
            StringComparer.OrdinalIgnoreCase);
        var spellNegated = negationEvents.Any(x => x.Kind == NegationKind.Immune);
        var failedAppUnnamed = negationEvents.Any(x => x.Kind == NegationKind.FailedApplication && x.TargetName is null);
        var hitOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var mention in hitMentions)
        {
            var context = ClassifyMention(contextSpans, mention.Index);
            // "begin casting" / "prepare to perform" / "fail to execute"
            // name the ability without applying its effect — only a completed
            // cast or an executed style can land a CC.
            if (context is MentionContext.CastStart or MentionContext.StylePrepare
                or MentionContext.StyleFail or MentionContext.Resist)
            {
                continue;
            }

            var targetName = ResolveTargetForIndex(targetMentions, mention.Index);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = fallbackTargetName?.Trim() ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                continue;
            }

            targetName = NormalizeTargetName(targetName);

            var landed = context switch
            {
                // "You perform your X perfectly!" only fires on a resolved
                // hit — but "already has this effect" means the CC itself
                // was rejected even though the swing landed.
                MentionContext.StyleExecute => !failedAppUnnamed,
                // "You cast a X spell!" — landed unless a resist/immune
                // line names this target in the same frame.
                // "You begin playing X!" — a song IS the application; the
                // client prints no completion line for it.
                MentionContext.CastComplete or MentionContext.SongStart =>
                    !spellNegated && !failedAppUnnamed && !resistedNames.Contains(NormalizeTargetName(targetName)),
                // Failure text lives in the mention's own sentence or the ones
                // after it ("Foo resists your Slam!") — a "must wait … again"
                // line from an earlier attempt must not suppress a landed hit.
                _ => !failedAppUnnamed && !ContainsFailureKeyword(WindowFromLineStart(normalizedOcrText, mention.Index, mention.Ability.Name.Length))
            };
            if (resistedNames.Contains(NormalizeTargetName(targetName)) ||
                failedAppNames.Contains(NormalizeTargetName(targetName)))
            {
                landed = false;
            }
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
                occurrenceOrdinal,
                mention.Ability.Icon,
                context == MentionContext.StyleExecute));
        }

        return new ChatParseResult(
            targetEvent,
            hits,
            castEvent,
            visibleTargetEvents,
            visibleCastEvents,
            ParseSelfCcEvents(normalizedOcrText),
            ParseSelfCcExpireEvents(normalizedOcrText),
            ParseIncomingAttacks(normalizedOcrText),
            ParseLifeEvents(normalizedOcrText),
            ParseRealmAbilityEvents(normalizedOcrText),
            negationEvents,
            ParseBroadcastCcEvents(normalizedOcrText));
    }

    private enum MentionContext
    {
        Bare,
        CastStart,
        CastComplete,
        SongStart,
        StylePrepare,
        StyleExecute,
        StyleFail,
        Resist
    }

    /// <summary>Phrase spans that name an ability but mean different things —
    /// "begin casting X" is not "cast X", "prepare to perform X" is not
    /// "perform X perfectly".</summary>
    private static List<(int Start, int End, MentionContext Context)> BuildMentionContextSpans(string text)
    {
        var spans = new List<(int, int, MentionContext)>();
        foreach (Match m in BeginCastingRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.CastStart));
        }
        foreach (Match m in BeginPlayingRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.SongStart));
        }
        foreach (Match m in CastCompletedRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.CastComplete));
        }
        foreach (Match m in StylePrepareRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.StylePrepare));
        }
        foreach (Match m in StyleExecuteRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.StyleExecute));
        }
        foreach (Match m in PetStyleExecuteRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.StyleExecute));
        }
        foreach (Match m in StyleFailRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.StyleFail));
        }
        foreach (Match m in StyleCancelRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.StyleFail));
        }
        foreach (Match m in ResistTargetRegex.Matches(text))
        {
            spans.Add((m.Index, m.Index + m.Length, MentionContext.Resist));
        }
        return spans;
    }

    private static MentionContext ClassifyMention(
        IReadOnlyList<(int Start, int End, MentionContext Context)> spans,
        int mentionIndex)
    {
        // Prefer the tightest containing span — nested matches (a cast name
        // inside a longer sentence region) should resolve to the innermost.
        var best = MentionContext.Bare;
        var bestSize = int.MaxValue;
        foreach (var (start, end, context) in spans)
        {
            if (mentionIndex < start || mentionIndex >= end)
            {
                continue;
            }
            var size = end - start;
            if (size < bestSize)
            {
                bestSize = size;
                best = context;
            }
        }
        return best;
    }

    private static IReadOnlyList<NegationEvent> ParseNegationEvents(string ocrText)
    {
        var events = new List<(int Index, NegationEvent Event)>();

        foreach (Match m in ResistTargetRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.Resisted, CleanupResistName(m.Groups["name"].Value))));
        }

        foreach (Match m in SpellNoEffectRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.Resisted, CleanupResistName(m.Groups["name"].Value))));
        }

        var lowered = ocrText.ToLowerInvariant();
        foreach (var marker in ImmuneMarkers)
        {
            var startIndex = 0;
            while ((startIndex = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal)) >= 0)
            {
                events.Add((startIndex, new NegationEvent(NegationKind.Immune, null)));
                startIndex += marker.Length;
            }
        }

        foreach (Match m in StyleFailRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.StyleFailed, CleanupName(m.Groups["name"].Value))));
        }
        foreach (Match m in StyleCancelRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.StyleFailed, CleanupName(m.Groups["name"].Value))));
        }
        foreach (Match m in SwingDeflectedRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.SwingFailed, CleanupResistName(m.Groups["name"].Value))));
        }
        foreach (Match m in FailedApplicationNamedRegex.Matches(ocrText))
        {
            events.Add((m.Index, new NegationEvent(NegationKind.FailedApplication, CleanupResistName(m.Groups["name"].Value))));
        }
        foreach (var marker in FailedApplicationMarkers)
        {
            var startIndex = 0;
            while ((startIndex = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal)) >= 0)
            {
                events.Add((startIndex, new NegationEvent(NegationKind.FailedApplication, null)));
                startIndex += marker.Length;
            }
        }
        foreach (var marker in SwingFailedMarkers)
        {
            var startIndex = 0;
            while ((startIndex = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal)) >= 0)
            {
                events.Add((startIndex, new NegationEvent(NegationKind.SwingFailed, null)));
                startIndex += marker.Length;
            }
        }

        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return events
            .OrderBy(x => x.Index)
            .Select(x =>
            {
                var key = $"{x.Event.Kind}|{x.Event.TargetName ?? string.Empty}";
                ordinals.TryGetValue(key, out var ord);
                ordinals[key] = ++ord;
                return x.Event with { OccurrenceOrdinal = ord };
            })
            .ToList();
    }

    /// <summary>OCR can merge "You cast a X spell Alice resists…" without
    /// punctuation — cut a bleed-over prefix at the last phrase boundary.</summary>
    private static string CleanupResistName(string raw)
    {
        var name = CleanupName(raw);
        // "Your Moolish resists the effect!" — a pet-target resist reports
        // the pet's name prefixed by "Your".
        if (name.StartsWith("your ", StringComparison.OrdinalIgnoreCase))
        {
            name = name[5..].TrimStart();
        }
        foreach (var sep in new[] { " cast a ", " casts ", " casting ", " spell ", " you " })
        {
            int cut;
            while ((cut = name.LastIndexOf(sep, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                name = name[(cut + sep.Length)..];
            }
        }
        return name.Trim();
    }

    /// "the goborchend wounder"/"goborchend wounder" and the "---" suffix the
    /// adapter layer appends to non-player targets must all fold to one name.
    private static string NormalizeTargetName(string name)
    {
        var trimmed = name.Trim().TrimEnd('-').TrimEnd();
        return trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..].TrimStart()
            : trimmed;
    }

    private static IReadOnlyList<SelfCcEvent> ParseSelfCcEvents(string ocrText)
    {
        var lowered = ocrText.ToLowerInvariant();
        var events = new List<SelfCcEvent>();
        foreach (var (marker, effect) in SelfCcMarkers)
        {
            var count = 0;
            var startIndex = 0;
            while ((startIndex = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += marker.Length;
            }

            if (count > 0)
            {
                events.Add(new SelfCcEvent(effect, count));
            }
        }

        return events;
    }

    private static IReadOnlyList<SelfCcEvent> ParseSelfCcExpireEvents(string ocrText)
    {
        var lowered = ocrText.ToLowerInvariant();
        var events = new List<SelfCcEvent>();
        foreach (var (marker, effect) in SelfCcExpireMarkers)
        {
            var count = 0;
            var startIndex = 0;
            while ((startIndex = lowered.IndexOf(marker, startIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += marker.Length;
            }

            if (count > 0)
            {
                events.Add(new SelfCcEvent(effect, count));
            }
        }

        return events;
    }

    private static IReadOnlyList<BroadcastCcEvent> ParseBroadcastCcEvents(string ocrText)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var events = new List<BroadcastCcEvent>();
        CollectBroadcast(events, ordinals, BroadcastCcApplyPatterns, ocrText, applied: true);
        CollectBroadcast(events, ordinals, BroadcastCcExpirePatterns, ocrText, applied: false);
        return events;
    }

    private static void CollectBroadcast(
        List<BroadcastCcEvent> events,
        Dictionary<string, int> ordinals,
        (Regex Pattern, ControlEffectType Effect)[] patterns,
        string ocrText,
        bool applied)
    {
        foreach (var (pattern, effect) in patterns)
        {
            foreach (Match match in pattern.Matches(ocrText))
            {
                var name = CleanupName(match.Groups["name"].Value);
                // "You can move normally again" is the self-expire line —
                // the broadcast form names a third person.
                if (name.Equals("you", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                ordinals.TryGetValue(name, out var ord);
                ordinals[name] = ++ord;
                events.Add(new BroadcastCcEvent(name, effect, applied, ord));
            }
        }
    }

    private static IReadOnlyList<IncomingAttackEvent> ParseIncomingAttacks(string ocrText)
    {
        var events = new List<(int Index, IncomingAttackEvent Event)>();
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in IncomingMeleeRegex.Matches(ocrText))
        {
            var name = CleanupName(match.Groups["name"].Value);
            var verb = match.Groups["verb"].Value;
            var critical = verb.StartsWith("critical", StringComparison.OrdinalIgnoreCase);
            var damage = match.Groups["dmg"].Success &&
                         int.TryParse(match.Groups["dmg"].Value, out var dmg)
                ? dmg
                : (int?)null;
            ordinals.TryGetValue(name, out var ord);
            ordinals[name] = ++ord;
            events.Add((match.Index, new IncomingAttackEvent(name, damage, critical, false, false, ord)));
        }

        foreach (Match match in IncomingMissRegex.Matches(ocrText))
        {
            var name = CleanupName(match.Groups["name"].Value);
            ordinals.TryGetValue(name, out var ord);
            ordinals[name] = ++ord;
            events.Add((match.Index, new IncomingAttackEvent(name, null, false, true, false, ord)));
        }

        foreach (Match match in IncomingSpellRegex.Matches(ocrText))
        {
            var name = CleanupName(match.Groups["name"].Value);
            ordinals.TryGetValue(name, out var ord);
            ordinals[name] = ++ord;
            events.Add((match.Index, new IncomingAttackEvent(name, null, false, false, true, ord)));
        }

        return events.OrderBy(x => x.Index).Select(x => x.Event).ToList();
    }

    private static IReadOnlyList<CombatLifeEvent> ParseLifeEvents(string ocrText)
    {
        var events = new List<(int Index, CombatLifeEvent Event)>();
        var killOrd = 0;
        var deathOrd = 0;

        foreach (Match match in KillRegex.Matches(ocrText))
        {
            events.Add((match.Index, new CombatLifeEvent(
                CombatLifeKind.Kill, CleanupName(match.Groups["name"].Value), ++killOrd)));
        }

        foreach (Match match in DeathKillerRegex.Matches(ocrText))
        {
            var name = match.Groups["name"].Success ? CleanupName(match.Groups["name"].Value) : null;
            events.Add((match.Index, new CombatLifeEvent(CombatLifeKind.Death, name, ++deathOrd)));
        }

        foreach (Match match in DeathPlainRegex.Matches(ocrText))
        {
            events.Add((match.Index, new CombatLifeEvent(CombatLifeKind.Death, null, ++deathOrd)));
        }

        return events.OrderBy(x => x.Index).Select(x => x.Event).ToList();
    }

    private static IReadOnlyList<RealmAbilityEvent> ParseRealmAbilityEvents(string ocrText)
    {
        var events = new List<(int Index, RealmAbilityEvent Event)>();
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RealmAbilityUseRegex.Matches(ocrText))
        {
            var name = CleanupName(match.Groups["name"].Value);
            if (name.Length < 3)
            {
                continue;
            }

            ordinals.TryGetValue(name, out var ord);
            ordinals[name] = ++ord;
            events.Add((match.Index, new RealmAbilityEvent(name, ord)));
        }

        return events.OrderBy(x => x.Index).Select(x => x.Event).ToList();
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

        foreach (Match match in AttackInterruptRegex.Matches(ocrText))
        {
            events.Add((match.Index, new CastEvent(CastEventType.Interrupted, null)));
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
            "n" => ControlEffectType.Nearsight,
            "e" => ControlEffectType.Snare,
            _ => ControlEffectType.Stun
        };
    }
}
