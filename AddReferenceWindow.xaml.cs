using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Code2Viz.Project;

namespace Code2Viz;

public class FrameworkAssemblyItem
{
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public partial class AddReferenceWindow : Window
{
    private readonly VizCodeProject _project;
    public ObservableCollection<FrameworkAssemblyItem> FrameworkAssemblies { get; } = new();
    public ObservableCollection<string> LocalDlls { get; } = new();

    // Common framework assemblies to show
    private static readonly string[] CommonFrameworkAssemblies = new[]
    {
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Xml",
        "System.Xml.Linq",
        "System.Net.Http",
        "System.IO.Compression",
        "System.Threading.Tasks",
        "System.Data.Common",
        "System.ComponentModel",
        "System.Drawing.Primitives",
        "Microsoft.CSharp"
    };

    public AddReferenceWindow(VizCodeProject project)
    {
        InitializeComponent();
        _project = project;

        // Get already referenced assemblies
        var existingRefs = project.ProjectFile?.References
            ?.Where(r => r.IsFramework)
            .Select(r => r.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        // Populate framework assemblies
        foreach (var asm in CommonFrameworkAssemblies)
        {
            FrameworkAssemblies.Add(new FrameworkAssemblyItem
            {
                Name = asm,
                IsSelected = existingRefs.Contains(asm)
            });
        }

        FrameworkListBox.ItemsSource = FrameworkAssemblies;
        LocalDllsListBox.ItemsSource = LocalDlls;

        // Load existing local refs
        var existingLocalRefs = project.ProjectFile?.References
            ?.Where(r => !r.IsFramework)
            .Select(r => r.Path) ?? Enumerable.Empty<string>();
        foreach (var path in existingLocalRefs)
        {
            LocalDlls.Add(path);
        }
    }

    private void BrowseDll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DLL files (*.dll)|*.dll",
            Title = "Select Assembly",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (!LocalDlls.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    LocalDlls.Add(file);
                }
            }
        }
    }

    private void RemoveDll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
        {
            LocalDlls.Remove(path);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_project.ProjectFile == null) return;

        // Clear existing references and rebuild
        _project.ProjectFile.References.Clear();

        // Add selected framework references
        foreach (var item in FrameworkAssemblies.Where(a => a.IsSelected))
        {
            _project.ProjectFile.References.Add(new AssemblyReference
            {
                Path = item.Name,
                IsFramework = true
            });
        }

        // Add local DLL references
        foreach (var path in LocalDlls)
        {
            _project.ProjectFile.References.Add(new AssemblyReference
            {
                Path = path,
                IsFramework = false
            });
        }

        _project.SaveProjectFile();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
