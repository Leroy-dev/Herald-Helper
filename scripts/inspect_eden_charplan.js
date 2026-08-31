const fs = require("fs");
const path = require("path");

const playwright = require("../src/HeraldHelper.Desktop/bin/Debug/net10.0-windows10.0.19041.0/.playwright/package");

async function main() {
  const outDir = path.resolve(__dirname, "..", "tmp", "eden-inspect");
  fs.mkdirSync(outDir, { recursive: true });

  const browser = await playwright.chromium.launch({
    headless: true,
    args: [
      "--disable-blink-features=AutomationControlled",
      "--no-default-browser-check",
      "--no-first-run"
    ]
  });

  try {
    const page = await browser.newPage();
    await page.goto("https://eden-daoc.net/charplan?c=Armsman&l=50&r=Briton&rr=5L0&p=s", {
      waitUntil: "networkidle",
      timeout: 60000
    });

    await page.screenshot({ path: path.join(outDir, "armsman.png"), fullPage: true });
    const html = await page.content();
    fs.writeFileSync(path.join(outDir, "armsman.html"), html, "utf8");

    const summary = await page.evaluate(() => {
      const anchors = Array.from(document.querySelectorAll("a"))
        .map(a => ({
          text: (a.textContent || "").trim(),
          href: a.getAttribute("href") || ""
        }))
        .filter(x => x.text || x.href)
        .slice(0, 120);

      const selects = Array.from(document.querySelectorAll("select")).map(select => ({
        name: select.getAttribute("name") || "",
        id: select.id || "",
        options: Array.from(select.options).map(option => ({
          value: option.value,
          text: option.text.trim(),
          selected: option.selected
        }))
      }));

      const inputs = Array.from(document.querySelectorAll("input, button"))
        .map(el => ({
          tag: el.tagName,
          type: el.getAttribute("type") || "",
          name: el.getAttribute("name") || "",
          id: el.id || "",
          value: el.getAttribute("value") || "",
          text: (el.textContent || "").trim()
        }))
        .slice(0, 120);

      return {
        title: document.title,
        url: location.href,
        headingText: Array.from(document.querySelectorAll("h1,h2,h3,h4")).map(x => x.textContent.trim()),
        selects,
        inputs,
        anchors,
        bodyText: (document.body?.innerText || "").slice(0, 4000)
      };
    });

    fs.writeFileSync(path.join(outDir, "summary.json"), JSON.stringify(summary, null, 2), "utf8");
    console.log(JSON.stringify(summary, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error(err);
  process.exitCode = 1;
});
