using System.Diagnostics;
using System.Windows;

namespace UAssetLens.App;

// The diff windows this process shows. A diff goes to the most recently used unpinned window, or a new one
// when every window is pinned. Each window closes when the Plastic whose diff it last showed exits.
static class DiffWindows
{
    static readonly List<DiffWindow> open = [];
    static readonly HashSet<int> watched = [];

    public static bool IsEmpty => open.Count == 0;

    // Call on the UI thread.
    public static void Show(DiffModel model, Process? plastic)
    {
        var w = open.LastOrDefault(x => !x.Pinned);
        if (w == null)
        {
            w = new DiffWindow(model);
            // Step a new window off the pinned one it would otherwise cover exactly.
            if (open.LastOrDefault() is { } under)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = under.Left + 28;
                w.Top = under.Top + 28;
            }
            var added = w;
            w.Closed += (_, _) => open.Remove(added);
            w.Show();
        }
        else w.Load(model);
        open.Remove(w);
        open.Add(w);
        w.PlasticId = plastic?.Id;
        w.BringToFront();

        if (plastic != null && watched.Add(plastic.Id))
        {
            int id = plastic.Id;
            var dispatcher = w.Dispatcher;
            Plastic.OnExit(plastic, () => dispatcher.BeginInvoke(() =>
            {
                watched.Remove(id);
                foreach (var x in open.Where(x => x.PlasticId == id).ToList()) x.Close();
            }));
        }
    }
}
