using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using UAssetLens.Core;

namespace UAssetLens.App;

public static class Program
{
    public const int Kept = 0;
    public const int Cancelled = 1;
    public const int Failed = 2;

    [STAThread]
    public static int Main(string[] args)
    {
        Log.Write("start " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));
        int code;
        try { code = Run(args); }
        catch (UsageException ex)
        {
            Report($"{ex.Message}\n\n{CommandLine.Usage}", args.Contains("--text"));
            code = Failed;
        }
        catch (Exception ex)
        {
            Log.Write(ex.ToString());
            Report($"UAsset Lens hit an error:\n{ex.Message}", args.Contains("--text"));
            code = Failed;
        }
        Log.Write($"exit {code}");
        return code;
    }

    static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Report(CommandLine.Usage, text: false);
            return Kept;
        }
        var cl = CommandLine.Parse(args);
        switch (cl.Mode)
        {
            case "show" when cl.Text:
                Print(TextReport.Show(Load(cl["file"]!)));
                return Kept;
            case "diff" when cl.Text:
            {
                var before = Load(cl["before"]!);
                var after = Load(cl["after"]!);
                Print(TextReport.Diff(before, after, Compare.Diff(before, after), cl.All));
                return Kept;
            }
            case "show" or "diff":
            {
                var plastic = Plastic.Launcher();
                // Two tries, in case the open window closes between our claim and our hand-off.
                for (int i = 0; i < 2 && !DiffHost.Claim(); i++)
                    if (DiffHost.TryHandOff(args, plastic?.Id ?? 0)) return Kept;
                return RunDiffWindows(DiffModelFor(cl, Environment.CurrentDirectory), plastic);
            }
            default: // merge
            {
                var model = new MergeModel(
                    Load(cl["base"]!), Load(cl["theirs"]!), Load(cl["yours"]!),
                    cl["theirs"]!, cl["yours"]!, cl["out"]!,
                    cl["base-name"], cl["theirs-name"], cl["yours-name"]);
                if (cl.Text) { Print(TextReport.Merge(model.Yours, model.Result)); return Cancelled; }
                return RunMergeWindow(model);
            }
        }
    }

    // Plastic passes an empty or missing file for the side of an added or deleted file.
    static AssetSnapshot Load(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) return AssetSnapshot.Missing;
        return SnapshotReader.Read(path);
    }

    // A run handed over by DiffHost carries the sender's working folder, so relative paths still resolve.
    static DiffModel DiffModelFor(CommandLine cl, string cwd)
    {
        string Full(string name) => Path.GetFullPath(cl[name]!, cwd);
        return cl.Mode == "show"
            ? new DiffModel(AssetSnapshot.Missing, Load(Full("file")), null, cl["file"])
            : new DiffModel(Load(Full("before")), Load(Full("after")), cl["before-name"], cl["after-name"]);
    }

    // The theme has to be loaded before a window is built, since the windows look up its styles.
    static Application NewApp(ShutdownMode mode)
    {
        var app = new Application { ShutdownMode = mode };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/UAssetLens;component/Theme.xaml") });
        return app;
    }

    static int RunDiffWindows(DiffModel first, Process? plastic)
    {
        var app = NewApp(ShutdownMode.OnLastWindowClose);
        DiffWindows.Show(first, plastic);
        if (DiffHost.IsOwner)
            DiffHost.Listen((cwd, plasticId, args) =>
            {
                var cl = CommandLine.Parse(args);
                if (cl.Mode is not ("diff" or "show") || cl.Text) return false;
                var model = DiffModelFor(cl, cwd); // read here, before the sender exits and Plastic deletes its temp files
                var from = Plastic.ById(plasticId);
                bool shown = false;
                app.Dispatcher.Invoke(() =>
                {
                    if (DiffWindows.IsEmpty) return; // the last window closed and the process is ending
                    DiffWindows.Show(model, from);
                    shown = true;
                });
                return shown;
            });
        app.Run();
        DiffHost.Release();
        return Kept;
    }

    static int RunMergeWindow(MergeModel model)
    {
        var app = NewApp(ShutdownMode.OnMainWindowClose);
        var window = new MergeWindow(model);
        // Closing Plastic closes the conflict window too, which leaves the conflict unresolved like Cancel.
        if (Plastic.Launcher() is { } plastic)
            Plastic.OnExit(plastic, () => window.Dispatcher.BeginInvoke(window.Close));
        app.Run(window);
        return window.ExitCode;
    }

    static void Report(string message, bool text)
    {
        if (text) Print(message);
        else MessageBox.Show(message, "UAsset Lens");
    }

    // The exe is a Windows app, so it only has a console when started from one or with redirected output.
    static void Print(string text)
    {
        AttachConsole(-1);
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Out.Write(text);
        Console.Out.Flush();
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);
}

// A short log of each run, so a Plastic hookup that misbehaves can be traced afterwards.
static class Log
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UAssetLens");
    public static readonly string FilePath = Path.Combine(Dir, "uassetlens.log");

    public static void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 512 * 1024)
                File.Move(FilePath, FilePath + ".old", overwrite: true);
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{Environment.ProcessId}] {line}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
