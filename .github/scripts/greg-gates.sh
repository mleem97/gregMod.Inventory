#!/bin/bash
# greg quality gates: dev -> $STAGE (env STAGE, KIND, NEEDS_REFS).
# Writes gate-report.txt, sets verdict= / sha= in $GITHUB_OUTPUT.
set -u
STAGE="${STAGE:?}"; KIND="${KIND:?}"; NEEDS_REFS="${NEEDS_REFS:-false}"
git fetch origin "$STAGE" --depth=100 >/dev/null 2>&1 || git fetch origin "$STAGE" >/dev/null 2>&1 || true
git fetch origin --tags --force >/dev/null 2>&1 || true
SHA="$(git rev-parse HEAD)"
: > gate-report.txt
GATES_OK=true
report() { echo "$1" | tee -a gate-report.txt; }
gfail() { GATES_OK=false; report "GATE $1: FAIL - $2"; }
gpass() { report "GATE $1: PASS - $2"; }
gna()   { report "GATE $1: N/A (pass, annotated) - $2"; }
report "# greg gates: dev -> $STAGE @ $SHA (kind=$KIND)"

CHANGED="$(git diff --name-only "origin/$STAGE...HEAD" 2>/dev/null | sort -u || true)"

# ---------- build ----------
BUILD_RC=1; WARN_DEV=0; SOL=""
build_dotnet() {
  # NOTE: must run in the CURRENT shell (not $()) — it sets SOL/BUILD_RC/WARN_DEV
  # and calls gpass/gfail directly.
  if [ "$NEEDS_REFS" = "true" ]; then
    mkdir -p references
    cp -n /opt/greg-refs/net6/*.dll references/ 2>/dev/null || true
    cp -n /opt/greg-refs/Il2CppAssemblies/*.dll references/ 2>/dev/null || true
    if [ -x scripts/setup-dev.py ]; then
      python3 scripts/setup-dev.py --game-dir /opt/greg-refs         --interop-dir /opt/greg-refs/Il2CppAssemblies --copy-interop >setup-dev.log 2>&1         || { gfail build "scripts/setup-dev.py failed (see setup-dev.log)"; return 1; }
    fi
  fi
  SOL="$(find . -maxdepth 3 -name '*.sln' -not -path '*/obj/*' -not -path '*/bin/*' -not -path './.git/*' 2>/dev/null | head -n 1)"
  [ -z "$SOL" ] && SOL="$(find . -maxdepth 3 -name '*.csproj' -not -path '*/obj/*' -not -path '*/bin/*' -not -path './.git/*' -not -name '*Tests*' 2>/dev/null | head -n 1)"
  if [ -z "$SOL" ]; then gfail build "no solution/project found"; return 2; fi
  dotnet build "$SOL" -c Release --nologo -v:m >build-dev.log 2>&1; BUILD_RC=$?
  WARN_DEV=$(grep -c "warning CS" build-dev.log || true)
  if [ "$BUILD_RC" -eq 0 ]; then gpass build "$SOL ok, warnings=$WARN_DEV";
  else gfail build "$SOL rc=$BUILD_RC (see build-dev.log)"; fi
}
case "$KIND" in
  dotnet*)
    SOL=""; BUILD_RC=1; WARN_DEV=0
    build_dotnet
    ;;
  rust)
    if [ -f Cargo.toml ]; then
      cargo build --release >build-dev.log 2>&1; BUILD_RC=$?
      [ "$BUILD_RC" -eq 0 ] && gpass build "cargo build --release ok" || gfail build "cargo build failed (see build-dev.log)"
    else gfail build "no Cargo.toml"; fi
    ;;
  node)
    if [ -f package.json ]; then
      if [ -f pnpm-lock.yaml ]; then PM=pnpm; else PM=npm; fi
      $PM install --frozen-lockfile >build-dev.log 2>&1 || $PM install >>build-dev.log 2>&1
      if node -e "const p=require('./package.json');process.exit(p.scripts&&p.scripts.build?0:1)"; then
        $PM run build >>build-dev.log 2>&1; BUILD_RC=$?
        [ "$BUILD_RC" -eq 0 ] && gpass build "$PM run build ok" || gfail build "$PM run build failed"
      else gna build "no build script (lint/typecheck only)"; BUILD_RC=0; fi
    else gna build "no package.json"; BUILD_RC=0; fi
    ;;
  *) gna build "generic repo: nothing to compile"; BUILD_RC=0 ;;
esac

# ---------- tests ----------
TESTS_RC=1; HAVE_TESTS=false
case "$KIND" in
  dotnet*)
    TP="$(find . -path ./obj -prune -o -path ./.git -prune -o -name '*Tests*.csproj' -print 2>/dev/null | grep -v -E '/(obj|bin)/' | head -n 20)"
    if [ -z "$TP" ]; then gfail tests "no *Tests*.csproj found (add tests to unblock staging)";
    else
      HAVE_TESTS=true
      # coverage-collecting run; results reused by coverage gate
      dotnet test $TP -c Release --no-build --collect:"XPlat Code Coverage" --results-directory "$PWD/cov-dev" >test-dev.log 2>&1; TESTS_RC=$?
      [ "$TESTS_RC" -eq 0 ] && gpass tests "dotnet test ok" || gfail tests "dotnet test failed (see test-dev.log)"
    fi
    ;;
  rust)
    if [ -f Cargo.toml ]; then HAVE_TESTS=true
      cargo test >test-dev.log 2>&1; TESTS_RC=$?
      [ "$TESTS_RC" -eq 0 ] && gpass tests "cargo test ok" || gfail tests "cargo test failed"
    else gfail tests "no Cargo.toml"; fi
    ;;
  node)
    if [ -f package.json ] && node -e "const p=require('./package.json');process.exit(p.scripts&&p.scripts.test?0:1)"; then
      HAVE_TESTS=true
      if [ -f pnpm-lock.yaml ]; then pnpm test >test-dev.log 2>&1; TESTS_RC=$?
      else npm test >test-dev.log 2>&1; TESTS_RC=$?; fi
      [ "$TESTS_RC" -eq 0 ] && gpass tests "npm/pnpm test ok" || gfail tests "tests failed"
    else gna tests "no test script (annotated)"; TESTS_RC=0; fi
    ;;
  *) gna tests "generic repo (annotated)"; TESTS_RC=0 ;;
esac

# ---------- coverage (>=90%%, drop <=5pp vs STAGE) ----------
cov_dotnet() { # $1 = results dir -> prints pct or empty
  f="$(find "$1" -name 'coverage.cobertura.xml' 2>/dev/null | head -n 1)"
  [ -z "$f" ] && echo "" && return
  python3 -c "import xml.etree.ElementTree as ET,sys; print(round(float(ET.parse('$f').getroot().get('line-rate'))*100, 2))"
}
if [ "$HAVE_TESTS" = true ] && [ "$TESTS_RC" -eq 0 ]; then
  case "$KIND" in
    dotnet*)
      DEV_COV="$(cov_dotnet "$PWD/cov-dev")"
      if [ -z "$DEV_COV" ]; then gfail coverage "no cobertura output (add coverlet.collector to test projects)";
      else
        rm -rf /tmp/gbase && git worktree add --detach /tmp/gbase "origin/$STAGE" >/dev/null 2>&1
        (cd /tmp/gbase && dotnet test $TP -c Release --collect:"XPlat Code Coverage" --results-directory /tmp/gbase/cov-base >/tmp/base-test.log 2>&1 || true)
        BASE_COV="$(cov_dotnet /tmp/gbase/cov-base)"; [ -z "$BASE_COV" ] && BASE_COV=0
        git worktree remove --force /tmp/gbase >/dev/null 2>&1 || true
        DROP="$(python3 -c "print(round($BASE_COV - $DEV_COV, 2))")"
        if python3 -c "import sys; sys.exit(0 if ($DEV_COV >= 90 and ($BASE_COV - $DEV_COV) <= 5) else 1)"; then
          gpass coverage "dev=${DEV_COV}% base=${BASE_COV}% drop=${DROP}pp"
        else gfail coverage "need >=90%% and drop<=5pp (dev=${DEV_COV}% base=${BASE_COV}%)"; fi
      fi
      ;;
    rust)
      if command -v cargo-tarpaulin >/dev/null; then
        DEV_COV="$(cargo tarpaulin --out Xml --output-dir /tmp 2>/dev/null | grep -oE '[0-9]+\.[0-9]+%' | tail -n 1 | tr -d '%')"
        rm -rf /tmp/gbase && git worktree add --detach /tmp/gbase "origin/$STAGE" >/dev/null 2>&1
        BASE_COV="$(cd /tmp/gbase && cargo tarpaulin --out Xml --output-dir /tmp 2>/dev/null | grep -oE '[0-9]+\.[0-9]+%' | tail -n 1 | tr -d '%')"
        git worktree remove --force /tmp/gbase >/dev/null 2>&1 || true
        DEV_COV="${DEV_COV:-0}"; BASE_COV="${BASE_COV:-0}"
        if python3 -c "import sys; sys.exit(0 if ($DEV_COV >= 90 and ($BASE_COV - $DEV_COV) <= 5) else 1)"; then
          gpass coverage "dev=${DEV_COV}% base=${BASE_COV}%"
        else gfail coverage "need >=90%% and drop<=5pp (dev=${DEV_COV}% base=${BASE_COV}%)"; fi
      else gfail coverage "cargo-tarpaulin missing"; fi
      ;;
    *) gna coverage "not measurable for kind=$KIND (annotated)" ;;
  esac
else
  case "$KIND" in dotnet*|rust) gfail coverage "no passing tests to measure (fail closed)";; *) gna coverage "no test harness (annotated)";; esac
fi

# ---------- security: 0 new issues ----------
if command -v gitleaks >/dev/null; then
  if gitleaks detect --source . --no-git -v >gitleaks.log 2>&1; then gpass security-secrets "gitleaks: 0 findings";
  else gfail security-secrets "gitleaks findings (see gitleaks.log)"; fi
else gfail security-secrets "gitleaks not installed on runner"; fi
case "$KIND" in
  dotnet*)
    if [ -n "$SOL" ]; then
      VULN=$(dotnet list "$SOL" package --vulnerable --include-transitive 2>/dev/null | grep -ciE "high|critical" || true)
      [ "$VULN" -eq 0 ] && gpass security-deps "0 high/critical vulnerable packages" || gfail security-deps "$VULN high/critical findings"
      # warnings delta vs STAGE (staging log from coverage worktree run if present)
      WARN_BASE=0
      if [ -f /tmp/base-test.log ]; then WARN_BASE=$(grep -c "warning CS" /tmp/base-test.log || true); fi
      if [ "$WARN_DEV" -le "$WARN_BASE" ] 2>/dev/null; then gpass security-warnings "warnings dev=$WARN_DEV base=$WARN_BASE (delta<=0)";
      else gfail security-warnings "new warnings: dev=$WARN_DEV base=$WARN_BASE"; fi
    else gna security-deps "no solution"; fi
    ;;
  rust)
    if [ -f Cargo.lock ]; then cargo audit >cargo-audit.log 2>&1 && gpass security-deps "cargo audit clean" || gfail security-deps "cargo audit findings";
    else gna security-deps "no Cargo.lock"; fi
    ;;
  node)
    if [ -f package-lock.json ]; then npm audit --audit-level=high >npm-audit.log 2>&1 && gpass security-deps "npm audit clean" || gfail security-deps "npm audit high+ findings";
    elif [ -f pnpm-lock.yaml ]; then pnpm audit --audit-level high >npm-audit.log 2>&1 && gpass security-deps "pnpm audit clean" || gfail security-deps "pnpm audit high+ findings";
    else gna security-deps "no lockfile"; fi
    ;;
  *) gna security-deps "generic repo" ;;
esac

# ---------- version consistency (repos with VERSION file) ----------
if [ -f VERSION ]; then
  python3 - <<'PYEOF' >>gate-report.txt 2>&1 || echo "GATE consistency: FAIL - checker error" | tee -a gate-report.txt
import re, json, subprocess, glob, sys
def sh(*a):
    try: return subprocess.run(a, capture_output=True, text=True).stdout.strip()
    except Exception: return ""
ver = open("VERSION").read().strip()
base = re.sub(r"-dev$", "", ver)
ok = True; notes = []
for cs in glob.glob("*.csproj"):
    t = open(cs).read()
    m = re.search(r"<Version>([^<]+)</Version>", t)
    if m and re.sub(r"-dev$", "", m.group(1)) != base:
        ok = False; notes.append(f"{cs} Version={m.group(1)}")
try:
    man = json.load(open("manifest.json")); mv = man.get("version", "")
    tag = sh("git", "describe", "--tags", "--abbrev=0", "--match", "v*") or "v0.0.0"
    tv = tag.lstrip("v")
    if mv not in (tv, base):
        ok = False; notes.append(f"manifest.json={mv} (expected {tv} or {base})")
except FileNotFoundError:
    notes.append("no manifest.json (skipped)")
except Exception as e:
    ok = False; notes.append(f"manifest error: {e}")
import subprocess as sp
found_melon = False
for f in sp.run(["git", "grep", "-l", "MelonInfo", "--", "src"], capture_output=True, text=True).stdout.split():
    found_melon = True
    for line in open(f):
        if "MelonInfo" in line:
            for v in re.findall(r'"([0-9]+\.[0-9]+\.[0-9]+(?:-dev)?)"', line):
                if re.sub(r"-dev$", "", v) != base:
                    ok = False; notes.append(f"{f}: MelonInfo {v}")
print(("GATE consistency: PASS - all version sources agree on " + base) if ok else ("GATE consistency: FAIL - " + "; ".join(notes) + f" (VERSION base={base})"))
sys.exit(0 if ok else 1)
PYEOF
  [ $? -eq 0 ] || GATES_OK=false
else gna consistency "no VERSION file"; fi

# ---------- conventional commits ----------
if git log --no-merges --format=%s "origin/$STAGE..HEAD" 2>/dev/null | grep -vE "^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\(.+\))?!?: .+" | grep -q .; then
  gfail conventional "non-conventional subjects:"; git log --no-merges --format=%s "origin/$STAGE..HEAD" | grep -vE "^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\(.+\))?!?: .+" | head -n 5 | tee -a gate-report.txt
else gpass conventional "all subjects conventional"; fi

# ---------- verdict ----------
if [ "$GATES_OK" = true ]; then echo "OVERALL: PASS" | tee -a gate-report.txt; echo "verdict=pass" >> "$GITHUB_OUTPUT";
else echo "OVERALL: FAIL (staging NOT updated)" | tee -a gate-report.txt; echo "verdict=fail" >> "$GITHUB_OUTPUT"; fi
echo "sha=$SHA" >> "$GITHUB_OUTPUT"
