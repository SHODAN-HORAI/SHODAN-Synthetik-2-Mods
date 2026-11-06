using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace SHODANSynthetik2;

public class Package
{
    public string version { get; set; }
    public string url { get; set; }
    public string sha256 { get; set; }
}

public class ModInfo : Package
{
    public string id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
}

public class Manifest
{
    public Package app { get; set; }
    public Package bepinex { get; set; }
    public List<ModInfo> mods { get; set; }
}

public static class Core
{
    // The manifest is a release asset, so it always matches the files of the newest release.
    public const string ManifestUrl = "https://github.com/SHODAN-HORAI/SHODAN-Synthetik-2-Mods/releases/latest/download/mods.json";
    const string GameExe = "Synthetik2.exe";
    static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SHODAN", "Synthetik2", "game.txt");

    public static Version AppVersion => typeof(Core).Assembly.GetName().Version;

    static Core() => ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

    // ---------- Game folder ----------

    public static bool IsGameFolder(string dir) => dir != null && File.Exists(Path.Combine(dir, GameExe));

    public static string FindGame()
    {
        try { var saved = File.ReadAllText(SettingsFile).Trim(); if (IsGameFolder(saved)) return saved; } catch { }

        // Steam writes the exact install folder to Windows' "installed apps" list.
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1471410");
                if (key?.GetValue("InstallLocation") is string loc && IsGameFolder(loc)) return Path.GetFullPath(loc);
            }
            catch { }

        // Otherwise check every Steam library folder.
        var steams = new[]
        {
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string,
        }.Where(s => s != null);
        var libraries = new List<string>();
        foreach (var steam in steams)
        {
            libraries.Add(steam);
            try
            {
                var vdf = File.ReadAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
                libraries.AddRange(Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value.Replace(@"\\", @"\")));
            }
            catch { }
        }
        return libraries.Select(l => Path.GetFullPath(Path.Combine(l, "steamapps", "common", "SYNTHETIK 2"))).FirstOrDefault(IsGameFolder);
    }

    public static void SaveGame(string dir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
        File.WriteAllText(SettingsFile, dir);
    }

    public static bool GameRunning() => Process.GetProcessesByName("Synthetik2").Length > 0;

    // ---------- Downloads ----------

    public static Manifest LoadManifest(string url)
    {
        using var web = new WebClient();
        web.Encoding = System.Text.Encoding.UTF8;
        return new JavaScriptSerializer().Deserialize<Manifest>(web.DownloadString(url));
    }

    static byte[] Download(Package p)
    {
        using var web = new WebClient();
        var data = web.DownloadData(p.url);
        if (!string.IsNullOrEmpty(p.sha256))
        {
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
            if (!hash.Equals(p.sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Download of {Path.GetFileName(p.url)} was damaged, try again in a minute.");
        }
        return data;
    }

    public static bool IsNewer(string remote, Version local) =>
        Version.TryParse(remote, out var r) && (local == null || Norm(r) > Norm(local));

    static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    // ---------- Mods ----------

    static string Plugins(string game) => Path.Combine(game, "BepInEx", "plugins");
    static string ModPath(string game, ModInfo mod) => Path.Combine(Plugins(game), mod.id + ".dll");
    static string LoaderMarker(string game) => Path.Combine(game, "BepInEx", "SHODANSynthetik2-bepinex.txt");

    public static Version InstalledVersion(string game, ModInfo mod)
    {
        var path = ModPath(game, mod);
        if (!File.Exists(path)) return null;
        return Version.TryParse(FileVersionInfo.GetVersionInfo(path).FileVersion, out var v) ? v : new Version(0, 0);
    }

    static bool LoaderInstalled(string game) =>
        File.Exists(Path.Combine(game, "winhttp.dll")) && Directory.Exists(Path.Combine(game, "BepInEx", "core"));

    /// <summary>Installs or updates BepInEx. Returns true when it was (re)installed.</summary>
    static bool EnsureLoader(string game, Package bepinex)
    {
        string current = null;
        try { current = File.ReadAllText(LoaderMarker(game)).Trim(); } catch { }
        if (LoaderInstalled(game) && (current == null || current == bepinex.version)) return false;

        var root = Path.GetFullPath(game + Path.DirectorySeparatorChar);
        using (var zip = new ZipArchive(new MemoryStream(Download(bepinex))))
            foreach (var e in zip.Entries)
            {
                var dest = Path.GetFullPath(Path.Combine(game, e.FullName));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // never write outside the game folder
                if (e.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                e.ExtractToFile(dest, true);
            }
        File.WriteAllText(LoaderMarker(game), bepinex.version);
        return true;
    }

    public static void InstallMod(string game, Manifest m, ModInfo mod, Action<string> log)
    {
        if (EnsureLoader(game, m.bepinex)) log("Installed BepInEx " + m.bepinex.version);
        var data = Download(mod);
        Directory.CreateDirectory(Plugins(game));
        File.WriteAllBytes(ModPath(game, mod), data);
        log($"Installed {mod.name} {mod.version}");
    }

    public static void RemoveMod(string game, ModInfo mod, Action<string> log)
    {
        File.Delete(ModPath(game, mod));
        log("Removed " + mod.name);
    }

    /// <summary>Removes our mods, and BepInEx too when no other plugins are left.</summary>
    public static void RemoveAll(string game, Manifest m, Action<string> log)
    {
        foreach (var mod in m.mods)
            if (File.Exists(ModPath(game, mod))) RemoveMod(game, mod, log);

        var others = Directory.Exists(Plugins(game)) ? Directory.GetFiles(Plugins(game), "*.dll", SearchOption.AllDirectories) : new string[0];
        if (others.Length > 0) { log("Kept BepInEx because other mods use it."); return; }

        foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version" })
            File.Delete(Path.Combine(game, f));
        foreach (var d in new[] { "BepInEx", "dotnet" })
            if (Directory.Exists(Path.Combine(game, d))) Directory.Delete(Path.Combine(game, d), true);
        log("Removed BepInEx. The game is back to normal.");
    }

    // ---------- Self update ----------

    public static string OldExe => Process.GetCurrentProcess().MainModule.FileName + ".old";

    /// <summary>Swaps the running exe for the new one (Windows allows renaming a running exe) and starts it.</summary>
    public static void SelfUpdate(Package app)
    {
        var exe = Process.GetCurrentProcess().MainModule.FileName;
        var data = Download(app);
        if (File.Exists(OldExe)) File.Delete(OldExe);
        File.Move(exe, OldExe);
        File.WriteAllBytes(exe, data);
        Process.Start(exe);
    }
}
