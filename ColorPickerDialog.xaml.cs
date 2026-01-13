using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Code2Viz
{
    public partial class ColorPickerDialog : Window
    {
        public string SelectedColor { get; private set; } = string.Empty;

        // HSV values (0-360 for H, 0-1 for S and V)
        private double _hue = 0;
        private double _saturation = 1;
        private double _value = 1;

        private bool _isUpdating = false;
        private bool _isDraggingColor = false;
        private bool _isDraggingHue = false;

        public ColorPickerDialog(string initialColor = "")
        {
            InitializeComponent();
            Loaded += ColorPickerDialog_Loaded;

            if (!string.IsNullOrWhiteSpace(initialColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(initialColor);
                    SetColorFromRgb(color.R, color.G, color.B);
                }
                catch
                {
                    // Default to red
                    _hue = 0;
                    _saturation = 1;
                    _value = 1;
                }
            }
        }

        private void ColorPickerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAllFromHsv();
            UpdateMarkers();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorTag)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorTag);
                    SetColorFromRgb(color.R, color.G, color.B);
                    UpdateAllFromHsv();
                    UpdateMarkers();
                }
                catch { }
            }
        }

        #region Color Canvas (Saturation/Value)

        private void ColorCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingColor = true;
            ColorCanvas.CaptureMouse();
            UpdateColorFromCanvas(e.GetPosition(ColorCanvas));
        }

        private void ColorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingColor && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateColorFromCanvas(e.GetPosition(ColorCanvas));
            }
        }

        private void ColorCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingColor = false;
            ColorCanvas.ReleaseMouseCapture();
        }

        private void UpdateColorFromCanvas(Point pos)
        {
            double width = ColorCanvas.ActualWidth;
            double height = ColorCanvas.ActualHeight;

            if (width <= 0 || height <= 0) return;

            _saturation = Math.Clamp(pos.X / width, 0, 1);
            _value = Math.Clamp(1 - (pos.Y / height), 0, 1);

            UpdateAllFromHsv();
            UpdateMarkers();
        }

        #endregion

        #region Hue Canvas

        private void HueCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = true;
            HueCanvas.CaptureMouse();
            UpdateHueFromCanvas(e.GetPosition(HueCanvas));
        }

        private void HueCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHue && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateHueFromCanvas(e.GetPosition(HueCanvas));
            }
        }

        private void HueCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingHue = false;
            HueCanvas.ReleaseMouseCapture();
        }

        private void UpdateHueFromCanvas(Point pos)
        {
            double height = HueCanvas.ActualHeight;
            if (height <= 0) return;

            _hue = Math.Clamp((pos.Y / height) * 360, 0, 360);

            UpdateHueGradient();
            UpdateAllFromHsv();
            UpdateMarkers();
        }

        #endregion

        #region Update Methods

        private void UpdateMarkers()
        {
            // Color marker position
            double canvasWidth = ColorCanvas.ActualWidth;
            double canvasHeight = ColorCanvas.ActualHeight;

            if (canvasWidth > 0 && canvasHeight > 0)
            {
                double markerX = _saturation * canvasWidth - 6;
                double markerY = (1 - _value) * canvasHeight - 6;
                System.Windows.Controls.Canvas.SetLeft(ColorMarker, markerX);
                System.Windows.Controls.Canvas.SetTop(ColorMarker, markerY);
            }

            // Hue marker position
            double hueHeight = HueCanvas.ActualHeight;
            if (hueHeight > 0)
            {
                double hueY = (_hue / 360) * hueHeight - 2;
                System.Windows.Controls.Canvas.SetTop(HueMarker, hueY);
            }
        }

        private void UpdateHueGradient()
        {
            var hueColor = HsvToRgb(_hue, 1, 1);
            HueGradientStop.Color = hueColor;
        }

        private void UpdateAllFromHsv()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                UpdateHueGradient();

                var color = HsvToRgb(_hue, _saturation, _value);
                PreviewRect.Fill = new SolidColorBrush(color);

                // Update text boxes
                HueBox.Text = ((int)_hue).ToString();
                SatBox.Text = ((int)(_saturation * 100)).ToString();
                LumBox.Text = ((int)(_value * 100)).ToString();

                RedBox.Text = color.R.ToString();
                GreenBox.Text = color.G.ToString();
                BlueBox.Text = color.B.ToString();

                HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void SetColorFromRgb(byte r, byte g, byte b)
        {
            RgbToHsv(r, g, b, out _hue, out _saturation, out _value);
        }

        #endregion

        #region Text Changed Handlers

        private void HSL_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (int.TryParse(HueBox.Text, out int h) &&
                int.TryParse(SatBox.Text, out int s) &&
                int.TryParse(LumBox.Text, out int l))
            {
                _hue = Math.Clamp(h, 0, 360);
                _saturation = Math.Clamp(s / 100.0, 0, 1);
                _value = Math.Clamp(l / 100.0, 0, 1);

                _isUpdating = true;
                try
                {
                    UpdateHueGradient();
                    var color = HsvToRgb(_hue, _saturation, _value);
                    PreviewRect.Fill = new SolidColorBrush(color);

                    RedBox.Text = color.R.ToString();
                    GreenBox.Text = color.G.ToString();
                    BlueBox.Text = color.B.ToString();
                    HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                    UpdateMarkers();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void RGB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (int.TryParse(RedBox.Text, out int r) &&
                int.TryParse(GreenBox.Text, out int g) &&
                int.TryParse(BlueBox.Text, out int b))
            {
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                SetColorFromRgb((byte)r, (byte)g, (byte)b);

                _isUpdating = true;
                try
                {
                    UpdateHueGradient();
                    var color = Color.FromRgb((byte)r, (byte)g, (byte)b);
                    PreviewRect.Fill = new SolidColorBrush(color);

                    HueBox.Text = ((int)_hue).ToString();
                    SatBox.Text = ((int)(_saturation * 100)).ToString();
                    LumBox.Text = ((int)(_value * 100)).ToString();
                    HexBox.Text = $"#{r:X2}{g:X2}{b:X2}";

                    UpdateMarkers();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void Hex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(HexBox.Text);
                SetColorFromRgb(color.R, color.G, color.B);

                _isUpdating = true;
                try
                {
                    UpdateHueGradient();
                    PreviewRect.Fill = new SolidColorBrush(color);

                    HueBox.Text = ((int)_hue).ToString();
                    SatBox.Text = ((int)(_saturation * 100)).ToString();
                    LumBox.Text = ((int)(_value * 100)).ToString();

                    RedBox.Text = color.R.ToString();
                    GreenBox.Text = color.G.ToString();
                    BlueBox.Text = color.B.ToString();

                    UpdateMarkers();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
            catch
            {
                // Invalid hex, ignore
            }
        }

        #endregion

        #region Color Conversion

        private static Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)
            {
                h = 0;
            }
            else if (max == rd)
            {
                h = 60 * (((gd - bd) / delta) % 6);
            }
            else if (max == gd)
            {
                h = 60 * (((bd - rd) / delta) + 2);
            }
            else
            {
                h = 60 * (((rd - gd) / delta) + 4);
            }

            if (h < 0) h += 360;
        }

        #endregion

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = HexBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
