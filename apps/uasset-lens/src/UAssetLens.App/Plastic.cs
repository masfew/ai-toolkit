using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UAssetLens.App;

// Finds the Plastic or Gluon window that started this run, so our windows can close when it does.
static class Plastic
{
    static readonly string[] Names = ["plastic.exe", "gluon.exe"];

    // Null for runs from the command line, cm or tests. Looks a few levels up in case Plastic starts tools
    // through a helper process.
    public static Process? Launcher()
    {
        var table = ProcessTable();
        var chain = new List<string>();
        int pid = Environment.ProcessId;
        DateTime childStart;
        try { childStart = Process.GetCurrentProcess().StartTime; } catch (Win32Exception) { return null; }
        for (int depth = 0; depth < 4 && table.TryGetValue(pid, out var entry); depth++)
        {
            if (!table.TryGetValue(entry.Parent, out var parent)) break;
            chain.Add($"{parent.Name} {entry.Parent}");
            Process p;
            try
            {
                p = Process.GetProcessById(entry.Parent);
                // Windows reuses process ids, so a "parent" that started after its child is someone else.
                if (p.StartTime > childStart) break;
                childStart = p.StartTime;
            }
            catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException) { break; }
            if (Names.Contains(parent.Name, StringComparer.OrdinalIgnoreCase))
            {
                Log.Write($"started by {string.Join(" < ", chain)}");
                return p;
            }
            pid = entry.Parent;
        }
        Log.Write(chain.Count == 0 ? "no Plastic parent" : $"no Plastic parent: {string.Join(" < ", chain)}");
        return null;
    }

    public static Process? ById(int id)
    {
        try { return id > 0 ? Process.GetProcessById(id) : null; }
        catch (ArgumentException) { return null; } // it has exited
    }

    // Runs action once the process exits, on a thread-pool thread (or straight away if it already has).
    public static void OnExit(Process p, Action action)
    {
        try
        {
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => action();
            if (p.HasExited) action();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { Log.Write("can't watch Plastic: " + ex.Message); }
    }

    static Dictionary<int, (int Parent, string Name)> ProcessTable()
    {
        var table = new Dictionary<int, (int, string)>();
        var snap = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snap == InvalidHandle) return table;
        try
        {
            var e = new ProcessEntry { Size = Marshal.SizeOf<ProcessEntry>() };
            for (bool ok = Process32FirstW(snap, ref e); ok; ok = Process32NextW(snap, ref e))
                table[e.ProcessId] = (e.ParentProcessId, e.ExeFile);
        }
        finally { CloseHandle(snap); }
        return table;
    }

    const uint SnapProcess = 2;
    static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry
    {
        public int Size;
        public int Usage;
        public int ProcessId;
        public IntPtr DefaultHeapId;
        public int ModuleId;
        public int Threads;
        public int ParentProcessId;
        public int PriClassBase;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
}
