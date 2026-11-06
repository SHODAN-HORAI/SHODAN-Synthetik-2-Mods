using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SHODANSynthetik2;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try { File.Delete(Core.OldExe); } catch { } // leftover from a self update

        string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var manifestUrl = Arg("--manifest") ?? Core.ManifestUrl;

        // Headless mode: --install-all / --remove-all / --update-app [--game dir] [--manifest url]
        if (args.Contains("--update-app"))
        {
            var m = Core.LoadManifest(manifestUrl);
            if (Core.IsNewer(m.app.version, Core.AppVersion)) { Core.SelfUpdate(m.app); Console.WriteLine("Updated to " + m.app.version); }
            else Console.WriteLine("Up to date");
            return 0;
        }
        if (args.Contains("--install-all") || args.Contains("--remove-all"))
        {
            try
            {
                var game = Arg("--game") ?? Core.FindGame();
                if (!Core.IsGameFolder(game)) throw new Exception("Game folder not found.");
                var m = Core.LoadManifest(manifestUrl);
                if (args.Contains("--remove-all")) Core.RemoveAll(game, m, Console.WriteLine);
                else foreach (var mod in m.mods) Core.InstallMod(game, m, mod, Console.WriteLine);
                return 0;
            }
            catch (Exception e) { Console.WriteLine("ERROR: " + e.Message); return 1; }
        }

        Application.EnableVisualStyles();
        Application.Run(new MainForm(manifestUrl));
        return 0;
    }
}

class MainForm : Form
{
    readonly string _manifestUrl;
    Manifest _manifest;
    string _game;

    readonly Label _gameLabel = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly ListView _list = new() { CheckBoxes = true, View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    readonly Button _apply = new() { Text = "Apply", Width = 110, Height = 30, Enabled = false };
    readonly Button _removeAll = new() { Text = "Remove everything", Width = 140, Height = 30, Enabled = false };
    readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

    public MainForm(string manifestUrl)
    {
        _manifestUrl = manifestUrl;
        Text = $"SHODAN Synthetik 2 v{Core.AppVersion.ToString(3)}";
        Width = 880; Height = 400;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _list.Columns.Add("Mod", 170);
        _list.Columns.Add("Installed", 75);
        _list.Columns.Add("Latest", 65);
        _list.Columns.Add("What it does", 540);

        var browse = new Button { Text = "Change...", Width = 90, Height = 28 };
        browse.Click += (_, _) => PickGame();
        _apply.Click += async (_, _) => await Run(Apply);
        _removeAll.Click += async (_, _) =>
        {
            if (MessageBox.Show(this, "Remove all SHODAN Synthetik 2 mods from the game?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
                await Run(() => Core.RemoveAll(_game, _manifest, Log));
        };

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 2, Padding = new Padding(8, 6, 8, 0) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(_gameLabel, 0, 0);
        top.Controls.Add(browse, 1, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 46, ColumnCount = 3, Padding = new Padding(8, 6, 8, 6) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(_removeAll, 1, 0);
        bottom.Controls.Add(_apply, 2, 0);

        var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 0) };
        middle.Controls.Add(_list);
        Controls.Add(middle);
        Controls.Add(top);
        Controls.Add(bottom);

        Shown += async (_, _) => await LoadAll();
    }

    string _last;
    void Log(string msg) { _last = msg; BeginInvoke(new Action(() => _status.Text = msg)); }

    void SetGame(string dir)
    {
        _game = dir;
        _gameLabel.Text = Core.IsGameFolder(dir) ? "Game: " + dir : "Synthetik 2 not found. Click Change... and pick the game folder.";
    }

    void PickGame()
    {
        using var dlg = new FolderBrowserDialog { Description = "Pick the SYNTHETIK 2 folder (the one with Synthetik2.exe)" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!Core.IsGameFolder(dlg.SelectedPath)) { MessageBox.Show(this, "Synthetik2.exe is not in that folder.", Text); return; }
        Core.SaveGame(dlg.SelectedPath);
        SetGame(dlg.SelectedPath);
        Refill();
    }

    async Task LoadAll()
    {
        SetGame(Core.FindGame());
        _status.Text = "Checking GitHub for mods...";
        try { _manifest = await Task.Run(() => Core.LoadManifest(_manifestUrl)); }
        catch (Exception e) { _status.Text = "Could not reach GitHub: " + e.Message; return; }

        if (Core.IsNewer(_manifest.app?.version, Core.AppVersion) &&
            MessageBox.Show(this, $"A new version ({_manifest.app.version}) of this app is out. Update now?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            try { await Task.Run(() => Core.SelfUpdate(_manifest.app)); Close(); return; }
            catch (Exception e) { MessageBox.Show(this, "Update failed: " + e.Message, Text); }
        }
        Refill();
    }

    void Refill()
    {
        _list.Items.Clear();
        if (_manifest == null) return;
        bool ok = Core.IsGameFolder(_game);
        int updates = 0;
        foreach (var mod in _manifest.mods)
        {
            var have = ok ? Core.InstalledVersion(_game, mod) : null;
            bool outdated = have != null && Core.IsNewer(mod.version, have);
            if (outdated) updates++;
            var item = new ListViewItem(new[] { mod.name, have == null ? "-" : have.ToString(3) + (outdated ? " (old)" : ""), mod.version, mod.description })
                { Checked = have != null, Tag = mod };
            _list.Items.Add(item);
        }
        _apply.Enabled = _removeAll.Enabled = ok;
        _status.Text = !ok ? "" : updates > 0 ? $"{updates} update(s) available. Click Apply." : "Tick the mods you want, then click Apply.";
    }

    void Apply()
    {
        foreach (var item in _list.Items.Cast<ListViewItem>().Select(i => (mod: (ModInfo)i.Tag, want: (bool)Invoke(new Func<bool>(() => i.Checked)))))
        {
            var have = Core.InstalledVersion(_game, item.mod);
            if (item.want && (have == null || Core.IsNewer(item.mod.version, have))) Core.InstallMod(_game, _manifest, item.mod, Log);
            else if (!item.want && have != null) Core.RemoveMod(_game, item.mod, Log);
        }
        Log("Done. Start the game. The first launch after installing takes 30 to 60 seconds longer.");
    }

    async Task Run(Action work)
    {
        if (Core.GameRunning()) { MessageBox.Show(this, "Close Synthetik 2 first.", Text); return; }
        _apply.Enabled = _removeAll.Enabled = false;
        _last = null;
        try { await Task.Run(work); }
        catch (UnauthorizedAccessException) { MessageBox.Show(this, "Windows blocked writing to the game folder. Right-click this app and pick Run as administrator.", Text); }
        catch (Exception e) { _last = null; MessageBox.Show(this, "Something went wrong: " + e.Message, Text); }
        Refill();
        if (_last != null) _status.Text = _last;
    }
}
