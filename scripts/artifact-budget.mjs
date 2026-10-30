#!/usr/bin/env node
/**
 * Refuse to publish an artifact that one run cannot afford.
 *
 * Artifact storage is a single meter for the whole account, not a per-repository
 * one: 500 MB of included storage per month on the Free plan, which is those
 * 500 MiB held for 730 hours — 356 GB-hr, the unit the billing page reports and
 * the one this script projects. Once that is
 * spent, every repository's uploads are refused with "Failed to CreateArtifact:
 * Artifact storage quota has been hit" until the cycle resets, no matter how
 * little storage that repository is using — which is how it arrived here: the
 * refusals that started this work hit OmniRetail (runs 40434343214 and
 * 40889906916) while it held 0.23 MB of artifacts, and IBEBarcodeGenerator's
 * desktop build (436.8 MB in a single run) is what spent the meter.
 *
 * This repository's own uploads are the small side of that:
 *
 *   - `build.yml` uploads a Release APK on every push and pull request: 51.8 MB,
 *     held 14 days, which is 17.4 GB-hr — roughly 5% of the month per run.
 *   - `release.yml` uploads one file per Android flavor plus the `.ipa`:
 *     178.9 MB for a release, held 7 days, under a tenth of the month each
 *     time.
 *
 * Five scanner runs around 1-3 October put about 0.33 GB in flight alongside the
 * generator's 1.31 GB, and everything expired on the 8-10 October seven-day
 * boundary — after which there was nothing left to delete and the month's
 * metered allowance was still spent. Pruning cannot refund that, so the only
 * useful place to stand is *before* an upload, and the same rule is applied here
 * as in the sibling repositories:
 *
 *   - A fifth of the month (100 MiB) is what one run may spend on evidence. The
 *     APK on every push fits inside that with room, so a normal build is not
 *     asked to change anything; a push that blows past it is a build that
 *     started packing something unexpected, and it says so instead of quietly
 *     taking the meter.
 *   - A release is the run whose files ARE the product, so each flavor gets
 *     200 MiB — enough that the budget never argues with a release, tight enough
 *     that one flavor ballooning is caught, and the cost line prints what the
 *     whole thing takes out of the month either way.
 *
 * This measures, it does not estimate: every byte of every matched file is
 * summed, directories are walked, and overlapping patterns are counted once.
 *
 *     node scripts/artifact-budget.mjs [--budget-mb N] [--retention-days N]
 *                                      [--allowance-mb N] [--label NAME]
 *                                      [--github-output FILE] PATH...
 *
 * `--budget-mb` is the per-run cap (default 100 MiB: a fifth of the month, so
 * five such runs still fit). `--retention-days` is how long the upload is held
 * (default 7 — what every upload in this repository asks for) and only feeds the
 * cost projection. `--allowance-mb` is the account's included monthly storage
 * (default 500, the Free plan figure). `--github-output` appends
 * `over-budget=true|false` for a workflow step to read; it defaults to
 * `$GITHUB_OUTPUT` when that is set, so wiring the skip needs no extra flag.
 *
 * Exit codes: 0 inside the budget, 1 over it, 2 a usage error. Missing paths are
 * not an error — an artifact that found nothing is a warning at upload time, not
 * a budget problem — but they are named, so a mistyped path cannot read as a
 * cheap artifact.
 */
import fs from "node:fs";
import path from "node:path";

const HOURS_PER_MONTH = 730;
const MIB = 1024 * 1024;

const USAGE =
  "usage: node scripts/artifact-budget.mjs [--budget-mb N] [--retention-days N]\n" +
  "                                       [--allowance-mb N] [--label NAME]\n" +
  "                                       [--github-output FILE] PATH...";

function usage(message) {
  process.stderr.write(`artifact-budget: ${message}\n${USAGE}\n`);
  process.exit(2);
}

function number(flag, raw) {
  const n = Number(raw);
  if (!Number.isFinite(n) || n <= 0) usage(`${flag} needs a positive number, got ${JSON.stringify(raw)}`);
  return n;
}

function parseArgs(argv) {
  const opts = {
    budgetMb: 100,
    retentionDays: 7,
    allowanceMb: 500,
    label: null,
    githubOutput: process.env.GITHUB_OUTPUT || null,
    paths: [],
  };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    const take = (flag) => {
      const value = argv[i + 1];
      if (value === undefined || value.startsWith("--")) usage(`${flag} needs a value`);
      i += 1;
      return value;
    };
    if (arg === "--budget-mb") opts.budgetMb = number(arg, take(arg));
    else if (arg === "--retention-days") opts.retentionDays = number(arg, take(arg));
    else if (arg === "--allowance-mb") opts.allowanceMb = number(arg, take(arg));
    else if (arg === "--label") opts.label = take(arg);
    else if (arg === "--github-output") opts.githubOutput = take(arg);
    else if (arg === "--help" || arg === "-h") {
      process.stdout.write(`${USAGE}\n`);
      process.exit(0);
    } else if (arg.startsWith("--")) usage(`unknown option ${arg}`);
    else opts.paths.push(arg);
  }
  if (opts.paths.length === 0) usage("no paths given, so there is nothing to measure");
  if (opts.label === "") usage("--label needs a name");
  return opts;
}

/** true when the pattern needs expanding; plain paths are stat'ed directly. */
function isPattern(p) {
  return /[*?]/.test(p);
}

/** Turn a glob into a regex: `**` spans separators, `*` stops at one. */
function globToRegExp(pattern) {
  const normalised = pattern.replace(/^\.\//, "").replace(/\/+$/, "");
  let out = "";
  for (let i = 0; i < normalised.length; i += 1) {
    const ch = normalised[i];
    if (ch === "*") {
      if (normalised[i + 1] === "*") {
        // `a/**/b` also matches `a/b`, so swallow the separator after `**`.
        i += 1;
        if (normalised[i + 1] === "/") {
          i += 1;
          out += "(?:.*/)?";
        } else out += ".*";
      } else out += "[^/]*";
    } else if (ch === "?") out += "[^/]";
    else if ("\\^$+|()[]{}".includes(ch)) out += `\\${ch}`;
    else out += ch;
  }
  return new RegExp(`^${out}$`);
}

/** The longest directory before the first wildcard: where the walk starts. */
function walkRoot(pattern) {
  const parts = pattern.replace(/^\.\//, "").split("/");
  const fixed = [];
  for (const part of parts) {
    if (isPattern(part)) break;
    fixed.push(part);
  }
  if (fixed.length === parts.length) fixed.pop(); // the last part is the file itself
  return fixed.length ? fixed.join("/") : ".";
}

function* walk(dir) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    // Symlinks are counted as themselves and never followed: a link back up the
    // tree would otherwise make the walk unbounded.
    if (entry.isDirectory()) yield* walk(full);
    else if (entry.isFile()) yield full;
  }
}

/**
 * Every file a path argument names, keyed by device+inode so that `dist` and
 * `dist/**` together cannot count the same bytes twice.
 */
function resolve(paths) {
  const files = new Map();
  const missing = [];
  for (const pattern of paths) {
    if (!isPattern(pattern)) {
      let stat = null;
      try {
        stat = fs.lstatSync(pattern);
      } catch {
        missing.push(pattern);
        continue;
      }
      if (stat.isDirectory()) {
        for (const file of walk(pattern)) add(file);
      } else if (stat.isFile()) add(pattern);
      else missing.push(pattern); // a symlink or a device: not bytes we upload
      continue;
    }
    const re = globToRegExp(pattern);
    let matched = false;
    for (const file of walk(walkRoot(pattern))) {
      if (!re.test(file.split(path.sep).join("/"))) continue;
      matched = true;
      add(file);
    }
    if (!matched) missing.push(pattern);
  }

  function add(file) {
    let stat;
    try {
      stat = fs.statSync(file);
    } catch {
      return;
    }
    const key = `${stat.dev}:${stat.ino}`;
    if (!files.has(key)) files.set(key, { file, size: stat.size });
  }

  return { files: [...files.values()], missing };
}

function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < MIB) return `${(bytes / 1024).toFixed(1)} KiB`;
  return `${(bytes / MIB).toFixed(1)} MiB`;
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  const { files, missing } = resolve(opts.paths);
  const total = files.reduce((sum, f) => sum + f.size, 0);

  const budgetBytes = opts.budgetMb * MIB;
  const allowanceBytes = opts.allowanceMb * MIB;
  const over = total > budgetBytes;
  const share = allowanceBytes === 0 ? 0 : (total / allowanceBytes) * 100;
  const gbHours = (total / 1024 ** 3) * opts.retentionDays * 24;
  const allowanceGbHours = (opts.allowanceMb / 1024) * HOURS_PER_MONTH;

  const label = opts.label ? ` ${opts.label}` : "";
  const lines = [
    `Artifact${label}: ${files.length} file${files.length === 1 ? "" : "s"}, held ${opts.retentionDays} day${opts.retentionDays === 1 ? "" : "s"}`,
  ];
  for (const f of files.sort((a, b) => b.size - a.size).slice(0, 10)) {
    lines.push(`  ${formatBytes(f.size).padStart(10)}  ${f.file}`);
  }
  if (files.length > 10) lines.push(`  ... ${files.length - 10} more`);
  for (const m of missing) lines.push(`  not found: ${m}`);
  lines.push(
    `  total ${formatBytes(total)} of the ${opts.budgetMb.toFixed(1)} MiB run budget ` +
      `(${budgetBytes === 0 ? 0 : ((total / budgetBytes) * 100).toFixed(1)}%) — ` +
      `${share.toFixed(2)}% of the account's ${opts.allowanceMb.toFixed(0)} MiB monthly allowance`,
    `  cost ${gbHours.toFixed(2)} GB-hr at ${opts.retentionDays} days = ` +
      `${((gbHours / allowanceGbHours) * 100).toFixed(1)}% of the ${allowanceGbHours.toFixed(1)} GB-hr the monthly allowance buys`,
  );
  process.stdout.write(`${lines.join("\n")}\n`);

  if (opts.githubOutput) {
    try {
      fs.appendFileSync(opts.githubOutput, `over-budget=${over}\n`);
    } catch (error) {
      process.stderr.write(`artifact-budget: could not write ${opts.githubOutput}: ${error.message}\n`);
    }
  }

  if (!over) {
    process.stdout.write(`  ok: inside the budget\n`);
    process.exit(0);
  }
  // The overage matters in both directions: what this run cannot afford, and
  // what the meter it is drawing on has left. Saying only the first invites
  // "raise the budget", which is how the allowance was spent to begin with.
  process.stderr.write(
    `  FAIL: ${formatBytes(total)} is ${formatBytes(total - budgetBytes)} over the ` +
      `${opts.budgetMb.toFixed(1)} MiB budget for one run ` +
      `(${((total / budgetBytes) * 100).toFixed(0)}% of it).\n`,
  );
  process.stderr.write(
    `  Artifact storage is one 500 MB meter for the whole account and this run would ` +
      `spend ${share.toFixed(1)}% of the month's included storage for ${opts.retentionDays} days.\n` +
      `  Upload less (a smaller path, or a release asset, which is not metered this way),\n` +
      `  shorten --retention-days, or say why this artifact is worth it with --budget-mb.\n`,
  );
  process.exit(1);
}

main();
