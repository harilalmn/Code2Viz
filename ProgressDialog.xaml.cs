using System.Windows;

namespace Code2Viz;

public partial class ProgressDialog : Window
{
    public ProgressDialog(string message = "Processing...")
    {
        InitializeComponent();
        MessageText.Text = message;

        // Set hourglass cursor
        Cursor = System.Windows.Input.Cursors.Wait;
    }

    public void UpdateMessage(string message)
    {
        MessageText.Text = message;
    }

    public void SetProgress(int current, int total)
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Maximum = total;
        ProgressBar.Value = current;
        MessageText.Text = $"Exporting frame {current} of {total}...";
    }
}
