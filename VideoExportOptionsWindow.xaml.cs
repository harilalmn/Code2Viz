using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Code2Viz;

public partial class VideoExportOptionsWindow : Window
{
    public Brush? SelectedBackground { get; private set; }
    public bool IncludeGrid { get; private set; }
    public double Duration { get; private set; } = 5.0;
    public int Fps { get; private set; } = 30;
    public uint Bitrate { get; private set; } = 5;
    public int OutputWidth { get; private set; } = 1920;
    public int OutputHeight { get; private set; } = 1080;
    public bool UseCanvasSize { get; private set; } = true;

    private int _canvasWidth = 1920;
    private int _canvasHeight = 1080;

    public VideoExportOptionsWindow()
    {
        InitializeComponent();
        SliderDuration.ValueChanged += (s, e) => UpdateDisplay();
        SliderFps.ValueChanged += (s, e) => UpdateDisplay();
        SliderBitrate.ValueChanged += (s, e) => UpdateDisplay();
        UpdateDisplay();
    }

    public void SetDuration(double duration)
    {
        Duration = duration;
        if (duration >= 1 && duration <= 60)
        {
            SliderDuration.Value = duration;
        }
        else
        {
            SliderDuration.Value = Math.Min(60, Math.Max(1, duration));
        }
        UpdateDisplay();
    }

    public void SetCanvasSize(int width, int height)
    {
        _canvasWidth = width;
        _canvasHeight = height;
        UpdateResolutionDisplay();
    }

    private void UpdateDisplay()
    {
        Duration = SliderDuration.Value;
        Fps = (int)SliderFps.Value;
        Bitrate = (uint)SliderBitrate.Value;

        TextDuration.Text = Duration.ToString("0");
        TextFps.Text = Fps.ToString();
        TextBitrate.Text = Bitrate.ToString();

        int totalFrames = (int)(Duration * Fps);
        double estimatedSizeMB = (Bitrate * Duration) / 8.0;
        TextFrameInfo.Text = $"Total frames: {totalFrames} | Estimated size: ~{estimatedSizeMB:F1} MB";
    }

    private void ComboResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateResolutionDisplay();
    }

    private void UpdateResolutionDisplay()
    {
        if (ComboResolution == null || TextWidth == null || TextHeight == null) return;

        var selectedItem = ComboResolution.SelectedItem as ComboBoxItem;
        var content = selectedItem?.Content?.ToString() ?? "";

        bool isCustom = content == "Custom";
        TextWidth.IsEnabled = isCustom;
        TextHeight.IsEnabled = isCustom;

        if (content.StartsWith("Canvas"))
        {
            TextWidth.Text = _canvasWidth.ToString();
            TextHeight.Text = _canvasHeight.ToString();
            TextResolutionInfo.Text = "(current)";
        }
        else if (content.StartsWith("720p"))
        {
            TextWidth.Text = "1280";
            TextHeight.Text = "720";
            TextResolutionInfo.Text = "HD";
        }
        else if (content.StartsWith("1080p"))
        {
            TextWidth.Text = "1920";
            TextHeight.Text = "1080";
            TextResolutionInfo.Text = "Full HD";
        }
        else if (content.StartsWith("4K"))
        {
            TextWidth.Text = "3840";
            TextHeight.Text = "2160";
            TextResolutionInfo.Text = "Ultra HD";
        }
        else if (content == "Custom")
        {
            TextResolutionInfo.Text = "";
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (RadioCanvas.IsChecked == true)
            SelectedBackground = null;
        else if (RadioWhite.IsChecked == true)
            SelectedBackground = Brushes.White;
        else if (RadioBlack.IsChecked == true)
            SelectedBackground = Brushes.Black;

        IncludeGrid = CheckShowGrid.IsChecked == true;
        Duration = SliderDuration.Value;
        Fps = (int)SliderFps.Value;
        Bitrate = (uint)SliderBitrate.Value;

        // Parse resolution
        var selectedItem = ComboResolution.SelectedItem as ComboBoxItem;
        var content = selectedItem?.Content?.ToString() ?? "";
        UseCanvasSize = content.StartsWith("Canvas");

        if (int.TryParse(TextWidth.Text, out int width) && int.TryParse(TextHeight.Text, out int height))
        {
            // Ensure even dimensions (required for H.264)
            OutputWidth = width - (width % 2);
            OutputHeight = height - (height % 2);

            if (OutputWidth < 64 || OutputHeight < 64)
            {
                MessageBox.Show("Resolution must be at least 64×64 pixels.", "Invalid Resolution",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (OutputWidth > 4096 || OutputHeight > 4096)
            {
                MessageBox.Show("Resolution cannot exceed 4096×4096 pixels.", "Invalid Resolution",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            MessageBox.Show("Please enter valid width and height values.", "Invalid Resolution",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
