using System.Windows;

namespace UAssetLens.App;

public partial class MergeWindow : Window
{
    readonly MergeModel model;

    // Plastic reads the exit code: 0 means the output file holds the resolved version.
    public int ExitCode { get; private set; } = Program.Cancelled;

    public MergeWindow(MergeModel model)
    {
        this.model = model;
        InitializeComponent();
        DarkTitleBar.Apply(this);

        var shown = model.Shown;
        Title = $"UAsset Lens · Resolve conflict · {shown.Info.Name}";
        NameText.Text = shown.Info.Name;
        TypeText.Text = shown.Info.Type;
        TypeChip.Visibility = string.IsNullOrEmpty(shown.Info.Type) ? Visibility.Collapsed : Visibility.Visible;
        PathText.Text = model.StoredPath;
        VersionsText.Text = model.Versions;
        TheirsHead.Text = model.TheirsLabel;
        var changed = model.Result.Changed.Count();
        ChangedText.Text = changed == 1 ? "1 setting changed" : $"{changed} settings changed";
        FooterText.Text = model.FooterText;
        WarningText.Text = model.Warning;

        var problems = new List<string>();
        foreach (var (side, snap) in new[] { ("Base", model.Base), ("Theirs", model.Theirs), ("Yours", model.Yours) })
        {
            if (snap.IsMissing) continue;
            if (snap.Error != null) problems.Add($"{side}: couldn't read this file. {snap.Error}");
            problems.AddRange(snap.Problems.Select(p => $"{side}: {p}"));
        }
        if (problems.Count > 0)
        {
            ProblemBox.Visibility = Visibility.Visible;
            ProblemText.Text = string.Join("\n", problems.Distinct().Take(6)) + (problems.Count > 6 ? $"\n…and {problems.Count - 6} more" : "");
        }
        Fill();
        Loaded += (_, _) => KeepYoursButton.Focus();
    }

    void Fill()
    {
        var rows = model.Rows(ShowUnchanged.IsChecked == true);
        RowsList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = "No setting differs between the versions. Either choice gives the same settings.";
    }

    void ShowUnchanged_Changed(object sender, RoutedEventArgs e) => Fill();

    void KeepYours_Click(object sender, RoutedEventArgs e) => Keep(model.YoursFile, "yours");

    void KeepTheirs_Click(object sender, RoutedEventArgs e) => Keep(model.TheirsFile, "theirs");

    void Keep(string source, string side)
    {
        try
        {
            if (File.Exists(source)) File.Copy(source, model.OutputFile, overwrite: true);
            else if (File.Exists(model.OutputFile)) File.Delete(model.OutputFile); // the kept side deleted the file
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Couldn't write the result for Plastic:\n{ex.Message}", "UAsset Lens");
            return;
        }
        Log.Write($"kept {side}: {source} -> {model.OutputFile}");
        ExitCode = Program.Kept;
        Close();
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
