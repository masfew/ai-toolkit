using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace UAssetLens.App;

// Diffs share one window. The first diff run owns a mutex and listens on a pipe; later runs send it their
// command line and exit once it replies. It replies only after reading the files, because Plastic may delete
// its temp copies as soon as the process it started exits. Merges never take part: each conflict needs its
// own process and exit code.
static class DiffHost
{
    static readonly string Name = $"UAssetLens.Diff.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";
    static Mutex? owned;

    // True when we now own the shared window and should open it ourselves.
    public static bool Claim()
    {
        var m = new Mutex(false, $@"Local\{Name}");
        bool got;
        try { got = m.WaitOne(0); }
        catch (AbandonedMutexException) { got = true; } // the last owner crashed; the mutex is ours now
        if (got) owned = m; else m.Dispose();
        return got;
    }

    public static bool IsOwner => owned != null;

    // Hands the run to the window that's open. False means no window took it and this run shows it itself.
    public static bool TryHandOff(string[] args, int plasticId)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            // The owner may still be reading its own first files before it starts listening.
            pipe.Connect(10_000);
            AllowSetForegroundWindow(AsfwAny); // lets the window come to the front for this click
            var w = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);
            w.Write(Environment.CurrentDirectory);
            w.Write(plasticId);
            w.Write(args.Length);
            foreach (var a in args) w.Write(a);
            w.Flush();
            var reply = Task.Run(pipe.ReadByte);
            if (!reply.Wait(TimeSpan.FromSeconds(30))) { Log.Write("hand-off: no reply"); return false; }
            Log.Write(reply.Result == 1 ? "handed off to the open window" : "hand-off refused");
            return reply.Result == 1;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            Log.Write("hand-off failed: " + ex.Message);
            return false;
        }
    }

    // Runs on a background thread for the life of the owning process. handle gets the sender's working
    // folder, the id of the Plastic that started it (0 for none) and its arguments, and returns whether a
    // window took them.
    public static void Listen(Func<string, int, string[], bool> handle)
    {
        new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                    pipe.WaitForConnection();
                    var r = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
                    var cwd = r.ReadString();
                    var plasticId = r.ReadInt32();
                    var args = new string[r.ReadInt32()];
                    for (int i = 0; i < args.Length; i++) args[i] = r.ReadString();
                    Log.Write("diff from another run: " + string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));
                    bool ok;
                    try { ok = handle(cwd, plasticId, args); }
                    catch (Exception ex) { Log.Write(ex.ToString()); ok = false; }
                    pipe.WriteByte(ok ? (byte)1 : (byte)0);
                    pipe.WaitForPipeDrain();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
                {
                    Log.Write("pipe: " + ex.Message);
                    Thread.Sleep(500);
                }
            }
        }) { IsBackground = true, Name = "DiffHost" }.Start();
    }

    public static void Release()
    {
        owned?.ReleaseMutex();
        owned?.Dispose();
        owned = null;
    }

    const int AsfwAny = -1;

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
