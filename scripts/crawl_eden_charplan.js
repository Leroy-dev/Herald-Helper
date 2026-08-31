const fs = require("fs");
const path = require("path");

const BASE_URL = "https://eden-daoc.net";
const FINAL_OUT_ROOT = path.resolve(__dirname, "..", "data", "eden-charplan");
const OUT_ROOT = `${FINAL_OUT_ROOT}.staging-${process.pid}`;
const RAW_DIR = path.join(OUT_ROOT, "raw");
const GENERATED_DIR = path.join(OUT_ROOT, "generated");
const CLASS_DIR = path.join(GENERATED_DIR, "classes");
const ASSET_DIR = path.join(OUT_ROOT, "assets");
const SPRITE_DIR = path.join(ASSET_DIR, "sprites");

const TEXT_ASSETS = [
  { url: "/chrplan/charplan.js", relPath: "charplan.js" },
  { url: "/chrplan/charplan.css", relPath: "charplan.css" },
  { url: "/chrplan/daoc.json", relPath: "daoc.json" },
  { url: "/chrplan/charplan.json", relPath: "charplan.json", versioned: true },
  { url: "/chrplan/charplan_cl.json", relPath: "charplan_cl.json", versioned: true },
  { url: "/chrplan/icons.txt", relPath: "icons.txt" }
];

const SPRITE_ASSETS = [
  "spl_0.png",
  "spl_100.png",
  "spl_200.png",
  "spl_300.png",
  "spl_400.png",
  "cbt_500.png",
  "cbt_600.png",
  "cbt_700.png",
  "cbt_800.png",
  "wpn_900.png",
  "wpn_1000.png",
  "itm_1200.png",
  "itm_1300.png",
  "itm_1400.png",
  "itm_1500.png",
  "spl_0n.png",
  "spl_100n.png",
  "spl_200n.png",
  "spl_300n.png",
  "spl_400n.png",
  "cbt_500n.png",
  "cbt_600n.png",
  "cbt_700n.png",
  "cbt_800n.png",
  "wpn_900n.png",
  "wpn_1000n.png",
  "itm_1200n.png",
  "itm_1300n.png",
  "itm_1400n.png",
  "itm_1500n.png",
  "icon_borders.png",
  "icon_corners.png",
  "icon_spells.png"
];

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function writeJson(filePath, value) {
  fs.writeFileSync(filePath, JSON.stringify(value, null, 2), "utf8");
}

function publishSnapshot() {
  const backupRoot = `${FINAL_OUT_ROOT}.backup-${process.pid}`;
  fs.rmSync(backupRoot, { recursive: true, force: true });
  let movedCurrent = false;
  try {
    if (fs.existsSync(FINAL_OUT_ROOT)) {
      fs.renameSync(FINAL_OUT_ROOT, backupRoot);
      movedCurrent = true;
    }
    fs.renameSync(OUT_ROOT, FINAL_OUT_ROOT);
    fs.rmSync(backupRoot, { recursive: true, force: true });
  } catch (error) {
    if (movedCurrent && !fs.existsSync(FINAL_OUT_ROOT) && fs.existsSync(backupRoot)) {
      fs.renameSync(backupRoot, FINAL_OUT_ROOT);
    }
    throw error;
  }
}

function slugify(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
}

function parseVersion(charplanJs) {
  const match = charplanJs.match(/var v = "([^"]+)"/);
  return match ? match[1] : null;
}

async function fetchText(asset) {
  const response = await fetch(new URL(asset.url, BASE_URL), {
    headers: {
      "user-agent": "Mozilla/5.0"
    }
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch ${asset.url}: ${response.status} ${response.statusText}`);
  }

  return await response.text();
}

async function fetchBinary(urlPath) {
  const response = await fetch(new URL(urlPath, BASE_URL), {
    headers: {
      "user-agent": "Mozilla/5.0"
    }
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch ${urlPath}: ${response.status} ${response.statusText}`);
  }

  return Buffer.from(await response.arrayBuffer());
}

function parseIconsTxt(content) {
  const [baseRaw = "", spellsRaw = "", stylesRaw = ""] = content.split("|");
  const icons = [];
  const spells = [];
  const styles = [];

  // Eden uses line numbers as IDs. Empty lines are intentional placeholders,
  // so removing them shifts every mapping that follows the gap.
  const baseLines = baseRaw.split(/\r?\n/);
  for (const [index, line] of baseLines.entries()) {
    if (!line) {
      continue;
    }

    const parts = line.split(",");
    const icon = [Number(parts[0]), 0, 0, 0, 0, 0, 0, 0, 0, 0];
    for (let i = 1; i < parts.length; i++) {
      const token = parts[i];
      const value = Number(token.slice(1)) + 1;
      switch (token.charAt(0)) {
        case "B": icon[1] = value; break;
        case "S": icon[9] = value; break;
        case "0": icon[2] = value; break;
        case "1": icon[3] = value; break;
        case "2": icon[4] = value; break;
        case "3": icon[5] = value; break;
        case "4": icon[6] = value; break;
        case "5": icon[7] = value; break;
        case "6": icon[8] = value; break;
      }
    }
    icons[index + 1] = icon;
  }

  for (const [index, line] of spellsRaw.split(/\r?\n/).entries()) {
    if (!line) {
      continue;
    }

    spells[index + 1] = Number(line);
  }

  for (const [index, line] of stylesRaw.split(/\r?\n/).entries()) {
    if (!line) {
      continue;
    }

    styles[index + 1] = Number(line);
  }

  return { icons, spells, styles };
}

function resolveSpriteSheetName(spriteClass, isNight = false) {
  const suffix = isNight ? "n" : "";
  if (spriteClass <= 400) {
    return `spl_${spriteClass}${suffix}.png`;
  }

  if (spriteClass >= 500 && spriteClass <= 800) {
    return `cbt_${spriteClass}${suffix}.png`;
  }

  if (spriteClass === 900 || spriteClass === 1000) {
    return `wpn_${spriteClass}${suffix}.png`;
  }

  if (spriteClass >= 1200 && spriteClass <= 1500) {
    return `itm_${spriteClass}${suffix}.png`;
  }

  return null;
}

function buildIconReference(skill, iconMaps) {
  let resolvedIcon = null;
  let mapType = null;

  if (skill.skillType === 2 && skill.icon && iconMaps.spells[skill.icon] && iconMaps.icons[iconMaps.spells[skill.icon]]) {
    mapType = "spell";
    resolvedIcon = iconMaps.icons[iconMaps.spells[skill.icon]];
  } else if (skill.skillType === 3 && skill.icon && iconMaps.styles[skill.icon] && iconMaps.icons[iconMaps.styles[skill.icon]]) {
    mapType = "style";
    resolvedIcon = iconMaps.icons[iconMaps.styles[skill.icon]];
  } else if (skill.icon && Object.prototype.hasOwnProperty.call(skill, "costScheme") && iconMaps.icons[skill.icon]) {
    mapType = "direct";
    resolvedIcon = iconMaps.icons[skill.icon];
  }

  if (!resolvedIcon) {
    return null;
  }

  const baseSpriteIndex = resolvedIcon[0];
  const spriteClass = Math.floor(baseSpriteIndex / 100) * 100;
  const spriteCell = baseSpriteIndex % 100;
  const spriteX = spriteCell % 10;
  const spriteY = (spriteCell - spriteX) / 10;

  return {
    mappingType: mapType,
    requestedIconId: skill.icon || 0,
    resolvedSpriteIndex: baseSpriteIndex,
    spriteClass,
    spriteSheet: resolveSpriteSheetName(spriteClass),
    spriteSheetNight: resolveSpriteSheetName(spriteClass, true),
    sprite: {
      x: spriteX,
      y: spriteY,
      width: 32,
      height: 32
    },
    overlays: {
      border: resolvedIcon[1],
      corners: {
        upLeft: resolvedIcon[2],
        up: resolvedIcon[3],
        upRight: resolvedIcon[4],
        right: resolvedIcon[5],
        downRight: resolvedIcon[6],
        down: resolvedIcon[7],
        left: resolvedIcon[8]
      },
      spellBadge: resolvedIcon[9]
    }
  };
}

function normalizeAttributes(attributes) {
  return (attributes || []).map(([name, value]) => ({ name, value }));
}

function normalizeSkill(skill, iconMaps) {
  return {
    id: skill.id,
    name: skill.name,
    level: skill.level,
    skillType: skill.skillType,
    requirement: skill.requirement || null,
    iconId: skill.icon || 0,
    attributes: normalizeAttributes(skill.attributes),
    icon: buildIconReference(skill, iconMaps),
    subSkills: (skill.subSkills || []).map(subSkill => normalizeSkill(subSkill, iconMaps))
  };
}

function flattenSkill(skill, parentPath, bucket) {
  bucket.push({
    id: skill.id,
    name: skill.name,
    level: skill.level,
    skillType: skill.skillType,
    parentPath
  });

  for (const subSkill of skill.subSkills || []) {
    flattenSkill(subSkill, [...parentPath, skill.name], bucket);
  }
}

function buildClassRecord(classEntry, classMeta, iconMaps) {
  const specs = (classEntry.specs || []).map(spec => ({
    id: spec.id,
    name: spec.name,
    base: Boolean(spec.base),
    autotrain: Boolean(spec.autotrain),
    skillGroups: (spec.skillGroups || []).map(group => ({
      name: group.name,
      skills: (group.skills || []).map(skill => normalizeSkill(skill, iconMaps))
    })),
    spellLines: (spec.spellLines || []).map(line => ({
      id: line.id,
      name: line.name,
      base: Boolean(line.base),
      level: line.level,
      spellGroups: (line.spellGroups || []).map(group => ({
        name: group.name,
        skills: (group.skills || []).map(skill => normalizeSkill(skill, iconMaps))
      }))
    }))
  }));

  const flatSkills = [];
  for (const spec of specs) {
    for (const group of spec.skillGroups) {
      for (const skill of group.skills) {
        flattenSkill(skill, [classEntry.name, spec.name, group.name], flatSkills);
      }
    }
    for (const line of spec.spellLines) {
      for (const group of line.spellGroups) {
        for (const skill of group.skills) {
          flattenSkill(skill, [classEntry.name, spec.name, line.name, group.name], flatSkills);
        }
      }
    }
  }

  return {
    id: classEntry.id,
    name: classEntry.name,
    slug: slugify(classEntry.name),
    realm: classMeta?.realm ?? null,
    races: classMeta?.races ?? [],
    armor: classMeta?.armor ?? null,
    classGroups: classMeta?.cls ?? [],
    weapons: classMeta?.weapons ?? {},
    specPointMultiplier: classEntry.specPointMultiplier,
    realmAbilities: (classEntry.realmAbilities || []).map(ra => ({
      id: ra.id,
      name: ra.name,
      description: ra.description,
      costScheme: ra.costScheme,
      iconId: ra.icon || 0,
      icon: buildIconReference({ ...ra, skillType: 0 }, iconMaps),
      levels: (ra.levels || []).map(level => ({
        cost: level.cost,
        shortInfo: level.shortInfo,
        cooldown: level.cooldown || null,
        value: Object.prototype.hasOwnProperty.call(level, "value") ? level.value : null
      }))
    })),
    specs,
    flattenedSkills: flatSkills
  };
}

async function main() {
  fs.rmSync(OUT_ROOT, { recursive: true, force: true });
  ensureDir(RAW_DIR);
  ensureDir(GENERATED_DIR);
  ensureDir(CLASS_DIR);
  ensureDir(SPRITE_DIR);

  const raw = {};
  const charplanScript = await fetchText(TEXT_ASSETS[0]);
  raw[TEXT_ASSETS[0].relPath] = charplanScript;
  fs.writeFileSync(path.join(RAW_DIR, TEXT_ASSETS[0].relPath), charplanScript, "utf8");

  const version = parseVersion(charplanScript);
  if (!version) {
    throw new Error("Could not determine the Eden charplan version.");
  }

  for (const asset of TEXT_ASSETS.slice(1)) {
    const requestAsset = asset.versioned
      ? { ...asset, url: `${asset.url}?v=${encodeURIComponent(version)}` }
      : asset;
    const content = await fetchText(requestAsset);
    raw[asset.relPath] = content;
    fs.writeFileSync(path.join(RAW_DIR, asset.relPath), content, "utf8");
  }

  for (const spriteName of SPRITE_ASSETS) {
    const buffer = await fetchBinary(`/itm/icon/${spriteName}`);
    fs.writeFileSync(path.join(SPRITE_DIR, spriteName), buffer);
  }

  const daoc = JSON.parse(raw["daoc.json"]);
  const charplan = JSON.parse(raw["charplan.json"]);
  const charplanCl = JSON.parse(raw["charplan_cl.json"]);
  const iconMaps = parseIconsTxt(raw["icons.txt"]);

  const supportedClasses = charplan.filter(entry => entry.name !== "Mauler");
  const classes = supportedClasses.map(entry =>
    buildClassRecord(entry, daoc.classes?.[entry.name] || null, iconMaps)
  );

  if (classes.length < 40 || iconMaps.icons.length < 1000 || iconMaps.spells.length < 1000) {
    throw new Error(`Eden snapshot validation failed (classes=${classes.length}, icons=${iconMaps.icons.length}, spells=${iconMaps.spells.length}).`);
  }

  const classIndex = classes.map(cls => ({
    id: cls.id,
    name: cls.name,
    slug: cls.slug,
    realm: cls.realm,
    races: cls.races,
    specPointMultiplier: cls.specPointMultiplier,
    realmAbilityCount: cls.realmAbilities.length,
    specCount: cls.specs.length,
    skillCount: cls.flattenedSkills.length
  }));

  const allRealmAbilities = [];
  const allFlattenedSkills = [];
  for (const cls of classes) {
    for (const ra of cls.realmAbilities) {
      allRealmAbilities.push({
        className: cls.name,
        classSlug: cls.slug,
        ...ra
      });
    }
    for (const skill of cls.flattenedSkills) {
      allFlattenedSkills.push({
        className: cls.name,
        classSlug: cls.slug,
        ...skill
      });
    }
  }

  for (const cls of classes) {
    writeJson(path.join(CLASS_DIR, `${cls.slug}.json`), cls);
  }

  const uniqueIconConfigs = new Map();
  const collectIcon = icon => {
    if (!icon) {
      return;
    }
    const key = JSON.stringify(icon);
    if (!uniqueIconConfigs.has(key)) {
      uniqueIconConfigs.set(key, icon);
    }
  };

  for (const cls of classes) {
    for (const ra of cls.realmAbilities) {
      collectIcon(ra.icon);
    }
    for (const spec of cls.specs) {
      for (const group of spec.skillGroups) {
        for (const skill of group.skills) {
          collectSkillIcons(skill, collectIcon);
        }
      }
      for (const line of spec.spellLines) {
        for (const group of line.spellGroups) {
          for (const skill of group.skills) {
            collectSkillIcons(skill, collectIcon);
          }
        }
      }
    }
  }

  writeJson(path.join(GENERATED_DIR, "manifest.json"), {
    source: BASE_URL,
    version,
    generatedAt: new Date().toISOString(),
    classCount: classes.length,
    charplanClassCount: charplan.length,
    ignoredClasses: ["Mauler"],
    charplanClCount: charplanCl.length,
    uniqueIconConfigCount: uniqueIconConfigs.size,
    files: {
      raw: TEXT_ASSETS.map(x => `raw/${x.relPath}`),
      classIndex: "generated/classes/index.json",
      realmAbilities: "generated/realm-abilities.json",
      flattenedSkills: "generated/skills-flat.json",
      iconConfigs: "generated/icon-configs.json",
      spriteDir: "assets/sprites"
    }
  });

  writeJson(path.join(CLASS_DIR, "index.json"), classIndex);
  writeJson(path.join(GENERATED_DIR, "realm-abilities.json"), allRealmAbilities);
  writeJson(path.join(GENERATED_DIR, "skills-flat.json"), allFlattenedSkills);
  writeJson(path.join(GENERATED_DIR, "icon-configs.json"), Array.from(uniqueIconConfigs.values()));
  writeJson(path.join(GENERATED_DIR, "charplan-cl.json"), charplanCl);

  publishSnapshot();

  console.log(`Saved Eden charplan dataset to ${FINAL_OUT_ROOT}`);
  console.log(`Classes: ${classes.length}`);
  console.log(`Realm abilities: ${allRealmAbilities.length}`);
  console.log(`Flattened skills: ${allFlattenedSkills.length}`);
  console.log(`Unique icon configs: ${uniqueIconConfigs.size}`);
}

function collectSkillIcons(skill, collectIcon) {
  collectIcon(skill.icon);
  for (const subSkill of skill.subSkills || []) {
    collectSkillIcons(subSkill, collectIcon);
  }
}

main().catch(err => {
  fs.rmSync(OUT_ROOT, { recursive: true, force: true });
  console.error(err);
  process.exitCode = 1;
});
