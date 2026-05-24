using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Code2Viz.Project;

namespace Code2Viz
{
    public partial class WelcomeWindow : Window
    {
        private enum WelcomeMode { Code, Animate }

        public WelcomeWindow()
        {
            InitializeComponent();
            ApplyMode(WelcomeMode.Code);
        }

        private WelcomeMode CurrentMode =>
            AnimateModeRadio?.IsChecked == true ? WelcomeMode.Animate : WelcomeMode.Code;

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            ApplyMode(CurrentMode);
        }

        private void ApplyMode(WelcomeMode mode)
        {
            if (mode == WelcomeMode.Code)
            {
                RecentListHeader.Text = "Recent Projects";
                NoRecentProjectsText.Text = "No recent projects";
                NewProjectBtn.Tag = "Create a new project";
                OpenProjectBtn.Tag = "Open a local project";
                LoadRecentProjects();
            }
            else
            {
                RecentListHeader.Text = "Recent Animation Files";
                NoRecentProjectsText.Text = "No recent animation files";
                NewProjectBtn.Tag = "Create a new animation";
                OpenProjectBtn.Tag = "Open an animation file";
                LoadRecentAnimations();
            }
        }

        // ── Recent (Code) ─────────────────────────────────────────────────────

        private void LoadRecentProjects()
        {
            var recentProjects = RecentProjectsManager.GetRecentProjects();
            RecentProjectsList.ItemsSource = recentProjects;

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

        // ── Recent (Animate) ──────────────────────────────────────────────────

        private void LoadRecentAnimations()
        {
            var recentAnimations = RecentAnimationsManager.GetRecentAnimations();
            RecentProjectsList.ItemsSource = recentAnimations;

            if (recentAnimations.Count == 0)
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

        // ── Recent list click ─────────────────────────────────────────────────

        private void RecentProjectsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem == null) return;

            if (CurrentMode == WelcomeMode.Code)
                OpenSelectedRecentProject();
            else
                OpenSelectedRecentAnimation();
        }

        private void OpenSelectedRecentProject()
        {
            if (RecentProjectsList.SelectedItem is not RecentProject project) return;

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

        private void OpenSelectedRecentAnimation()
        {
            if (RecentProjectsList.SelectedItem is not RecentAnimation animation) return;

            if (File.Exists(animation.Path))
            {
                RecentAnimationsManager.AddAnimation(animation.Path);
                LaunchAnimator(animation.Path);
            }
            else
            {
                MessageBox.Show("Animation file no longer exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                RecentAnimationsManager.RemoveAnimation(animation.Path);
                LoadRecentAnimations();
            }
        }

        // ── New / Open buttons ────────────────────────────────────────────────

        private void NewProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentMode == WelcomeMode.Code)
                NewProject();
            else
                NewAnimation();
        }

        private void OpenProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentMode == WelcomeMode.Code)
                OpenProject();
            else
                OpenAnimation();
        }

        private void NewProject()
        {
            var dialog = new NewProjectDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var fullPath = dialog.FullPath;
                try
                {
                    var project = VizCodeProject.CreateNew(fullPath, dialog.ProjectName);
                    OpenMainWindow(project);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenProject()
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

        private void NewAnimation()
        {
            LaunchAnimator(null);
        }

        private void OpenAnimation()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
                Title = "Open Animation",
                DefaultExt = ".cs"
            };

            if (dialog.ShowDialog() == true)
            {
                RecentAnimationsManager.AddAnimation(dialog.FileName);
                LaunchAnimator(dialog.FileName);
            }
        }

        private void OpenMainWindow(VizCodeProject project)
        {
            RecentProjectsManager.AddProject(project.ProjectFilePath, project.ProjectFile.Name);

            var mainWindow = new MainWindow(project);
            mainWindow.Show();
            this.Close();
        }

        private void LaunchAnimator(string? filePath)
        {
            var animatorPath = FindAnimatorExe();
            if (animatorPath == null)
            {
                MessageBox.Show(
                    "Could not locate Animator.exe. Build the Animator project first.",
                    "Animator", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = animatorPath,
                    UseShellExecute = true
                };
                if (!string.IsNullOrEmpty(filePath))
                    psi.ArgumentList.Add(filePath);

                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Animator: {ex.Message}",
                    "Animator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? FindAnimatorExe()
        {
            // Installed layout: {app}\Code2Viz.exe + {app}\Animator\Animator.exe
            // Dev layout:       {solution}\bin\{Config}\{TFM}\Code2Viz.exe + {solution}\Animator\bin\{Config}\{TFM}\Animator.exe
            var thisDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(thisDir, "Animator", "Animator.exe")),
                Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "Animator", "bin", "Debug", "net9.0-windows", "Animator.exe")),
                Path.GetFullPath(Path.Combine(thisDir, "..", "..", "..", "Animator", "bin", "Release", "net9.0-windows", "Animator.exe")),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }
    }
}
