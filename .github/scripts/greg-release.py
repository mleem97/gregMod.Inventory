#!/usr/bin/env python3
"""greg auto-release: bump (conventional commits) -> version files -> tag ->
build -> GitHub Release -> CHANGELOG -> next -dev cycle. Rerun-safe.
Env: DRY_RUN=true => compute + print only.
"""
import json, os, re, subprocess, sys, glob

DRY = os.environ.get("DRY_RUN", "false").lower() == "true"

def sh(*a, check=True):
    p = subprocess.run(a, capture_output=True, text=True)
    if check and p.returncode != 0:
        print(p.stderr[:2000]); sys.exit(f"CMD FAILED: {' '.join(a)}")
    return p.stdout.strip()

def parse(v):
    m = re.match(r"^(\d+)\.(\d+)\.(\d+)", v)
    return tuple(map(int, m.groups())) if m else (0, 0, 0)

def fmt(t): return f"{t[0]}.{t[1]}.{t[2]}"

# --- last tag ---
tags = sh("git", "tag", "--list", "v*", "--sort=-v:refname").split()
last = tags[0] if tags else "v0.0.0"
last_v = parse(last.lstrip("v"))
rng = f"{last}..HEAD" if tags else "HEAD"
msgs = sh("git", "log", "--no-merges", "--format=%s%n%b", rng).split("\n")

major = any(re.search(r"^[a-z]+(\(.+\))?!:", m) or "BREAKING CHANGE" in m for m in msgs)
minor = any(re.match(r"^feat(\(.+\))?!?:", m) for m in msgs)
patch = any(re.match(r"^(fix|perf|refactor|revert)(\(.+\))?!?:", m) for m in msgs)
if major: bump = (last_v[0] + 1, 0, 0)
elif minor: bump = (last_v[0], last_v[1] + 1, 0)
elif patch: bump = (last_v[0], last_v[1], last_v[2] + 1)
else:
    print(f"NO-RELEASE: no version-relevant commits since {last}"); return_code = 0; sys.exit(0)

# respect manually pre-set VERSION target
ver_base = "0.0.0"
if os.path.exists("VERSION"):
    ver_base = re.sub(r"-dev$", "", open("VERSION").read().strip())
new = bump if parse(ver_base) <= bump else parse(ver_base)
NV = fmt(new)
if f"v{NV}" == last or (tags and parse(NV) <= last_v):
    print(f"NO-RELEASE: {NV} already released as {last}"); sys.exit(0)
print(f"RELEASE: {last} -> v{NV} (major={major} minor={minor} patch={patch})")
if DRY:
    print(f"DRY-RUN: would release v{NV}"); sys.exit(0)

# remote tag already there? (rerun-safe)
rtags = sh("git", "ls-remote", "--tags", "origin", f"refs/tags/v{NV}")
if rtags:
    print(f"NO-RELEASE: remote tag v{NV} exists"); sys.exit(0)

# --- version files (stable, no -dev in the release commit) ---
def stable(v): return v
if os.path.exists("VERSION"):
    open("VERSION", "w").write(NV + "\n")
for cs in glob.glob("*.csproj"):
    t = open(cs).read()
    t = re.sub(r"<Version>[^<]+</Version>", f"<Version>{NV}</Version>", t)
    t = re.sub(r"<AssemblyVersion>[^<]+</AssemblyVersion>", f"<AssemblyVersion>{NV}.0</AssemblyVersion>", t)
    t = re.sub(r"<FileVersion>[^<]+</FileVersion>", f"<FileVersion>{NV}.0</FileVersion>", t)
    open(cs, "w").write(t)
if os.path.exists("manifest.json"):
    m = json.load(open("manifest.json")); m["version"] = NV
    json.dump(m, open("manifest.json", "w"), indent=4); open("manifest.json", "a").write("\n")
for f in subprocess.run(["git", "grep", "-l", "MelonInfo", "--", "src"],
                        capture_output=True, text=True).stdout.split():
    lines = open(f).read().split("\n")
    out = []
    for line in lines:
        if "MelonInfo" in line:
            line = re.sub(r'"\d+\.\d+\.\d+(?:-dev)?"', f'"{NV}"', line)
        out.append(line)
    open(f, "w").write("\n".join(out))

# --- CHANGELOG: move Unreleased content under dated version ---
if os.path.exists("CHANGELOG.md"):
    try:
        from datetime import date
        c = open("CHANGELOG.md").read()
        c = c.replace("## [Unreleased]", f"## [Unreleased]\n\n## [{NV}] - {date.today().isoformat()}", 1)
        open("CHANGELOG.md", "w").write(c)
    except Exception as e:
        print(f"CHANGELOG best-effort skipped: {e}")

sh("git", "add", "-A")
sh("git", "-c", "user.name=greg-ci", "-c", "user.email=greg-ci@users.noreply.github.com",
   "commit", "-m", f"chore(release): {NV}")
sh("git", "tag", f"v{NV}")
sh("git", "push", "origin", "HEAD")
sh("git", "push", "origin", f"v{NV}")

# --- build ---
sol = None
for pat in ["*.sln", "*.csproj"]:
    g = sorted(glob.glob(pat))
    if g: sol = g[0]; break
if sol is None:
    print("NO-BUILD: no solution/project (skipping assets)"); sys.exit(0)
os.makedirs("dist", exist_ok=True)
r = subprocess.run(["dotnet", "build", sol, "-c", "Release", "--nologo", "-v:m"])
if r.returncode != 0:
    print("BUILD FAILED after tagging (tag kept, fix forward)"); sys.exit(1)

# --- assets: <ModId>.dll ---
modid = None
if os.path.exists("manifest.json"):
    try: modid = json.load(open("manifest.json")).get("name")
    except Exception: pass
cands = []
for root, _, files in os.walk("bin"):
    for f in files:
        if f.endswith(".dll") and "Release" in root:
            cands.append(os.path.join(root, f))
asset = None
if modid:
    for c in cands:
        if os.path.basename(c) == f"{modid}.dll": asset = c; break
if asset is None and cands:
    asset = sorted(cands, key=len)[0]
if asset:
    import shutil
    shutil.copy(asset, "dist/")
    print(f"ASSET: {asset}")
    notes = sh("git", "log", f"{last}..HEAD", "--no-merges", "--format=- %s")
    sh("gh", "release", "create", f"v{NV}", os.path.join("dist", os.path.basename(asset)),
       "--title", f"v{NV}", "--notes", notes or f"Release v{NV}")
    print(f"RELEASED v{NV}")
else:
    print("NO-ASSET: no Release DLL found (tag kept)")

# --- next -dev cycle ---
nv2 = (new[0], new[1], new[2] + 1); ND = fmt(nv2) + "-dev"
if os.path.exists("VERSION"):
    open("VERSION", "w").write(ND + "\n")
for cs in glob.glob("*.csproj"):
    t = open(cs).read()
    t = re.sub(r"<Version>[^<]+</Version>", f"<Version>{ND}</Version>", t)
    t = re.sub(r"<AssemblyVersion>[^<]+</AssemblyVersion>", f"<AssemblyVersion>{ND}.0</AssemblyVersion>", t)
    t = re.sub(r"<FileVersion>[^<]+</FileVersion>", f"<FileVersion>{ND}.0</FileVersion>", t)
    open(cs, "w").write(t)
for f in subprocess.run(["git", "grep", "-l", "MelonInfo", "--", "src"],
                        capture_output=True, text=True).stdout.split():
    lines = open(f).read().split("\n")
    out = []
    for line in lines:
        if "MelonInfo" in line:
            line = re.sub(r'"\d+\.\d+\.\d+(?:-dev)?"', f'"{ND}"', line)
        out.append(line)
    open(f, "w").write("\n".join(out))
sh("git", "add", "-A")
sh("git", "-c", "user.name=greg-ci", "-c", "user.email=greg-ci@users.noreply.github.com",
   "commit", "-m", "[skip ci] chore(release): begin next dev cycle")
sh("git", "push", "origin", "HEAD")
print("DEV-CYCLE opened:", ND)
