using System.Windows;

namespace Animator.Export;

public partial class AnimatorExportOptionsWindow : Window
{
    public double Duration { get; private set; } = 5.0;
    public int Fps { get; private set; } = 30;

    public AnimatorExportOptionsWindow(string title)
    {
        InitializeComponent();
        Title = title;
        HeaderText.Text = title;
        SliderDuration.ValueChanged += (s, e) => UpdateDisplay();
        SliderFps.ValueChanged += (s, e) => UpdateDisplay();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        Duration = SliderDuration.Value;
        Fps = (int)SliderFps.Value;
        TextDuration.Text = Duration.ToString("0");
        TextFps.Text = Fps.ToString();
        TextFrameInfo.Text = $"Total frames: {(int)(Duration * Fps)}";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Duration = SliderDuration.Value;
        Fps = (int)SliderFps.Value;
        DialogResult = true;
        Close();
    }
}
