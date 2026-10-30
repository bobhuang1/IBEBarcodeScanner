#!/usr/bin/env bash
# Prove the artifact budget stops a run from spending the month's allowance.
#
# Artifact storage is one meter for the whole account: 500 MB a month, shared by
# every repository, and the account header in `scripts/artifact-budget.mjs` shows
# what spent it — a sibling repository's 436.8 MB desktop build per run, held
# seven days, which is why this repository's APK uploads stopped working. Two
# things follow, and this file pins both.
#
# The guard's own verdicts, because each has a quiet failure mode:
#   - a run under budget has to pass, or every upload gets a red check and the
#     budget becomes something people delete;
#   - a run over budget has to fail, or the guard is decoration — and it has to
#     name the overage and the share of the account allowance, because a message
#     that only says "too big" invites raising `--budget-mb` until the meter is
#     spent again;
#   - bytes are counted, not files: a directory is walked, a glob is expanded,
#     `dist` and `dist/**` do not count the same file twice, and a path that
#     matched nothing is reported rather than silently costing zero;
#   - the `over-budget` output the upload step reads must be written under both
#     verdicts, since a missing output compares as "not over" and uploads anyway.
#
# And the wiring, because a guard nobody calls is worse than none: every
# `actions/upload-artifact` step in every workflow here must be preceded by a
# step that runs this guard, must read the verdict that step writes, and must
# state a retention the guard can cost — two weeks at most, which is the 14 days
# the push-build APK already asks for. That is what keeps a new job from arriving
# with a ninety-day multi-gigabyte upload.
#
#   scripts/artifact-budget.test.sh
#
# No browser, no database, no network: fixtures are sparse files, so a 2 MiB case
# costs no disk.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GUARD="$REPO_ROOT/scripts/artifact-budget.mjs"
MIB=1048576

PASS=0
FAIL=0
ok()  { printf '  PASS  %s\n' "$1"; PASS=$((PASS + 1)); }
bad() { printf '  FAIL  %s\n' "$1"; FAIL=$((FAIL + 1)); }

if [[ ! -f "$GUARD" ]]; then
  printf '  FAIL  guard not found: %s\n' "$GUARD"
  exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Run the guard, capturing combined output and the exit status separately: the
# status is the interface, and a pipeline would lose it.
run() {
  OUT="$(node "$GUARD" "$@" 2>&1)"
  STATUS=$?
}

printf 'artifact budget\n'

# --- a run that fits -------------------------------------------------------
mkdir -p "$TMP/small"
truncate -s 4096 "$TMP/small/report.json"
run --label small --budget-mb 1 "$TMP/small/report.json"
if [[ $STATUS -eq 0 ]] && grep -q "ok: inside the budget" <<<"$OUT"; then
  ok "a 4 KiB report inside a 1 MiB budget passes"
else
  bad "a 4 KiB report inside a 1 MiB budget should pass (exit $STATUS)"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi
if grep -q "small" <<<"$OUT" && grep -q "4.0 KiB" <<<"$OUT"; then
  ok "the report names the label and the measured size"
else
  bad "the report should name the label and the measured size"
fi

# --- a run that does not ---------------------------------------------------
truncate -s $((3 * MIB)) "$TMP/big.tgz"
run --label big --budget-mb 1 "$TMP/big.tgz"
if [[ $STATUS -eq 1 ]]; then
  ok "a 3 MiB artifact over a 1 MiB budget fails"
else
  bad "a 3 MiB artifact over a 1 MiB budget should exit 1 (got $STATUS)"
fi
if grep -q "FAIL" <<<"$OUT" && grep -q "2.0 MiB over" <<<"$OUT"; then
  ok "the failure names the overage in bytes"
else
  bad "the failure should name the overage in bytes"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi
if grep -q "of the month's included storage" <<<"$OUT"; then
  ok "the failure says what it would cost the account's allowance"
else
  bad "the failure should say what it would cost the account's allowance"
fi

# --- the default budget ----------------------------------------------------
run "$TMP/small/report.json"
if [[ $STATUS -eq 0 ]] && grep -q "100.0 MiB run budget" <<<"$OUT"; then
  ok "the default per-run budget is 100 MiB"
else
  bad "the default per-run budget should be 100 MiB"
fi
truncate -s $((101 * MIB)) "$TMP/huge.bin"
run "$TMP/huge.bin"
if [[ $STATUS -eq 1 ]]; then
  ok "an upload past the default budget fails without any flag"
else
  bad "an upload past the default budget should fail (got $STATUS)"
fi

# --- what counts as bytes --------------------------------------------------
mkdir -p "$TMP/dist/nested/deeper"
truncate -s $((1 * MIB)) "$TMP/dist/a.bin"
truncate -s $((1 * MIB)) "$TMP/dist/nested/b.bin"
truncate -s $((1 * MIB)) "$TMP/dist/nested/deeper/c.bin"
run --budget-mb 4 "$TMP/dist"
if [[ $STATUS -eq 0 ]] && grep -q "total 3.0 MiB" <<<"$OUT"; then
  ok "a directory is walked, nested files included"
else
  bad "a directory should be walked recursively (exit $STATUS)"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi
run --budget-mb 4 "$TMP/dist/**/*.bin"
if [[ $STATUS -eq 0 ]] && grep -q "total 3.0 MiB" <<<"$OUT"; then
  ok "a ** glob matches across separators"
else
  bad "a ** glob should match across separators"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi
run --budget-mb 4 "$TMP/dist" "$TMP/dist/**"
if [[ $STATUS -eq 0 ]] && grep -q "total 3.0 MiB" <<<"$OUT"; then
  ok "the same file named by two patterns is counted once"
else
  bad "overlapping patterns should count a file once"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi
run --budget-mb 2 "$TMP/dist/**"
if [[ $STATUS -eq 1 ]]; then
  ok "a glob's bytes are budgeted, not just its match count"
else
  bad "a 3 MiB glob should fail a 2 MiB budget (got $STATUS)"
fi

# --- a path that found nothing ---------------------------------------------
run --budget-mb 1 "$TMP/does-not-exist.json"
if [[ $STATUS -eq 0 ]] && grep -q "not found: " <<<"$OUT"; then
  ok "a path that matched nothing is named, not silently free"
else
  bad "a missing path should be named (exit $STATUS)"
  printf '%s\n' "$OUT" | sed 's/^/        /'
fi

# --- the output the upload step reads ---------------------------------------
run --budget-mb 1 --github-output "$TMP/out-under.txt" "$TMP/small/report.json"
if [[ $STATUS -eq 0 ]] && grep -qx "over-budget=false" "$TMP/out-under.txt"; then
  ok "an upload inside budget writes over-budget=false"
else
  bad "inside budget should write over-budget=false"
fi
run --budget-mb 1 --github-output "$TMP/out-over.txt" "$TMP/big.tgz"
if [[ $STATUS -eq 1 ]] && grep -qx "over-budget=true" "$TMP/out-over.txt"; then
  ok "an upload over budget writes over-budget=true"
else
  bad "over budget should write over-budget=true"
fi

# --- the cost projection ---------------------------------------------------
run --budget-mb 500 --retention-days 7 --allowance-mb 500 "$TMP/big.tgz"
if grep -q "the account's 500 MiB monthly allowance" <<<"$OUT" && grep -q "GB-hr" <<<"$OUT"; then
  ok "the report projects the GB-hr cost against the monthly allowance"
else
  bad "the report should project GB-hr against the monthly allowance"
fi

# --- usage errors ----------------------------------------------------------
run
if [[ $STATUS -eq 2 ]]; then ok "no paths is a usage error"; else bad "no paths should exit 2 (got $STATUS)"; fi
run --budget-mb
if [[ $STATUS -eq 2 ]]; then ok "a flag without a value is a usage error"; else bad "a flag without a value should exit 2 (got $STATUS)"; fi
run --budget-mb nope "$TMP/big.tgz"
if [[ $STATUS -eq 2 ]]; then ok "a non-numeric budget is a usage error"; else bad "a non-numeric budget should exit 2 (got $STATUS)"; fi

# --- the wiring ------------------------------------------------------------
# Every upload in this repository measures itself first and expires within a
# week. Read out of the workflow YAML rather than grepped, so a step that moves
# between jobs cannot satisfy the check by accident.
python3 - "$REPO_ROOT" <<'PY'
import glob
import os
import sys
import yaml

root = sys.argv[1]
problems = []
checked = 0
for path in sorted(glob.glob(os.path.join(root, ".github/workflows/*.yml"))):
    doc = yaml.safe_load(open(path)) or {}
    for job_name, job in (doc.get("jobs") or {}).items():
        steps = job.get("steps") or []
        for index, step in enumerate(steps):
            uses = str(step.get("uses", ""))
            if "upload-artifact" not in uses:
                continue
            checked += 1
            where = f"{os.path.basename(path)}:{job_name}:{step.get('name', index)}"
            guard = steps[index - 1] if index > 0 else {}
            if "artifact-budget.mjs" not in str(guard.get("run", "")):
                problems.append(f"{where} has no artifact-budget step before it")
            else:
                budget_id = str(guard.get("id", ""))
                if not budget_id:
                    problems.append(f"{where}'s budget step has no id to read")
                elif f"steps.{budget_id}.outputs.over-budget" not in str(step.get("if", "")):
                    problems.append(f"{where} does not skip when over budget")
            retention = (step.get("with") or {}).get("retention-days")
            try:
                days = int(retention)
            except (TypeError, ValueError):
                problems.append(f"{where} has no numeric retention-days")
                continue
            if days > 14:
                problems.append(f"{where} is held {days} days, over the 14-day cap")

for problem in problems:
    print(f"  FAIL  {problem}")
if problem_count := len(problems):
    sys.exit(1)
print(f"  PASS  every upload-artifact step ({checked}) is budgeted and expires within 14 days")
PY
if [[ $? -eq 0 ]]; then
  PASS=$((PASS + 1))
else
  FAIL=$((FAIL + 1))
fi

printf '\n%d passed, %d failed\n' "$PASS" "$FAIL"
[[ $FAIL -eq 0 ]] || exit 1
