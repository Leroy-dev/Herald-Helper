const fs = require("fs");
const path = require("path");

const BASE_URL = "https://blackthorn-daoc.com";
const FINAL_OUT_ROOT = path.resolve(__dirname, "..", "data", "blackthorn-charplan");
const OUT_ROOT = `${FINAL_OUT_ROOT}.staging-${process.pid}`;
const CLASS_DIR = path.join(OUT_ROOT, "generated", "classes");
const ICON_ROOT = path.join(OUT_ROOT, "assets", "icons");

function slugify(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
}

function writeJson(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
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

async function fetchText(url) {
  const response = await fetch(url, {
    headers: { "user-agent": "HeraldHelper charplan snapshot crawler" }
  });
  if (!response.ok) {
    throw new Error(`Failed to fetch ${url}: ${response.status} ${response.statusText}`);
  }
  return await response.text();
}

async function fetchOptionalBinary(url) {
  const response = await fetch(url, {
    headers: { "user-agent": "HeraldHelper charplan snapshot crawler" }
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Failed to fetch ${url}: ${response.status} ${response.statusText}`);
  }
  return Buffer.from(await response.arrayBuffer());
}

async function discoverClassNames() {
  const html = await fetchText(`${BASE_URL}/class/Armsman`);
  const scriptMatch = html.match(/src="([^"]*\/pages\/class\/%5Bclass%5D-[^"]+\.js)"/i);
  if (!scriptMatch) {
    throw new Error("Could not find the Blackthorn class planner bundle.");
  }

  const scriptUrl = new URL(scriptMatch[1], BASE_URL);
  const script = await fetchText(scriptUrl);
  const start = script.indexOf("e[e.Armsman=");
  const end = start < 0 ? -1 : script.indexOf("}({})", start);
  if (start < 0 || end < 0) {
    throw new Error("Could not find the Blackthorn class enumeration.");
  }

  const classSection = script.slice(start, end);
  const names = Array.from(classSection.matchAll(/e\[e\.[A-Za-z]+\s*=\s*\d+\]\s*=\s*"([^"]+)"/g), match => match[1]);
  const uniqueNames = Array.from(new Set(names));
  if (uniqueNames.length < 30) {
    throw new Error(`Blackthorn class discovery returned only ${uniqueNames.length} classes.`);
  }
  return uniqueNames;
}

async function fetchClassData(className) {
  const url = `${BASE_URL}/class/${encodeURIComponent(className)}`;
  const html = await fetchText(url);
  const match = html.match(/<script id="__NEXT_DATA__" type="application\/json">([\s\S]*?)<\/script>/);
  if (!match) {
    throw new Error(`No __NEXT_DATA__ payload found for ${className}.`);
  }

  const nextData = JSON.parse(match[1]);
  const classData = nextData?.props?.pageProps?.classData;
  if (!classData || !classData.name) {
    throw new Error(`No classData payload found for ${className}.`);
  }

  return { classData, buildId: nextData.buildId || null, sourceUrl: url };
}

async function mapWithConcurrency(items, concurrency, callback) {
  const result = new Array(items.length);
  let nextIndex = 0;

  async function worker() {
    while (true) {
      const index = nextIndex++;
      if (index >= items.length) {
        return;
      }

      result[index] = await callback(items[index]);
    }
  }

  await Promise.all(Array.from({ length: concurrency }, worker));
  return result;
}

function discoverIconAssets(classDataItems) {
  const assets = new Set();
  function visit(value) {
    if (!value || typeof value !== "object") {
      return;
    }
    if ((value.objectType === "Spell" || value.objectType === "Style") &&
        Number.isInteger(value.icon?.iconLocation) && value.icon.iconLocation >= 0) {
      const spriteClass = Math.floor(value.icon.iconLocation / 100) * 100;
      const directory = value.objectType === "Spell" ? "spells" : "styles";
      const prefix = value.objectType === "Spell" ? "spl" : "cbt";
      assets.add(`${directory}/${prefix}_${spriteClass}.bmp`);
    }
    for (const child of Object.values(value)) {
      visit(child);
    }
  }
  for (const classData of classDataItems) {
    visit(classData);
  }
  return Array.from(assets).sort();
}

async function main() {
  fs.rmSync(OUT_ROOT, { recursive: true, force: true });
  fs.mkdirSync(CLASS_DIR, { recursive: true });
  const classNames = await discoverClassNames();
  const fetched = await mapWithConcurrency(classNames, 5, fetchClassData);
  const iconAssets = discoverIconAssets(fetched.map(item => item.classData));
  const downloadedIconAssets = [];
  const missingIconAssets = [];
  for (const asset of iconAssets) {
    const targetPath = path.join(ICON_ROOT, asset);
    fs.mkdirSync(path.dirname(targetPath), { recursive: true });
    const content = await fetchOptionalBinary(`${BASE_URL}/icons/${asset}`);
    if (content === null) {
      missingIconAssets.push(asset);
      continue;
    }
    if (content.length < 1000) {
      throw new Error(`Blackthorn icon asset is unexpectedly small: ${asset}`);
    }
    fs.writeFileSync(targetPath, content);
    downloadedIconAssets.push(asset);
  }
  if (downloadedIconAssets.length < 5) {
    throw new Error(`Blackthorn snapshot validation found only ${downloadedIconAssets.length} icon assets.`);
  }
  const classIndex = [];
  const buildIds = new Set();

  for (const item of fetched) {
    const classData = item.classData;
    const slug = slugify(classData.name);
    if (item.buildId) {
      buildIds.add(item.buildId);
    }

    writeJson(path.join(CLASS_DIR, `${slug}.json`), classData);
    classIndex.push({
      id: classData.classId,
      name: classData.name,
      slug,
      realm: classData.realm,
      specCount: Array.isArray(classData.specs) ? classData.specs.length : 0,
      realmAbilityCount: Array.isArray(classData.realmAbilities) ? classData.realmAbilities.length : 0,
      sourceUrl: item.sourceUrl
    });
  }

  classIndex.sort((a, b) => a.name.localeCompare(b.name));
  if (classIndex.length !== classNames.length || classIndex.some(x => !x.id || !x.name || x.specCount === 0)) {
    throw new Error("Blackthorn snapshot validation failed.");
  }
  writeJson(path.join(OUT_ROOT, "generated", "classes", "index.json"), classIndex);
  writeJson(path.join(OUT_ROOT, "generated", "manifest.json"), {
    source: BASE_URL,
    generatedAt: new Date().toISOString(),
    buildIds: Array.from(buildIds),
    classCount: classIndex.length,
    iconAssetCount: downloadedIconAssets.length,
    missingIconAssets,
    files: {
      classIndex: "generated/classes/index.json",
      classDirectory: "generated/classes",
      iconDirectory: "assets/icons"
    }
  });

  publishSnapshot();

  console.log(`Saved Blackthorn charplan dataset to ${FINAL_OUT_ROOT}`);
  console.log(`Classes: ${classIndex.length}`);
  console.log(`Icon assets: ${downloadedIconAssets.length} (${missingIconAssets.length} missing upstream)`);
}

main().catch(error => {
  fs.rmSync(OUT_ROOT, { recursive: true, force: true });
  console.error(error);
  process.exitCode = 1;
});
