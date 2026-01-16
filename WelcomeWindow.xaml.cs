using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Code2Viz.Project;

namespace Code2Viz
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            LoadRecentProjects();
        }

        private void LoadRecentProjects()
        {
            var recentProjects = RecentProjectsManager.GetRecentProjects();
            RecentProjectsList.ItemsSource = recentProjects;

            // Show/hide empty state
            if (recentProjects.Count == 0)
            {
                RecentProjectsList.Visibility = Visibility.Collapsed;
                NoRecentProjectsText.Visibility = Visibility.Visible;
            }
            else
            {
                RecentProjectsList.Visibility = Visibility.Visible;
                NoRecentProjectsText.Visibility = Visibility.Collapsed;
            }
        }

        private void RecentProjectsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem != null)
            {
                OpenSelectedRecentProject();
            }
        }

        private void OpenSelectedRecentProject()
        {
            if (RecentProjectsList.SelectedItem is RecentProject project)
            {
                if (File.Exists(project.Path))
                {
                    try
                    {
                        var loadedProject = VizCodeProject.Load(project.Path);
                        RecentProjectsManager.AddProject(project.Path, loadedProject.ProjectFile.Name);
                        OpenMainWindow(loadedProject);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        RecentProjectsManager.RemoveProject(project.Path);
                        LoadRecentProjects();
                    }
                }
                else
                {
                    MessageBox.Show("Project file no longer exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RecentProjectsManager.RemoveProject(project.Path);
                    LoadRecentProjects();
                }
            }
        }

        private void NewProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var fullPath = dialog.FullPath;
                try
                {
                    // Logic to create project
                    var project = VizCodeProject.CreateNew(fullPath, dialog.ProjectName, dialog.SelectedLanguage);
                    OpenMainWindow(project);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Code2Viz Project (*.vizproj)|*.vizproj",
                Title = "Open Project"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var project = VizCodeProject.Load(dialog.FileName);
                    OpenMainWindow(project);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenMainWindow(VizCodeProject project)
        {
            // Add to recent projects
            RecentProjectsManager.AddProject(project.ProjectFilePath, project.ProjectFile.Name);

            var mainWindow = new MainWindow(project);
            mainWindow.Show();
            this.Close();
        }
    }
}
