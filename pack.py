"""Build a release into builds/: the app, every mod and mods.json (what the installer reads).
BepInEx-Base.zip (35 MB) is only built into builds/ when <bepinex version> is new; otherwise mods.json
keeps linking the release it was uploaded in. Also writes release-notes.md.
Usage: python pack.py <BepInEx-Unity.IL2CPP-win-x64 zip> <bepinex version> <release tag, e.g. v1.1.0>
Adding a mod: put its project in Mods/<Id>/ and add it to MODS below."""
import hashlib, json, re, shutil, subprocess, sys, urllib.request, zipfile
from pathlib import Path

ROOT = Path(__file__).parent
OUT = ROOT / "builds"
REPO = "SHODAN-HORAI/SHODAN-Synthetik-2-Mods"

MODS = [
    ("BreacherNoSelfStun", "Breacher No Self Stun", "Your own fusion stun grenade no longer stuns or staggers you."),
    ("AllModifiers", "All Modifiers", "Every difficulty modifier in the lobby, hidden ones too, and they never get used up."),
    ("DropStarterWeapon", "Drop Starter Weapon", "Class starter weapons can be dropped."),
]


def build(project: Path, tfm: str) -> tuple[Path, str]:
    subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", str(project)], check=True)
    version = re.search(r"<Version>(.+?)</Version>", project.read_text()).group(1)
    return project.parent / "bin/Release" / tfm, version


def previous() -> dict:
    """The live mods.json, with "latest" links pinned to the release they really live in."""
    try:
        with urllib.request.urlopen(f"https://api.github.com/repos/{REPO}/releases/latest") as r:
            tag = json.load(r)["tag_name"]
        with urllib.request.urlopen(f"https://github.com/{REPO}/releases/download/{tag}/mods.json") as r:
            text = r.read().decode()
    except OSError as e:
        sys.exit(f"Couldn't read the current release ({e}). Nothing was built.")
    return json.loads(text.replace(f"/{REPO}/releases/latest/download/", f"/{REPO}/releases/download/{tag}/"))


def asset(path: Path, version: str, tag: str, old: dict | None = None) -> dict:
    sha = hashlib.sha256(path.read_bytes()).hexdigest()
    if old and old["version"] == version and old["sha256"] != sha:
        print(f"  note: {path.name} changed but is still version {version}, so the app won't update it; bump the version")
    if path.parent != OUT:
        shutil.copy2(path, OUT / path.name)
    return {"version": version, "url": f"https://github.com/{REPO}/releases/download/{tag}/{path.name}", "sha256": sha}


def main(bie_zip: str, bie_version: str, tag: str):
    old = previous()
    old_mods = {m["id"]: m for m in old["mods"]}
    OUT.mkdir(exist_ok=True)
    for f in OUT.iterdir():
        f.unlink()

    if old["bepinex"]["version"] == bie_version:
        bepinex = old["bepinex"]
    else:
        base = OUT / "BepInEx-Base.zip"
        with zipfile.ZipFile(bie_zip) as src, zipfile.ZipFile(base, "w", zipfile.ZIP_DEFLATED) as dst:
            for item in src.infolist():
                if item.filename != "changelog.txt":
                    dst.writestr(item, src.read(item))
            dst.write(ROOT / "ThirdParty/BepInEx-LICENSE.txt", "BepInEx/LICENSE-BepInEx.txt")  # LGPL-2.1 asks for its text
        bepinex = asset(base, bie_version, tag)

    app_dir, app_version = build(ROOT / "Installer/SHODANSynthetik2.csproj", "net48")
    manifest = {"app": asset(app_dir / "SHODANSynthetik2.exe", app_version, tag, old["app"]), "bepinex": bepinex, "mods": []}
    for mod_id, name, description in MODS:
        out_dir, version = build(ROOT / f"Mods/{mod_id}/{mod_id}.csproj", "net6.0")
        dll = asset(out_dir / f"{mod_id}.dll", version, tag, old_mods.get(mod_id))
        manifest["mods"].append({"id": mod_id, "name": name, "description": description, **dll})

    (OUT / "mods.json").write_text(json.dumps(manifest, indent=2))
    (ROOT / "release-notes.md").write_text(
        "Easiest way: download `SHODANSynthetik2.exe` below and run it.\n\n"
        f"Installing by hand? You also need [BepInEx-Base.zip]({bepinex['url']}) (BepInEx {bepinex['version']}).\n")
    print("Upload to release", tag + ":")
    for f in sorted(OUT.iterdir()):
        print(" ", f.name)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:4])
