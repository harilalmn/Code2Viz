using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Code2Viz
{
    public partial class NewProjectDialog : Window
    {
        public string ProjectName => ProjectNameBox.Text;
        public string ProjectLocation => LocationBox.Text;
        public string FullPath => Path.Combine(ProjectLocation, ProjectName);
        public Code2Viz.Project.ProjectLanguage SelectedLanguage => LanguageBox.SelectedIndex == 1 ? Code2Viz.Project.ProjectLanguage.FSharp : Code2Viz.Project.ProjectLanguage.CSharp;
        public bool OpenExistingProject { get; private set; }

        public NewProjectDialog()
        {
            InitializeComponent();
            ProjectNameBox.TextChanged += (s, e) => UpdatePath();
            LocationBox.TextChanged += (s, e) => UpdatePath();
            
            // Set default location to MyDocuments or similar if possible
            LocationBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Code2VizProjects");
        }

        private void UpdatePath()
        {
            try
            {
                FinalPathText.Text = Path.Combine(LocationBox.Text, ProjectNameBox.Text);
            }
            catch
            {
                FinalPathText.Text = "Invalid path";
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // WPF doesn't have a FolderBrowserDialog in standard namespaces, so usually we use WinForms or a trick.
            // Using OpenFileDialog with "Select Folder" hack or just simple folder picker if available.
            // For simplicity in this environment, I'll use OpenSaveFileDialog to pick a "dummy" file or just assume user types it.
            // Actually, let's use the Microsoft.Win32.OpenFileDialog with CheckFileExists=false to pick a folder name "Select Folder"
            
            var dialog = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|no.files"
            };
            
            if (dialog.ShowDialog() == true)
            {
                var path = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(path))
                {
                    LocationBox.Text = path;
                }
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show("Please enter a project name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectLocation))
            {
                MessageBox.Show("Please select a location.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if project already exists
            var projectPath = FullPath;
            if (Directory.Exists(projectPath))
            {
                // Check for project file
                var vizProjFile = Path.Combine(projectPath, $"{ProjectName}.vizproj");
                if (File.Exists(vizProjFile))
                {
                    var result = MessageBox.Show(
                        $"A project named '{ProjectName}' already exists at this location.\n\nDo you want to open the existing project instead?",
                        "Project Already Exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // User wants to open existing project
                        OpenExistingProject = true;
                        DialogResult = true;
                        Close();
                    }
                    return;
                }
                else if (Directory.GetFiles(projectPath).Length > 0 || Directory.GetDirectories(projectPath).Length > 0)
                {
                    // Directory exists with other files
                    var result = MessageBox.Show(
                        $"The folder '{projectPath}' already exists and contains files.\n\nDo you want to create the project here anyway? Existing files will NOT be deleted.",
                        "Folder Not Empty",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
