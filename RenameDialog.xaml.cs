using System.Windows;
using System.Windows.Input;

namespace Code2Viz
{
    public partial class RenameDialog : Window
    {
        public string NewName => NameTextBox.Text;

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OkButton_Click(sender, e);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("Please enter a valid name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
    }
}
