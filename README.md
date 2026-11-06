# SHODAN Synthetik 2 Mods

Mods for **Synthetik 2**, and **SHODAN Synthetik 2**, a small Windows app that installs, updates and removes them for you.

**[Download the latest release](../../releases/latest)** · Windows

## The mods

| Mod | What it does |
|---|---|
| **Breacher No Self Stun** | Your own fusion stun grenade no longer stuns or staggers you. Enemies still get stunned. |
| **All Modifiers** | Every difficulty modifier is available in the lobby, including the ones you normally find in runs and the ones the game hides. Picking them never uses them up. |
| **Drop Starter Weapon** | Class starter weapons can be dropped like any other weapon. |

## Install with the app

1. Download `SHODANSynthetik2.exe` from the [latest release](../../releases/latest) and run it.
2. Tick the mods you want and click **Apply**.
3. Start the game. The first launch after installing takes 30 to 60 seconds longer while the mod loader sets itself up. This happens once.

The app finds the game by itself. It checks for updates every time you open it, for itself and for the mods, and new mods show up in the list when they come out. Untick a mod and click Apply to remove it, or click **Remove everything** to put the game back to normal.

Windows may show "Windows protected your PC" the first time you run the app. Click **More info** and then **Run anyway**.

## Install by hand

1. Download `BepInEx-Base.zip` and the `.dll` of each mod you want from the [latest release](../../releases/latest).
2. In Steam, right-click Synthetik 2 and pick **Manage** and then **Browse local files**.
3. Open `BepInEx-Base.zip` and drag everything inside it into the game folder. Don't use "Extract All", it puts the files one folder too deep.
4. Put the mod `.dll` files in `BepInEx\plugins` inside the game folder.

To remove a mod, delete its `.dll` from `BepInEx\plugins`. Steam's **Verify integrity of game files** removes everything.

## Building

Needs the .NET 8 SDK and Python. The mods build against the game classes BepInEx generates, so install BepInEx and start the game once first.

```
python pack.py <BepInEx-Unity.IL2CPP-win-x64 zip> <BepInEx version>
```

This builds the app and every mod into `builds\`, together with `mods.json`, the list the app reads. Upload everything in `builds\` to a new release.

The mods use [BepInEx 6](https://github.com/BepInEx/BepInEx) and [Harmony](https://github.com/pardeike/Harmony). Nothing on disk is changed besides the files they add to the game folder.

## License

GPL-3.0, see [LICENSE](LICENSE).
