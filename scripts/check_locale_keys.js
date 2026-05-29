#!/usr/bin/env node
const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..");
const localeDir = path.join(repoRoot, "Core", "Localization", "locales");
const enPath = path.join(localeDir, "en.json");
const scanExts = new Set([".cs", ".html", ".js"]);
const ignoreDirs = new Set([".git", "bin", "obj", "node_modules"]);
const args = new Set(process.argv.slice(2));
const scaffold = args.has("--scaffold");

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function walk(dir) {
  const files = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!ignoreDirs.has(entry.name)) {
        files.push(...walk(fullPath));
      }
      continue;
    }
    if (scanExts.has(path.extname(entry.name).toLowerCase())) {
      files.push(fullPath);
    }
  }
  return files;
}

function extractKeys(text) {
  const keys = new Map();
  const add = (key, source) => {
    if (!key || key.startsWith("_")) return;
    if (!keys.has(key)) keys.set(key, []);
    keys.get(key).push(source);
  };

  const patterns = [
    { name: "Localization.Get", regex: /Localization\.Get\(\s*["'`]([^"'`]+)["'`]\s*\)/g },
    { name: "data-i18n", regex: /data-i18n(?:-placeholder)?\s*=\s*["']([^"']+)["']/g },
    { name: "i18n lookup", regex: /i18n\[\s*["']([^"']+)["']\s*\]/g }
  ];

  for (const pattern of patterns) {
    let match;
    while ((match = pattern.regex.exec(text)) !== null) {
      add(match[1], pattern.name);
    }
  }

  return keys;
}

function flattenKeys(obj, prefix = "", out = new Set()) {
  for (const [key, value] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    out.add(fullKey);
    if (value && typeof value === "object" && !Array.isArray(value)) {
      flattenKeys(value, fullKey, out);
    }
  }
  return out;
}

function sortObjectKeys(obj) {
  return Object.keys(obj)
    .sort((a, b) => a.localeCompare(b))
    .reduce((acc, key) => {
      acc[key] = obj[key];
      return acc;
    }, {});
}

function writeJson(filePath, data) {
  fs.writeFileSync(filePath, `${JSON.stringify(data, null, 2)}\n`, "utf8");
}

function scaffoldMissingKeys(missingKeys, localeFiles) {
  if (!missingKeys.length) return [];

  const updated = [];
  const files = [enPath, ...localeFiles.map(item => item.filePath)];

  for (const filePath of files) {
    const raw = readJson(filePath);
    let changed = false;

    for (const key of missingKeys) {
      if (Object.prototype.hasOwnProperty.call(raw, key)) continue;
      raw[key] = "__MISSING__";
      changed = true;
    }

    if (changed) {
      writeJson(filePath, sortObjectKeys(raw));
      updated.push(path.relative(repoRoot, filePath));
    }
  }

  return updated;
}

function main() {
  if (!fs.existsSync(enPath)) {
    console.error(`Missing locale file: ${enPath}`);
    process.exit(1);
  }

  const enKeys = flattenKeys(readJson(enPath));
  const codeKeys = new Map();
  const files = walk(repoRoot);
  const localeFiles = [];

  for (const localeFile of fs.readdirSync(localeDir)) {
    if (!localeFile.endsWith(".json") || localeFile === "en.json") continue;
    localeFiles.push({ filePath: path.join(localeDir, localeFile), name: localeFile });
  }

  for (const filePath of files) {
    if (filePath.startsWith(localeDir)) continue;
    const text = fs.readFileSync(filePath, "utf8");
    const found = extractKeys(text);
    for (const [key, sources] of found.entries()) {
      if (!codeKeys.has(key)) codeKeys.set(key, []);
      codeKeys.get(key).push({ file: path.relative(repoRoot, filePath), sources });
    }
  }

  const missingInEn = [];
  for (const [key, usages] of codeKeys.entries()) {
    if (!enKeys.has(key)) missingInEn.push({ key, usages });
  }
  missingInEn.sort((a, b) => a.key.localeCompare(b.key));

  const unusedInCode = [...enKeys]
    .filter(key => !codeKeys.has(key))
    .sort();

  if (scaffold && missingInEn.length) {
    const updatedFiles = scaffoldMissingKeys(missingInEn.map(item => item.key), localeFiles);
    console.log("Scaffolded missing keys into:");
    for (const file of updatedFiles) {
      console.log(`- ${file}`);
    }
    console.log("");
  }

  if (missingInEn.length === 0) {
    console.log("No code keys are missing from en.json.");
  } else {
    console.log("Keys used in code but missing from en.json:");
    for (const item of missingInEn) {
      console.log(`- ${item.key}`);
      for (const usage of item.usages) {
        console.log(`  - ${usage.file} [${usage.sources.join(", ")}]`);
      }
    }
  }

  if (unusedInCode.length) {
    console.log("");
    console.log("Keys in en.json not referenced by the scanned code:");
    for (const key of unusedInCode) {
      console.log(`- ${key}`);
    }
  }

  const hadErrors = missingInEn.length > 0 || unusedInCode.length > 0;
  process.exit(hadErrors ? 1 : 0);
}

main();
