using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace UAssetLens.App;

public partial class DiffWindow : Window, INotifyPropertyChanged
{
    DiffModel model = null!;

    public GridLength LabelWidth { get; } = new(2, GridUnitType.Star);
    public GridLength BeforeWidth { get; private set; }
    public GridLength AfterWidth { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Pinned => PinButton.IsChecked == true;

    // The Plastic or Gluon process whose diff this window shows; DiffWindows closes the window when it exits.
    public int? PlasticId { get; set; }

    public DiffWindow(DiffModel model)
    {
        InitializeComponent();
        DataContext = this;
        DarkTitleBar.Apply(this);
        Load(model);
    }

    // Also called when a later diff reuses this window (DiffHost), so it resets everything it sets.
    public void Load(DiffModel model)
    {
        this.model = model;
        // An added or deleted file has only one side to show.
        BeforeWidth = model.Before.IsMissing ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        AfterWidth = model.After.IsMissing ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BeforeWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AfterWidth)));

        var shown = model.Shown;
        Title = $"UAsset Lens · {model.Title} · {shown.Info.Name}";
        NameText.Text = shown.Info.Name;
        TypeText.Text = shown.Info.Type;
        TypeChip.Visibility = string.IsNullOrEmpty(shown.Info.Type) ? Visibility.Collapsed : Visibility.Visible;
        PathText.Text = model.StoredPath;
        VersionsText.Text = model.Versions;
        VersionsText.Visibility = model.Versions.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ChangedText.Text = model.ChangedText;
        FooterText.Text = model.FooterText;
        FooterText.Visibility = model.FooterText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        BeforeHead.Text = model.After.IsMissing ? "Value" : "Before";
        AfterHead.Text = model.Before.IsMissing ? "Value" : "After";
        ShowUnchanged.Visibility = model.Before.IsMissing || model.After.IsMissing ? Visibility.Collapsed : Visibility.Visible;
        CopyButton.Visibility = model.Before.IsMissing && model.Title == "Show" ? Visibility.Collapsed : Visibility.Visible;

        var problems = new List<string>();
        foreach (var (side, snap) in new[] { ("Before", model.Before), ("After", model.After) })
        {
            if (snap.IsMissing) continue;
            if (snap.Error != null) problems.Add($"{side}: couldn't read this file. {snap.Error}");
            problems.AddRange(snap.Problems.Select(p => $"{side}: {p}"));
        }
        ProblemBox.Visibility = problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ProblemText.Text = string.Join("\n", problems.Distinct().Take(8)) + (problems.Count > 8 ? $"\n…and {problems.Count - 8} more" : "");
        Fill();
        (RowsList.Template.FindName("Scroller", RowsList) as ScrollViewer)?.ScrollToTop();
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        // Windows can refuse Activate for a background process; a topmost flip still raises the window.
        Topmost = true;
        Topmost = false;
    }

    void Fill()
    {
        var rows = model.Rows(ShowUnchanged.IsChecked == true);
        RowsList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = model.Before.Error != null || model.After.Error != null
            ? "Nothing to compare: one version couldn't be read."
            : "No setting changes. Only fields UEFN rewrites on every save differ." + (model.Result.HiddenNoise == 0 ? "" : "\nTurn on Show unchanged to see them.");
        if (model.Result.HiddenNoise == 0 && rows.Count == 0) EmptyText.Text = "The two versions have the same settings.";
    }

    void ShowUnchanged_Changed(object sender, RoutedEventArgs e) => Fill();

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(model.Summary); }
        catch (System.Runtime.InteropServices.COMException) { return; } // clipboard busy
        CopyButton.Content = "Copied";
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        t.Tick += (_, _) => { CopyButton.Content = "Copy summary"; t.Stop(); };
        t.Start();
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
