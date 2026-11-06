"""Build every release asset into builds/:
SHODANSynthetik2.exe, BepInEx-Base.zip, one <Mod>.dll per mod, and mods.json (what the installer reads).
Usage: python pack.py <BepInEx-Unity.IL2CPP-win-x64 zip> <bepinex version>
Adding a mod: put its project in Mods/<Id>/ and add it to MODS below."""
import hashlib, json, re, shutil, subprocess, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).parent
OUT = ROOT / "builds"
URL = "https://github.com/SHODAN-HORAI/SHODAN-Synthetik-2-Mods/releases/latest/download/"

MODS = [
    ("BreacherNoSelfStun", "Breacher No Self Stun", "Your own fusion stun grenade no longer stuns or staggers you."),
    ("AllModifiers", "All Modifiers", "Every difficulty modifier in the lobby, hidden ones too, and they never get used up."),
    ("DropStarterWeapon", "Drop Starter Weapon", "Class starter weapons can be dropped."),
]


def build(project: Path, tfm: str) -> tuple[Path, str]:
    subprocess.run(["dotnet", "build", "-c", "Release", "-v", "q", str(project)], check=True)
    version = re.search(r"<Version>(.+?)</Version>", project.read_text()).group(1)
    return project.parent / "bin/Release" / tfm, version


def asset(path: Path, version: str) -> dict:
    shutil.copy2(path, OUT / path.name)
    return {"version": version, "url": URL + path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def main(bie_zip: str, bie_version: str):
    OUT.mkdir(exist_ok=True)
    for old in OUT.iterdir():
        old.unlink()

    base = OUT / "BepInEx-Base.zip"
    with zipfile.ZipFile(bie_zip) as src, zipfile.ZipFile(base, "w", zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            if item.filename != "changelog.txt":
                dst.writestr(item, src.read(item))
        dst.write(ROOT / "ThirdParty/BepInEx-LICENSE.txt", "BepInEx/LICENSE-BepInEx.txt")  # LGPL-2.1 asks for its text

    app_dir, app_version = build(ROOT / "Installer/SHODANSynthetik2.csproj", "net48")
    manifest = {
        "app": asset(app_dir / "SHODANSynthetik2.exe", app_version),
        "bepinex": {"version": bie_version, "url": URL + base.name, "sha256": hashlib.sha256(base.read_bytes()).hexdigest()},
        "mods": [],
    }
    for mod_id, name, description in MODS:
        out_dir, version = build(ROOT / f"Mods/{mod_id}/{mod_id}.csproj", "net6.0")
        manifest["mods"].append({"id": mod_id, "name": name, "description": description, **asset(out_dir / f"{mod_id}.dll", version)})

    (OUT / "mods.json").write_text(json.dumps(manifest, indent=2))
    for f in sorted(OUT.iterdir()):
        print(f.name)


if __name__ == "__main__":
    main(*sys.argv[1:3])
