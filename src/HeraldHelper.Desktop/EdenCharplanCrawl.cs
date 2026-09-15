using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HeraldHelper.Desktop;

/// <summary>
/// C# port of scripts/crawl_eden_charplan.js: fetches the Eden charplan assets
/// into a staging directory, generates the normalized dataset, then swaps it
/// into data/eden-charplan atomically.
/// </summary>
internal class EdenCharplanCrawl : CharplanCrawl
{
    private const string BaseUrl = "https://eden-daoc.net";
    private const string UserAgent = "Mozilla/5.0";

    private static readonly (string Url, string RelPath, bool Versioned)[] TextAssets =
    [
        ("/chrplan/charplan.js", "charplan.js", false),
        ("/chrplan/charplan.css", "charplan.css", false),
        ("/chrplan/daoc.json", "daoc.json", false),
        ("/chrplan/charplan.json", "charplan.json", true),
        ("/chrplan/charplan_cl.json", "charplan_cl.json", true),
        ("/chrplan/icons.txt", "icons.txt", false)
    ];

    private static readonly string[] SpriteAssets =
    [
        "spl_0.png", "spl_100.png", "spl_200.png", "spl_300.png", "spl_400.png",
        "cbt_500.png", "cbt_600.png", "cbt_700.png", "cbt_800.png",
        "wpn_900.png", "wpn_1000.png",
        "itm_1200.png", "itm_1300.png", "itm_1400.png", "itm_1500.png",
        "spl_0n.png", "spl_100n.png", "spl_200n.png", "spl_300n.png", "spl_400n.png",
        "cbt_500n.png", "cbt_600n.png", "cbt_700n.png", "cbt_800n.png",
        "wpn_900n.png", "wpn_1000n.png",
        "itm_1200n.png", "itm_1300n.png", "itm_1400n.png", "itm_1500n.png",
        "icon_borders.png", "icon_corners.png", "icon_spells.png"
    ];

    private static readonly Regex VersionRegex = new(@"var v = ""([^""]+)""", RegexOptions.Compiled);

    public EdenCharplanCrawl(string projectRoot, StringBuilder output, IProgress<string>? progress)
        : base(projectRoot, "eden-charplan", output, progress)
    {
    }

    protected virtual Task<string> FetchTextAsync(HttpClient client, string url, CancellationToken cancellationToken)
        => FetchText(client, url, UserAgent, cancellationToken);

    protected virtual Task<byte[]> FetchBinaryAsync(HttpClient client, string url, CancellationToken cancellationToken)
        => FetchBinary(client, url, UserAgent, cancellationToken);

    public override async Task RunAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var rawDir = Path.Combine(StagingRoot, "raw");
        var generatedDir = Path.Combine(StagingRoot, "generated");
        var classDir = Path.Combine(generatedDir, "classes");
        var spriteDir = Path.Combine(StagingRoot, "assets", "sprites");

        DeleteStaging();
        Directory.CreateDirectory(rawDir);
        Directory.CreateDirectory(classDir);
        Directory.CreateDirectory(spriteDir);

        try
        {
            var raw = new Dictionary<string, string>(StringComparer.Ordinal);

            Report("Fetching charplan.js");
            var charplanScript = await FetchTextAsync(client, BaseUrl + TextAssets[0].Url, cancellationToken);
            raw[TextAssets[0].RelPath] = charplanScript;
            File.WriteAllText(Path.Combine(rawDir, TextAssets[0].RelPath), charplanScript);

            var version = ParseVersion(charplanScript)
                ?? throw new InvalidOperationException("Could not determine the Eden charplan version.");
            Report($"Charplan version {version}");

            foreach (var asset in TextAssets.Skip(1))
            {
                var url = asset.Versioned
                    ? $"{asset.Url}?v={Uri.EscapeDataString(version)}"
                    : asset.Url;
                Report($"Fetching {asset.RelPath}");
                var content = await FetchTextAsync(client, BaseUrl + url, cancellationToken);
                raw[asset.RelPath] = content;
                File.WriteAllText(Path.Combine(rawDir, asset.RelPath), content);
            }

            foreach (var spriteName in SpriteAssets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var buffer = await FetchBinaryAsync(client, $"{BaseUrl}/itm/icon/{spriteName}", cancellationToken);
                File.WriteAllBytes(Path.Combine(spriteDir, spriteName), buffer);
            }
            Report($"Downloaded {SpriteAssets.Length} sprite sheets");

            var daoc = JsonNode.Parse(raw["daoc.json"])!;
            var charplan = JsonNode.Parse(raw["charplan.json"])!.AsArray();
            var charplanCl = JsonNode.Parse(raw["charplan_cl.json"])!.AsArray();
            var iconMaps = ParseIconsTxt(raw["icons.txt"]);

            var supported = charplan
                .Where(entry => entry?["name"]?.GetValue<string>() != "Mauler")
                .ToList();
            var classes = supported
                .Select(entry => BuildClassRecord(entry!, daoc["classes"] as JsonObject, iconMaps))
                .ToList();

            if (classes.Count < 40 || iconMaps.Icons.Count < 1000 || iconMaps.Spells.Count < 1000)
            {
                throw new InvalidOperationException(
                    $"Eden snapshot validation failed (classes={classes.Count}, icons={iconMaps.Icons.Count}, spells={iconMaps.Spells.Count}).");
            }

            var classIndex = new JsonArray();
            foreach (var cls in classes)
            {
                classIndex.Add(new JsonObject
                {
                    ["id"] = cls["id"]!.DeepClone(),
                    ["name"] = cls["name"]!.DeepClone(),
                    ["slug"] = cls["slug"]!.DeepClone(),
                    ["realm"] = cls["realm"]?.DeepClone(),
                    ["races"] = cls["races"]?.DeepClone(),
                    ["specPointMultiplier"] = cls["specPointMultiplier"]?.DeepClone(),
                    ["realmAbilityCount"] = cls["realmAbilities"]!.AsArray().Count,
                    ["specCount"] = cls["specs"]!.AsArray().Count,
                    ["skillCount"] = cls["flattenedSkills"]!.AsArray().Count
                });
            }

            var allRealmAbilities = new JsonArray();
            var allFlattenedSkills = new JsonArray();
            foreach (var cls in classes)
            {
                var className = cls["name"]!.DeepClone();
                var classSlug = cls["slug"]!.DeepClone();
                foreach (var ra in cls["realmAbilities"]!.AsArray())
                {
                    var item = new JsonObject
                    {
                        ["className"] = className.DeepClone(),
                        ["classSlug"] = classSlug.DeepClone()
                    };
                    foreach (var pair in ra!.AsObject())
                    {
                        item[pair.Key] = pair.Value?.DeepClone();
                    }
                    allRealmAbilities.Add(item);
                }
                foreach (var skill in cls["flattenedSkills"]!.AsArray())
                {
                    var item = new JsonObject
                    {
                        ["className"] = className.DeepClone(),
                        ["classSlug"] = classSlug.DeepClone()
                    };
                    foreach (var pair in skill!.AsObject())
                    {
                        item[pair.Key] = pair.Value?.DeepClone();
                    }
                    allFlattenedSkills.Add(item);
                }
            }

            foreach (var cls in classes)
            {
                WriteJson(Path.Combine(classDir, cls["slug"]!.GetValue<string>() + ".json"), cls);
            }

            var uniqueIconConfigs = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var cls in classes)
            {
                foreach (var ra in cls["realmAbilities"]!.AsArray())
                {
                    CollectIcon(ra?["icon"], uniqueIconConfigs);
                }
                foreach (var spec in cls["specs"]!.AsArray())
                {
                    foreach (var group in spec!["skillGroups"]!.AsArray())
                    {
                        foreach (var skill in group!["skills"]!.AsArray())
                        {
                            CollectSkillIcons(skill, uniqueIconConfigs);
                        }
                    }
                    foreach (var line in spec!["spellLines"]!.AsArray())
                    {
                        foreach (var group in line!["spellGroups"]!.AsArray())
                        {
                            foreach (var skill in group!["skills"]!.AsArray())
                            {
                                CollectSkillIcons(skill, uniqueIconConfigs);
                            }
                        }
                    }
                }
            }

            WriteJson(Path.Combine(generatedDir, "manifest.json"), new JsonObject
            {
                ["source"] = BaseUrl,
                ["version"] = version,
                ["generatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                ["classCount"] = classes.Count,
                ["charplanClassCount"] = charplan.Count,
                ["ignoredClasses"] = new JsonArray("Mauler"),
                ["charplanClCount"] = charplanCl.Count,
                ["uniqueIconConfigCount"] = uniqueIconConfigs.Count,
                ["files"] = new JsonObject
                {
                    ["raw"] = new JsonArray(TextAssets.Select(a => (JsonNode)$"raw/{a.RelPath}").ToArray()),
                    ["classIndex"] = "generated/classes/index.json",
                    ["realmAbilities"] = "generated/realm-abilities.json",
                    ["flattenedSkills"] = "generated/skills-flat.json",
                    ["iconConfigs"] = "generated/icon-configs.json",
                    ["spriteDir"] = "assets/sprites"
                }
            });

            WriteJson(Path.Combine(classDir, "index.json"), classIndex);
            WriteJson(Path.Combine(generatedDir, "realm-abilities.json"), allRealmAbilities);
            WriteJson(Path.Combine(generatedDir, "skills-flat.json"), allFlattenedSkills);
            WriteJson(Path.Combine(generatedDir, "icon-configs.json"),
                new JsonArray(uniqueIconConfigs.Values.Select(v => v.DeepClone()).ToArray()));
            WriteJson(Path.Combine(generatedDir, "charplan-cl.json"), charplanCl);

            PublishSnapshot();

            Report($"Saved Eden charplan dataset to {FinalRoot}");
            Report($"Classes: {classes.Count}");
            Report($"Realm abilities: {allRealmAbilities.Count}");
            Report($"Flattened skills: {allFlattenedSkills.Count}");
            Report($"Unique icon configs: {uniqueIconConfigs.Count}");
        }
        catch
        {
            DeleteStaging();
            throw;
        }
    }

    private static string? ParseVersion(string charplanScript)
    {
        var match = VersionRegex.Match(charplanScript);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal sealed record IconMaps(
        List<JsonNode?> Icons,
        List<JsonNode?> Spells,
        List<JsonNode?> Styles);

    internal static IconMaps ParseIconsTxt(string content)
    {
        var sections = content.Split('|');
        var baseRaw = sections.ElementAtOrDefault(0) ?? string.Empty;
        var spellsRaw = sections.ElementAtOrDefault(1) ?? string.Empty;
        var stylesRaw = sections.ElementAtOrDefault(2) ?? string.Empty;

        var icons = new List<JsonNode?>();
        var spells = new List<JsonNode?>();
        var styles = new List<JsonNode?>();

        static void Ensure<T>(List<T> list, int index)
        {
            while (list.Count <= index)
            {
                list.Add(default!);
            }
        }

        // Eden uses line numbers as IDs. Empty lines are intentional
        // placeholders, so removing them shifts every mapping after the gap.
        var baseLines = baseRaw.Split('\n');
        for (var index = 0; index < baseLines.Length; index++)
        {
            var line = baseLines[index].Trim('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(',');
            var icon = new JsonArray(
                int.Parse(parts[0]), 0, 0, 0, 0, 0, 0, 0, 0, 0);
            for (var i = 1; i < parts.Length; i++)
            {
                var token = parts[i];
                var value = int.Parse(token[1..]) + 1;
                switch (token[0])
                {
                    case 'B': icon[1] = value; break;
                    case 'S': icon[9] = value; break;
                    case '0': icon[2] = value; break;
                    case '1': icon[3] = value; break;
                    case '2': icon[4] = value; break;
                    case '3': icon[5] = value; break;
                    case '4': icon[6] = value; break;
                    case '5': icon[7] = value; break;
                    case '6': icon[8] = value; break;
                }
            }
            Ensure(icons, index + 1);
            icons[index + 1] = icon;
        }

        var spellLines = spellsRaw.Split('\n');
        for (var index = 0; index < spellLines.Length; index++)
        {
            var line = spellLines[index].Trim('\r');
            if (line.Length == 0)
            {
                continue;
            }
            Ensure(spells, index + 1);
            spells[index + 1] = int.TryParse(line, out var spell) ? spell : null;
        }

        var styleLines = stylesRaw.Split('\n');
        for (var index = 0; index < styleLines.Length; index++)
        {
            var line = styleLines[index].Trim('\r');
            if (line.Length == 0)
            {
                continue;
            }
            Ensure(styles, index + 1);
            styles[index + 1] = int.TryParse(line, out var style) ? style : null;
        }

        return new IconMaps(icons, spells, styles);
    }

    private static string? ResolveSpriteSheetName(int spriteClass, bool isNight = false)
    {
        var suffix = isNight ? "n" : string.Empty;
        return spriteClass switch
        {
            <= 400 => $"spl_{spriteClass}{suffix}.png",
            >= 500 and <= 800 => $"cbt_{spriteClass}{suffix}.png",
            900 or 1000 => $"wpn_{spriteClass}{suffix}.png",
            >= 1200 and <= 1500 => $"itm_{spriteClass}{suffix}.png",
            _ => null
        };
    }

    private static JsonNode? GetIndexed(List<JsonNode?> list, JsonNode? indexNode)
    {
        if (indexNode is not JsonValue value || !value.TryGetValue<int>(out var index) ||
            index < 0 || index >= list.Count)
        {
            return null;
        }
        return list[index];
    }

    private static JsonObject? BuildIconReference(JsonObject skill, IconMaps iconMaps)
    {
        JsonArray? resolvedIcon = null;
        string? mapType = null;
        var iconNode = skill["icon"];
        var iconId = IsTruthy(iconNode) && iconNode is JsonValue iconValue &&
            iconValue.TryGetValue<int>(out var parsedIcon) ? parsedIcon : 0;
        var skillType = skill["skillType"]?.GetValue<int>() ?? 0;

        if (skillType == 2 && iconId != 0)
        {
            var mapped = GetIndexed(iconMaps.Spells, iconNode);
            var icon = GetIndexed(iconMaps.Icons, mapped) as JsonArray;
            if (icon is not null)
            {
                mapType = "spell";
                resolvedIcon = icon;
            }
        }
        else if (skillType == 3 && iconId != 0)
        {
            var mapped = GetIndexed(iconMaps.Styles, iconNode);
            var icon = GetIndexed(iconMaps.Icons, mapped) as JsonArray;
            if (icon is not null)
            {
                mapType = "style";
                resolvedIcon = icon;
            }
        }
        else if (iconId != 0 && skill.ContainsKey("costScheme"))
        {
            var icon = GetIndexed(iconMaps.Icons, iconNode) as JsonArray;
            if (icon is not null)
            {
                mapType = "direct";
                resolvedIcon = icon;
            }
        }

        if (resolvedIcon is null)
        {
            return null;
        }

        var baseSpriteIndex = resolvedIcon[0]!.GetValue<int>();
        var spriteClass = baseSpriteIndex / 100 * 100;
        var spriteCell = baseSpriteIndex % 100;
        var spriteX = spriteCell % 10;
        var spriteY = (spriteCell - spriteX) / 10;

        return new JsonObject
        {
            ["mappingType"] = mapType,
            ["requestedIconId"] = iconId,
            ["resolvedSpriteIndex"] = baseSpriteIndex,
            ["spriteClass"] = spriteClass,
            ["spriteSheet"] = ResolveSpriteSheetName(spriteClass),
            ["spriteSheetNight"] = ResolveSpriteSheetName(spriteClass, true),
            ["sprite"] = new JsonObject
            {
                ["x"] = spriteX,
                ["y"] = spriteY,
                ["width"] = 32,
                ["height"] = 32
            },
            ["overlays"] = new JsonObject
            {
                ["border"] = resolvedIcon[1]!.GetValue<int>(),
                ["corners"] = new JsonObject
                {
                    ["upLeft"] = resolvedIcon[2]!.GetValue<int>(),
                    ["up"] = resolvedIcon[3]!.GetValue<int>(),
                    ["upRight"] = resolvedIcon[4]!.GetValue<int>(),
                    ["right"] = resolvedIcon[5]!.GetValue<int>(),
                    ["downRight"] = resolvedIcon[6]!.GetValue<int>(),
                    ["down"] = resolvedIcon[7]!.GetValue<int>(),
                    ["left"] = resolvedIcon[8]!.GetValue<int>()
                },
                ["spellBadge"] = resolvedIcon[9]!.GetValue<int>()
            }
        };
    }

    private static JsonArray NormalizeAttributes(JsonNode? attributes)
    {
        var result = new JsonArray();
        if (attributes is JsonArray pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair is JsonArray pairArray && pairArray.Count >= 1)
                {
                    // JS destructures [name, value]; a missing value is
                    // undefined and JSON.stringify omits it entirely.
                    var item = new JsonObject
                    {
                        ["name"] = pairArray[0]?.DeepClone()
                    };
                    if (pairArray.Count >= 2)
                    {
                        item["value"] = pairArray[1]?.DeepClone();
                    }
                    result.Add(item);
                }
            }
        }
        return result;
    }

    private static JsonObject NormalizeSkill(JsonObject skill, IconMaps iconMaps)
    {
        var subSkills = new JsonArray();
        if (skill["subSkills"] is JsonArray subs)
        {
            foreach (var sub in subs)
            {
                if (sub is JsonObject subObj)
                {
                    subSkills.Add(NormalizeSkill(subObj, iconMaps));
                }
            }
        }

        return new JsonObject
        {
            ["id"] = skill["id"]?.DeepClone(),
            ["name"] = skill["name"]?.DeepClone(),
            ["level"] = skill["level"]?.DeepClone(),
            ["skillType"] = skill["skillType"]?.DeepClone(),
            ["requirement"] = NullIfFalsy(skill["requirement"])?.DeepClone(),
            ["iconId"] = IsTruthy(skill["icon"]) ? skill["icon"]!.DeepClone() : 0,
            ["attributes"] = NormalizeAttributes(skill["attributes"]),
            ["icon"] = BuildIconReference(skill, iconMaps),
            ["subSkills"] = subSkills
        };
    }

    private static void FlattenSkill(JsonObject skill, List<JsonNode> parentPath, JsonArray bucket)
    {
        bucket.Add(new JsonObject
        {
            ["id"] = skill["id"]?.DeepClone(),
            ["name"] = skill["name"]?.DeepClone(),
            ["level"] = skill["level"]?.DeepClone(),
            ["skillType"] = skill["skillType"]?.DeepClone(),
            ["parentPath"] = new JsonArray(parentPath.Select(p => p.DeepClone()).ToArray())
        });

        if (skill["subSkills"] is JsonArray subs)
        {
            var nextPath = new List<JsonNode>(parentPath) { skill["name"]!.DeepClone() };
            foreach (var sub in subs)
            {
                if (sub is JsonObject subObj)
                {
                    FlattenSkill(subObj, nextPath, bucket);
                }
            }
        }
    }

    private static JsonObject BuildClassRecord(JsonNode classEntry, JsonObject? daocClasses, IconMaps iconMaps)
    {
        var entry = classEntry.AsObject();
        var name = entry["name"]!.GetValue<string>();
        var classMeta = daocClasses is not null && daocClasses.TryGetPropertyValue(name, out var meta)
            ? meta as JsonObject
            : null;

        var specs = new JsonArray();
        if (entry["specs"] is JsonArray specList)
        {
            foreach (var specNode in specList)
            {
                if (specNode is not JsonObject spec) continue;

                var skillGroups = new JsonArray();
                if (spec["skillGroups"] is JsonArray groups)
                {
                    foreach (var groupNode in groups)
                    {
                        if (groupNode is not JsonObject group) continue;
                        var skills = new JsonArray();
                        if (group["skills"] is JsonArray skillList)
                        {
                            foreach (var skillNode in skillList)
                            {
                                if (skillNode is JsonObject skillObj)
                                {
                                    skills.Add(NormalizeSkill(skillObj, iconMaps));
                                }
                            }
                        }
                        skillGroups.Add(new JsonObject
                        {
                            ["name"] = group["name"]?.DeepClone(),
                            ["skills"] = skills
                        });
                    }
                }

                var spellLines = new JsonArray();
                if (spec["spellLines"] is JsonArray lines)
                {
                    foreach (var lineNode in lines)
                    {
                        if (lineNode is not JsonObject line) continue;
                        var spellGroups = new JsonArray();
                        if (line["spellGroups"] is JsonArray groups2)
                        {
                            foreach (var groupNode in groups2)
                            {
                                if (groupNode is not JsonObject group) continue;
                                var skills = new JsonArray();
                                if (group["skills"] is JsonArray skillList)
                                {
                                    foreach (var skillNode in skillList)
                                    {
                                        if (skillNode is JsonObject skillObj)
                                        {
                                            skills.Add(NormalizeSkill(skillObj, iconMaps));
                                        }
                                    }
                                }
                                spellGroups.Add(new JsonObject
                                {
                                    ["name"] = group["name"]?.DeepClone(),
                                    ["skills"] = skills
                                });
                            }
                        }
                        spellLines.Add(new JsonObject
                        {
                            ["id"] = line["id"]?.DeepClone(),
                            ["name"] = line["name"]?.DeepClone(),
                            ["base"] = IsTruthy(line["base"]),
                            ["level"] = line["level"]?.DeepClone(),
                            ["spellGroups"] = spellGroups
                        });
                    }
                }

                specs.Add(new JsonObject
                {
                    ["id"] = spec["id"]?.DeepClone(),
                    ["name"] = spec["name"]?.DeepClone(),
                    ["base"] = IsTruthy(spec["base"]),
                    ["autotrain"] = IsTruthy(spec["autotrain"]),
                    ["skillGroups"] = skillGroups,
                    ["spellLines"] = spellLines
                });
            }
        }

        var flatSkills = new JsonArray();
        foreach (var spec in specs)
        {
            var specName = spec!["name"]!.DeepClone();
            foreach (var group in spec!["skillGroups"]!.AsArray())
            {
                var groupName = group!["name"]!.DeepClone();
                foreach (var skill in group!["skills"]!.AsArray())
                {
                    FlattenSkill(skill!.AsObject(),
                        [entry["name"]!.DeepClone(), specName.DeepClone(), groupName.DeepClone()], flatSkills);
                }
            }
            foreach (var line in spec!["spellLines"]!.AsArray())
            {
                var lineName = line!["name"]!.DeepClone();
                foreach (var group in line!["spellGroups"]!.AsArray())
                {
                    var groupName = group!["name"]!.DeepClone();
                    foreach (var skill in group!["skills"]!.AsArray())
                    {
                        FlattenSkill(skill!.AsObject(),
                            [entry["name"]!.DeepClone(), specName.DeepClone(), lineName.DeepClone(), groupName.DeepClone()],
                            flatSkills);
                    }
                }
            }
        }

        var realmAbilities = new JsonArray();
        if (entry["realmAbilities"] is JsonArray raList)
        {
            foreach (var raNode in raList)
            {
                if (raNode is not JsonObject ra) continue;
                var raForIcon = (JsonObject)ra.DeepClone();
                raForIcon["skillType"] = 0;

                var levels = new JsonArray();
                if (ra["levels"] is JsonArray levelList)
                {
                    foreach (var levelNode in levelList)
                    {
                        if (levelNode is not JsonObject level) continue;
                        levels.Add(new JsonObject
                        {
                            ["cost"] = level["cost"]?.DeepClone(),
                            ["shortInfo"] = level["shortInfo"]?.DeepClone(),
                            ["cooldown"] = NullIfFalsy(level["cooldown"])?.DeepClone(),
                            ["value"] = level.ContainsKey("value") ? level["value"]?.DeepClone() : null
                        });
                    }
                }

                realmAbilities.Add(new JsonObject
                {
                    ["id"] = ra["id"]?.DeepClone(),
                    ["name"] = ra["name"]?.DeepClone(),
                    ["description"] = ra["description"]?.DeepClone(),
                    ["costScheme"] = ra["costScheme"]?.DeepClone(),
                    ["iconId"] = IsTruthy(ra["icon"]) ? ra["icon"]!.DeepClone() : 0,
                    ["icon"] = BuildIconReference(raForIcon, iconMaps),
                    ["levels"] = levels
                });
            }
        }

        return new JsonObject
        {
            ["id"] = entry["id"]?.DeepClone(),
            ["name"] = entry["name"]?.DeepClone(),
            ["slug"] = Slugify(name),
            ["realm"] = classMeta?["realm"]?.DeepClone(),
            ["races"] = classMeta?["races"]?.DeepClone() ?? new JsonArray(),
            ["armor"] = classMeta?["armor"]?.DeepClone(),
            ["classGroups"] = classMeta?["cls"]?.DeepClone() ?? new JsonArray(),
            ["weapons"] = classMeta?["weapons"]?.DeepClone() ?? new JsonObject(),
            ["specPointMultiplier"] = entry["specPointMultiplier"]?.DeepClone(),
            ["realmAbilities"] = realmAbilities,
            ["specs"] = specs,
            ["flattenedSkills"] = flatSkills
        };
    }

    private static void CollectIcon(JsonNode? icon, Dictionary<string, JsonNode> unique)
    {
        if (icon is null)
        {
            return;
        }
        var key = icon.ToJsonString();
        unique.TryAdd(key, icon);
    }

    private static void CollectSkillIcons(JsonNode? skill, Dictionary<string, JsonNode> unique)
    {
        if (skill is null)
        {
            return;
        }
        CollectIcon(skill["icon"], unique);
        if (skill["subSkills"] is JsonArray subs)
        {
            foreach (var sub in subs)
            {
                CollectSkillIcons(sub, unique);
            }
        }
    }
}
