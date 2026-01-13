using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NuGet.Protocol.Core.Types;
using Code2Viz.Execution;
using Code2Viz.Project;

namespace Code2Viz;

public partial class NuGetPackageManagerWindow : Window
{
    private readonly VizCodeProject _project;
    private readonly NuGetHelper _nugetHelper;

    public NuGetPackageManagerWindow(VizCodeProject project)
    {
        InitializeComponent();
        _project = project;
        _nugetHelper = new NuGetHelper(Path.Combine(project.ProjectDirectory, ".packages"));
        RefreshList();
        
        // Focus search box by default
        Loaded += (s, e) => { SearchBox.Focus(); };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _nugetHelper?.Dispose();
    }

    private void RefreshList()
    {
        PackagesList.ItemsSource = null;
        PackagesList.ItemsSource = _project.ProjectFile.Packages;
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (PackagesList.SelectedItem is PackageReference pkg)
        {
            if (MessageBox.Show($"Remove {pkg.Id}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _project.RemovePackage(pkg.Id);
                RefreshList();
            }
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformSearch();
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformSearch();
        }
    }

    private async Task PerformSearch()
    {
        var term = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;

        SearchProgressBar.Visibility = Visibility.Visible;
        SearchResultsList.ItemsSource = null;

        try
        {
            var results = await Task.Run(() => _nugetHelper.SearchPackagesAsync(term));
            var viewModels = results.Select(r => new PackageSearchResultViewModel(r)).ToList();
            SearchResultsList.ItemsSource = viewModels;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Search failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void VersionComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.Tag is PackageSearchResultViewModel vm)
        {
            if (vm.Versions.Count <= 1 && !vm.IsLoadingVersions)
            {
                vm.IsLoadingVersions = true;
                try
                {
                    var versions = await Task.Run(() => _nugetHelper.GetPackageVersionsAsync(vm.Id));
                    // Sort descending (latest first)
                    var sortedVersions = versions.OrderByDescending(v => v).ToList();
                    
                    vm.Versions.Clear();
                    foreach (var v in sortedVersions)
                    {
                        vm.Versions.Add(v);
                    }
                    
                    if (vm.SelectedVersion != null && vm.Versions.Contains(vm.SelectedVersion))
                    {
                        // Keep selection
                    }
                    else if (vm.Versions.Count > 0)
                    {
                        vm.SelectedVersion = vm.Versions[0];
                    }
                }
                catch
                {
                    // Ignore errors for now, user can still use the single version
                }
                finally
                {
                    vm.IsLoadingVersions = false;
                }
            }
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PackageSearchResultViewModel vm)
        {
            var id = vm.Id;
            var version = vm.SelectedVersion?.ToString() ?? vm.Metadata.Identity.Version.ToString();

            if (_project.ProjectFile.Packages.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                // Optionally check if updating version
                var existing = _project.ProjectFile.Packages.First(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (existing.Version == version)
                {
                     MessageBox.Show($"{id} ({version}) is already installed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                     return;
                }
                else
                {
                    if (MessageBox.Show($"Update {id} from {existing.Version} to {version}?", "Update", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        var oldVersion = existing.Version;
                        _project.RemovePackage(id);
                        await InstallPackageAsync(id, version);
                    }
                    return;
                }
            }

            await InstallPackageAsync(id, version);
        }
    }

    private async Task InstallPackageAsync(string id, string version)
    {
        try
        {
            SearchProgressBar.Visibility = Visibility.Visible;
            _project.AddPackage(id, version);
            RefreshList();

            // Trigger immediate restore
            await Task.Run(() => _nugetHelper.RestorePackageAsync(id, version));

            MessageBox.Show($"Added and restored {id} ({version}) to project.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Added {id} but failed to restore: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
             SearchProgressBar.Visibility = Visibility.Collapsed;
        }
    }
}
