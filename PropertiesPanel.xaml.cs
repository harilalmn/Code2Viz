using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Code2Viz.Geometry;

namespace Code2Viz;

public partial class PropertiesPanel : UserControl
{
    private List<Shape> _selectedShapes = new();
    private bool _isUpdating;

    /// <summary>Raised when a shape property is changed via the panel.</summary>
    public event EventHandler<ShapePropertyChangedEventArgs>? ShapePropertyChanged;

    /// <summary>Raised when the user wants to dock the panel.</summary>
    public event EventHandler? DockRequested;

    /// <summary>Raised when the user wants to float the panel.</summary>
    public event EventHandler? FloatRequested;

    /// <summary>Whether this panel is currently docked.</summary>
    public bool IsDocked { get; set; }

    public PropertiesPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the panel to show properties of the given shapes.
    /// </summary>
    public void UpdateSelection(List<Shape> shapes)
    {
        _selectedShapes = shapes ?? new();
        RefreshUI();
    }

    /// <summary>
    /// Refreshes the panel display without changing the selection.
    /// </summary>
    public void RefreshUI()
    {
        _isUpdating = true;
        try
        {
            if (_selectedShapes.Count == 0)
            {
                NoSelectionText.Visibility = Visibility.Visible;
                MultiSelectionText.Visibility = Visibility.Collapsed;
                ShapeInfoSection.Visibility = Visibility.Collapsed;
                return;
            }

            if (_selectedShapes.Count > 1)
            {
                NoSelectionText.Visibility = Visibility.Collapsed;
                MultiSelectionText.Visibility = Visibility.Visible;
                MultiSelectionText.Text = $"{_selectedShapes.Count} shapes selected";
                ShapeInfoSection.Visibility = Visibility.Visible;

                // Show common properties
                ShapeTypeLabel.Text = "(multiple)";
                ShapeIdLabel.Text = "";
                ShapeNameBox.Text = "";
                ShapeNameBox.IsEnabled = false;

                BuildGeometrySection(null);
                UpdateStyleSection(_selectedShapes);
                return;
            }

            // Single selection
            var shape = _selectedShapes[0];
            NoSelectionText.Visibility = Visibility.Collapsed;
            MultiSelectionText.Visibility = Visibility.Collapsed;
            ShapeInfoSection.Visibility = Visibility.Visible;

            ShapeTypeLabel.Text = shape.GetType().Name;
            ShapeIdLabel.Text = shape.Id.ToString();
            ShapeNameBox.Text = shape.Name ?? "";
            ShapeNameBox.IsEnabled = true;

            BuildGeometrySection(shape);
            UpdateStyleSection(new List<Shape> { shape });
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void BuildGeometrySection(Shape? shape)
    {
        GeometrySection.Children.Clear();

        if (shape == null) return;

        var props = GetGeometryProperties(shape);
        foreach (var (label, value, setter) in props)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Margin = new Thickness(0, 2, 0, 2);

            var lbl = new TextBlock
            {
                Text = label,
                Foreground = (Brush)FindResource("MutedForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var tb = new TextBox
            {
                Text = value,
                Background = (Brush)FindResource("BackgroundBrush"),
                Foreground = (Brush)FindResource("ForegroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = setter
            };
            tb.LostFocus += GeometryTextBox_LostFocus;
            tb.KeyDown += PropTextBox_KeyDown;
            Grid.SetColumn(tb, 1);
            row.Children.Add(tb);

            GeometrySection.Children.Add(row);
        }
    }

    private List<(string label, string value, Action<string> setter)> GetGeometryProperties(Shape shape)
    {
        var props = new List<(string, string, Action<string>)>();
        string F(double v) => v.ToString("F2");

        switch (shape)
        {
            case VPoint pt:
                props.Add(("X", F(pt.X), v => { if (double.TryParse(v, out var d)) { pt.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Y", F(pt.Y), v => { if (double.TryParse(v, out var d)) { pt.Y = d; RaisePropertyChanged(shape); } }));
                break;

            case VLine ln:
                props.Add(("Start X", F(ln.Start.X), v => { if (double.TryParse(v, out var d)) { ln.Start.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Start Y", F(ln.Start.Y), v => { if (double.TryParse(v, out var d)) { ln.Start.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("End X", F(ln.End.X), v => { if (double.TryParse(v, out var d)) { ln.End.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("End Y", F(ln.End.Y), v => { if (double.TryParse(v, out var d)) { ln.End.Y = d; RaisePropertyChanged(shape); } }));
                break;

            case VCircle c:
                props.Add(("Center X", F(c.Center.X), v => { if (double.TryParse(v, out var d)) { c.Center.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Center Y", F(c.Center.Y), v => { if (double.TryParse(v, out var d)) { c.Center.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("Radius", F(c.Radius), v => { if (double.TryParse(v, out var d) && d > 0) { c.Radius = d; RaisePropertyChanged(shape); } }));
                break;

            case VRectangle rect:
                props.Add(("X", F(rect.Corner.X), v => { if (double.TryParse(v, out var d)) { rect.Corner = VPoint.Internal(d, rect.Corner.Y); RaisePropertyChanged(shape); } }));
                props.Add(("Y", F(rect.Corner.Y), v => { if (double.TryParse(v, out var d)) { rect.Corner = VPoint.Internal(rect.Corner.X, d); RaisePropertyChanged(shape); } }));
                props.Add(("Width", F(rect.Width), v => { if (double.TryParse(v, out var d) && d > 0) { rect.Width = d; RaisePropertyChanged(shape); } }));
                props.Add(("Height", F(rect.Height), v => { if (double.TryParse(v, out var d) && d > 0) { rect.Height = d; RaisePropertyChanged(shape); } }));
                break;

            case VArc arc:
                props.Add(("Center X", F(arc.Center.X), v => { if (double.TryParse(v, out var d)) { arc.Center.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Center Y", F(arc.Center.Y), v => { if (double.TryParse(v, out var d)) { arc.Center.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("Radius", F(arc.Radius), v => { if (double.TryParse(v, out var d) && d > 0) { arc.Radius = d; RaisePropertyChanged(shape); } }));
                props.Add(("Start °", F(arc.StartAngle), v => { if (double.TryParse(v, out var d)) { arc.StartAngle = d; RaisePropertyChanged(shape); } }));
                props.Add(("End °", F(arc.EndAngle), v => { if (double.TryParse(v, out var d)) { arc.EndAngle = d; RaisePropertyChanged(shape); } }));
                break;

            case VEllipse el:
                props.Add(("Center X", F(el.Center.X), v => { if (double.TryParse(v, out var d)) { el.Center.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Center Y", F(el.Center.Y), v => { if (double.TryParse(v, out var d)) { el.Center.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("Radius X", F(el.RadiusX), v => { if (double.TryParse(v, out var d) && d > 0) { el.RadiusX = d; RaisePropertyChanged(shape); } }));
                props.Add(("Radius Y", F(el.RadiusY), v => { if (double.TryParse(v, out var d) && d > 0) { el.RadiusY = d; RaisePropertyChanged(shape); } }));
                break;

            case VBezier bz:
                props.Add(("P0 X", F(bz.P0.X), v => { if (double.TryParse(v, out var d)) { bz.P0.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P0 Y", F(bz.P0.Y), v => { if (double.TryParse(v, out var d)) { bz.P0.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("P1 X", F(bz.P1.X), v => { if (double.TryParse(v, out var d)) { bz.P1.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P1 Y", F(bz.P1.Y), v => { if (double.TryParse(v, out var d)) { bz.P1.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("P2 X", F(bz.P2.X), v => { if (double.TryParse(v, out var d)) { bz.P2.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P2 Y", F(bz.P2.Y), v => { if (double.TryParse(v, out var d)) { bz.P2.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("P3 X", F(bz.P3.X), v => { if (double.TryParse(v, out var d)) { bz.P3.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P3 Y", F(bz.P3.Y), v => { if (double.TryParse(v, out var d)) { bz.P3.Y = d; RaisePropertyChanged(shape); } }));
                break;

            case VArrow arrow:
                props.Add(("Start X", F(arrow.Start.X), v => { if (double.TryParse(v, out var d)) { arrow.Start.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Start Y", F(arrow.Start.Y), v => { if (double.TryParse(v, out var d)) { arrow.Start.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("End X", F(arrow.End.X), v => { if (double.TryParse(v, out var d)) { arrow.End.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("End Y", F(arrow.End.Y), v => { if (double.TryParse(v, out var d)) { arrow.End.Y = d; RaisePropertyChanged(shape); } }));
                break;

            case VText txt:
                props.Add(("X", F(txt.Location.X), v => { if (double.TryParse(v, out var d)) { txt.Location.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("Y", F(txt.Location.Y), v => { if (double.TryParse(v, out var d)) { txt.Location.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("Height", F(txt.Height), v => { if (double.TryParse(v, out var d) && d > 0) { txt.Height = d; RaisePropertyChanged(shape); } }));
                break;

            case VDimension dim:
                props.Add(("P1 X", F(dim.Point1.X), v => { if (double.TryParse(v, out var d)) { dim.Point1.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P1 Y", F(dim.Point1.Y), v => { if (double.TryParse(v, out var d)) { dim.Point1.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("P2 X", F(dim.Point2.X), v => { if (double.TryParse(v, out var d)) { dim.Point2.X = d; RaisePropertyChanged(shape); } }));
                props.Add(("P2 Y", F(dim.Point2.Y), v => { if (double.TryParse(v, out var d)) { dim.Point2.Y = d; RaisePropertyChanged(shape); } }));
                props.Add(("Offset", F(dim.Offset), v => { if (double.TryParse(v, out var d)) { dim.Offset = d; RaisePropertyChanged(shape); } }));
                break;

            case VPolygon poly:
                props.Add(("Points", poly.Points.Count.ToString(), _ => { }));
                break;

            case VPolyline pl:
                props.Add(("Points", pl.Points.Count.ToString(), _ => { }));
                break;

            case VSpline sp:
                props.Add(("Points", sp.ControlPoints.Count.ToString(), _ => { }));
                break;
        }

        return props;
    }

    private void UpdateStyleSection(List<Shape> shapes)
    {
        if (shapes.Count == 0) return;

        var first = shapes[0];

        // Color
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(first.Color);
            ColorButton.Background = new SolidColorBrush(color);
        }
        catch
        {
            ColorButton.Background = Brushes.Transparent;
        }
        ColorLabel.Text = first.Color;

        // Fill Color
        try
        {
            var fillColor = (Color)ColorConverter.ConvertFromString(first.FillColor);
            FillColorButton.Background = new SolidColorBrush(fillColor);
        }
        catch
        {
            FillColorButton.Background = Brushes.Transparent;
        }
        FillColorLabel.Text = first.FillColor;

        // Line Weight
        LineWeightSlider.Value = first.LineWeight;
        LineWeightLabel.Text = first.LineWeight.ToString("F1");

        // Opacity (shape stores 0.0-1.0, slider shows 0-100)
        OpacitySlider.Value = first.Opacity * 100.0;
        OpacityLabel.Text = $"{first.Opacity * 100.0:F0}%";

        // Visible
        VisibleCheckBox.IsChecked = first.IsVisible;
    }

    private void GeometryTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        if (sender is TextBox tb && tb.Tag is Action<string> setter)
        {
            setter(tb.Text);
        }
    }

    private void PropTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox tb)
            {
                // Move focus away to trigger LostFocus
                var parent = tb.Parent as FrameworkElement;
                parent?.Focus();

                if (tb == ShapeNameBox)
                {
                    ShapeNameBox_LostFocus(sender, e);
                }
                else if (tb.Tag is Action<string> setter)
                {
                    setter(tb.Text);
                }
            }
        }
    }

    private void ShapeNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedShapes.Count != 1) return;
        var shape = _selectedShapes[0];
        var newName = ShapeNameBox.Text?.Trim() ?? "";
        if (shape.Name != newName)
        {
            var oldName = shape.Name;
            shape.Name = newName;
            RaisePropertyChanged(shape, "Name", oldName);
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedShapes.Count == 0) return;

        var dialog = new ColorPickerDialog(_selectedShapes[0].Color);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            foreach (var shape in _selectedShapes)
            {
                shape.Color = dialog.SelectedColor;
            }
            RaisePropertyChanged(_selectedShapes[0], "Color");
            RefreshUI();
        }
    }

    private void FillColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedShapes.Count == 0) return;

        var dialog = new ColorPickerDialog(_selectedShapes[0].FillColor);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            foreach (var shape in _selectedShapes)
            {
                shape.FillColor = dialog.SelectedColor;
            }
            RaisePropertyChanged(_selectedShapes[0], "FillColor");
            RefreshUI();
        }
    }

    private void LineWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || _selectedShapes.Count == 0) return;
        foreach (var shape in _selectedShapes)
        {
            shape.LineWeight = e.NewValue;
        }
        if (LineWeightLabel != null)
            LineWeightLabel.Text = e.NewValue.ToString("F1");
        RaisePropertyChanged(_selectedShapes[0], "LineWeight");
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || _selectedShapes.Count == 0) return;
        double normalizedOpacity = e.NewValue / 100.0;
        foreach (var shape in _selectedShapes)
        {
            shape.Opacity = normalizedOpacity;
        }
        if (OpacityLabel != null)
            OpacityLabel.Text = $"{e.NewValue:F0}%";
        RaisePropertyChanged(_selectedShapes[0], "Opacity");
    }

    private void VisibleCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedShapes.Count == 0) return;
        bool visible = VisibleCheckBox.IsChecked == true;
        foreach (var shape in _selectedShapes)
        {
            if (visible) shape.Show(); else shape.Hide();
        }
        RaisePropertyChanged(_selectedShapes[0], "IsVisible");
    }

    private void DockFloatButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsDocked)
            FloatRequested?.Invoke(this, EventArgs.Empty);
        else
            DockRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the dock/float button text.</summary>
    public void UpdateDockFloatButton()
    {
        DockFloatButton.Content = IsDocked ? "Float" : "Dock";
    }

    private void RaisePropertyChanged(Shape shape, string? propertyName = null, string? oldValue = null)
    {
        ShapePropertyChanged?.Invoke(this, new ShapePropertyChangedEventArgs(shape, propertyName, oldValue));
    }
}

public class ShapePropertyChangedEventArgs : EventArgs
{
    public Shape Shape { get; }
    public string? PropertyName { get; }
    public string? OldValue { get; }

    public ShapePropertyChangedEventArgs(Shape shape, string? propertyName = null, string? oldValue = null)
    {
        Shape = shape;
        PropertyName = propertyName;
        OldValue = oldValue;
    }
}
