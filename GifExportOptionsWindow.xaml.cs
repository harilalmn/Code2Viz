using System.Windows;
using System.Windows.Media;

namespace Code2Viz;

public partial class GifExportOptionsWindow : Window
{
    public Brush? SelectedBackground { get; private set; }
    public bool IncludeGrid { get; private set; }
    public double Duration { get; private set; } = 5.0;
    public int Fps { get; private set; } = 15;

    public GifExportOptionsWindow()
    {
        InitializeComponent();
        SliderDuration.ValueChanged += (s, e) => UpdateDisplay();
        SliderFps.ValueChanged += (s, e) => UpdateDisplay();
        UpdateDisplay();
    }

    public void SetDuration(double duration)
    {
        Duration = duration;
        if (duration >= 1 && duration <= 30)
        {
            SliderDuration.Value = duration;
        }
        else
        {
            SliderDuration.Value = Math.Min(30, Math.Max(1, duration));
        }
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
        if (RadioCanvas.IsChecked == true)
            SelectedBackground = null; // Use current canvas brush
        else if (RadioWhite.IsChecked == true)
            SelectedBackground = Brushes.White;
        else if (RadioBlack.IsChecked == true)
            SelectedBackground = Brushes.Black;

        IncludeGrid = CheckShowGrid.IsChecked == true;
        Duration = SliderDuration.Value;
        Fps = (int)SliderFps.Value;

        DialogResult = true;
        Close();
    }
}
