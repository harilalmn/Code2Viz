using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using C2VGeometry;

namespace Code2Viz;

public partial class PropertiesPanel : UserControl
{
    private List<Shape> _selectedShapes = new();
    private bool _isUpdating;

    /// <summary>Raised when a shape property is changed via the panel.</summary>
    public event EventHandler<ShapePropertyChangedEventArgs>? ShapePropertyChanged;

    /// <summary>
    /// Raised continuously while a flex-slider is being dragged. Hosts should respond by
    /// redrawing the canvas ONLY (no source-code sync) so dragging stays smooth and doesn't
    /// thrash the editor. The single code-sync happens on commit via <see cref="ShapePropertyChanged"/>.
    /// </summary>
    public event EventHandler<ShapePropertyChangedEventArgs>? ShapeLivePreview;

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

        foreach (var gp in GetGeometryProperties(shape))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Margin = new Thickness(0, 2, 0, 2);

            var lbl = new TextBlock
            {
                Text = gp.Label,
                Foreground = (Brush)FindResource("MutedForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var tb = new TextBox
            {
                Text = gp.Value,
                Background = (Brush)FindResource("BackgroundBrush"),
                Foreground = (Brush)FindResource("ForegroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = gp.SetText
            };
            tb.LostFocus += GeometryTextBox_LostFocus;
            tb.KeyDown += PropTextBox_KeyDown;
            Grid.SetColumn(tb, 1);
            row.Children.Add(tb);

            GeometrySection.Children.Add(row);

            // "Flex" slider row for numeric properties — drag to preview the value live.
            if (gp.Numeric is double nv && gp.SetNumber is Action<double> setNumber)
                GeometrySection.Children.Add(BuildFlexSliderRow(shape, gp, nv, setNumber, tb));
        }
    }

    /// <summary>Descriptor for one Properties-panel geometry row.</summary>
    private sealed class GeoProp
    {
        public string Label = "";
        public string Value = "";
        public Action<string> SetText = _ => { };
        /// <summary>Current numeric value, or null for non-numeric / read-only rows (no slider).</summary>
        public double? Numeric;
        /// <summary>Applies a numeric value WITHOUT committing (used by the slider during a drag).</summary>
        public Action<double>? SetNumber;
        public bool IsInteger;
        public bool NonNegative;
    }

    private List<GeoProp> GetGeometryProperties(Shape shape)
    {
        var props = new List<GeoProp>();
        string F(double v) => v.ToString("F2");

        // Numeric (sliderable) property. `set` mutates the shape; `positive` rejects <= 0,
        // `nonNegative` clamps to >= 0, `integer` rounds to whole numbers.
        void Num(string label, double value, Action<double> set,
                 bool positive = false, bool integer = false, bool nonNegative = false)
        {
            bool Apply(double d)
            {
                if (integer) d = Math.Round(d);
                if (positive && d <= 0) return false;
                if (nonNegative && d < 0) d = 0;
                set(d);
                return true;
            }

            props.Add(new GeoProp
            {
                Label = label,
                Value = integer ? ((long)Math.Round(value)).ToString() : F(value),
                Numeric = value,
                IsInteger = integer,
                NonNegative = positive || nonNegative,
                SetNumber = d => Apply(d),
                SetText = v => { if (double.TryParse(v, out var d) && Apply(d)) RaisePropertyChanged(shape); }
            });
        }

        // Non-numeric / read-only text row (no slider).
        void Txt(string label, string value, Action<string>? set)
        {
            props.Add(new GeoProp
            {
                Label = label,
                Value = value,
                SetText = set == null ? (_ => { }) : (v => { set(v); RaisePropertyChanged(shape); })
            });
        }

        switch (shape)
        {
            case VPoint pt:
                Num("X", pt.X, d => pt.X = d);
                Num("Y", pt.Y, d => pt.Y = d);
                break;

            case VLine ln:
                Num("Start X", ln.Start.X, d => ln.Start = new VXYZ(d, ln.Start.Y));
                Num("Start Y", ln.Start.Y, d => ln.Start = new VXYZ(ln.Start.X, d));
                Num("End X", ln.End.X, d => ln.End = new VXYZ(d, ln.End.Y));
                Num("End Y", ln.End.Y, d => ln.End = new VXYZ(ln.End.X, d));
                break;

            case VCircle c:
                Num("Center X", c.Center.X, d => c.Center = new VXYZ(d, c.Center.Y));
                Num("Center Y", c.Center.Y, d => c.Center = new VXYZ(c.Center.X, d));
                Num("Radius", c.Radius, d => c.Radius = d, positive: true);
                break;

            case VRectangle rect:
                Num("X", rect.Corner.X, d => rect.Corner = new VXYZ(d, rect.Corner.Y));
                Num("Y", rect.Corner.Y, d => rect.Corner = new VXYZ(rect.Corner.X, d));
                Num("Width", rect.Width, d => rect.Width = d, positive: true);
                Num("Height", rect.Height, d => rect.Height = d, positive: true);
                break;

            case VArc arc:
                Num("Center X", arc.Center.X, d => arc.Center = new VXYZ(d, arc.Center.Y));
                Num("Center Y", arc.Center.Y, d => arc.Center = new VXYZ(arc.Center.X, d));
                Num("Radius", arc.Radius, d => arc.Radius = d, positive: true);
                Num("Start °", arc.StartAngle, d => arc.StartAngle = d);
                Num("End °", arc.EndAngle, d => arc.EndAngle = d);
                break;

            case VEllipse el:
                Num("Center X", el.Center.X, d => el.Center = new VXYZ(d, el.Center.Y));
                Num("Center Y", el.Center.Y, d => el.Center = new VXYZ(el.Center.X, d));
                Num("Radius X", el.RadiusX, d => el.RadiusX = d, positive: true);
                Num("Radius Y", el.RadiusY, d => el.RadiusY = d, positive: true);
                break;

            case VBezier bz:
                Num("P0 X", bz.P0.X, d => bz.P0 = new VXYZ(d, bz.P0.Y));
                Num("P0 Y", bz.P0.Y, d => bz.P0 = new VXYZ(bz.P0.X, d));
                Num("P1 X", bz.P1.X, d => bz.P1 = new VXYZ(d, bz.P1.Y));
                Num("P1 Y", bz.P1.Y, d => bz.P1 = new VXYZ(bz.P1.X, d));
                Num("P2 X", bz.P2.X, d => bz.P2 = new VXYZ(d, bz.P2.Y));
                Num("P2 Y", bz.P2.Y, d => bz.P2 = new VXYZ(bz.P2.X, d));
                Num("P3 X", bz.P3.X, d => bz.P3 = new VXYZ(d, bz.P3.Y));
                Num("P3 Y", bz.P3.Y, d => bz.P3 = new VXYZ(bz.P3.X, d));
                break;

            case VArrow arrow:
                Num("Start X", arrow.Start.X, d => arrow.Start = new VXYZ(d, arrow.Start.Y));
                Num("Start Y", arrow.Start.Y, d => arrow.Start = new VXYZ(arrow.Start.X, d));
                Num("End X", arrow.End.X, d => arrow.End = new VXYZ(d, arrow.End.Y));
                Num("End Y", arrow.End.Y, d => arrow.End = new VXYZ(arrow.End.X, d));
                break;

            case VText txt:
                Num("X", txt.Location.X, d => txt.Location = new VXYZ(d, txt.Location.Y));
                Num("Y", txt.Location.Y, d => txt.Location = new VXYZ(txt.Location.X, d));
                Num("Height", txt.Height, d => txt.Height = d, positive: true);
                break;

            case VDimension dim:
                Num("P1 X", dim.Point1.X, d => dim.Point1 = new VXYZ(d, dim.Point1.Y));
                Num("P1 Y", dim.Point1.Y, d => dim.Point1 = new VXYZ(dim.Point1.X, d));
                Num("P2 X", dim.Point2.X, d => dim.Point2 = new VXYZ(d, dim.Point2.Y));
                Num("P2 Y", dim.Point2.Y, d => dim.Point2 = new VXYZ(dim.Point2.X, d));
                Num("Offset", dim.Offset, d => dim.Offset = d);
                Num("Arrow Size", dim.ArrowSize, d => dim.ArrowSize = d, positive: true);
                Num("Text Height", dim.TextHeight, d => dim.TextHeight = d, positive: true);
                Num("Decimal Places", dim.DecimalPlaces, d => dim.DecimalPlaces = (int)d, integer: true, nonNegative: true);
                Num("Extend Beyond", dim.ExtendBeyondDimLines, d => dim.ExtendBeyondDimLines = d);
                Num("Offset From Origin", dim.OffsetFromOrigin, d => dim.OffsetFromOrigin = d);
                Txt("Prefix", dim.Prefix, v => dim.Prefix = v);
                Txt("Suffix", dim.Suffix, v => dim.Suffix = v);
                break;

            case VPolygon poly:
                Txt("Points", poly.Points.Count.ToString(), null);
                break;

            case VPolyline pl:
                Txt("Points", pl.Points.Count.ToString(), null);
                break;

            case VSpline sp:
                Txt("Points", sp.ControlPoints.Count.ToString(), null);
                break;
        }

        return props;
    }

    /// <summary>
    /// Builds the "flex" slider row shown under a numeric property: [min] [====slider====] [max].
    /// Dragging mutates the shape and fires a canvas-only live preview; releasing commits once
    /// (canvas + source-code sync) via <see cref="RaisePropertyChanged"/>. The min/max boxes set
    /// the slider's range; editing the value box moves the slider (auto-expanding the range).
    /// </summary>
    private FrameworkElement BuildFlexSliderRow(Shape shape, GeoProp gp, double value,
                                                Action<double> setNumber, TextBox valueBox)
    {
        double span = Math.Max(Math.Abs(value), 10);
        double min = value - span, max = value + span;
        if (gp.NonNegative) min = Math.Max(0, min);
        if (gp.IsInteger) { min = Math.Floor(min); max = Math.Ceiling(max); }
        if (max <= min) max = min + 1;

        string fmt = gp.IsInteger ? "0" : "F2";

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var minBox = SmallBoundBox(min, fmt);
        var maxBox = SmallBoundBox(max, fmt);
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            IsSnapToTickEnabled = gp.IsInteger,
            TickFrequency = gp.IsInteger ? 1 : 0,
            SmallChange = gp.IsInteger ? 1 : span / 100.0,
            LargeChange = gp.IsInteger ? 1 : span / 10.0,
            ToolTip = "Drag to flex the value"
        };

        Grid.SetColumn(minBox, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(maxBox, 2);
        grid.Children.Add(minBox);
        grid.Children.Add(slider);
        grid.Children.Add(maxBox);

        bool dirty = false;

        slider.ValueChanged += (_, e) =>
        {
            if (_isUpdating) return;          // ignore programmatic / build-time value sets
            setNumber(e.NewValue);            // mutate shape only — no code sync
            _isUpdating = true;
            try { valueBox.Text = e.NewValue.ToString(fmt); } finally { _isUpdating = false; }
            dirty = true;
            RaiseLivePreview(shape);          // canvas-only redraw
        };

        void Commit()
        {
            if (!dirty) return;
            dirty = false;
            RaisePropertyChanged(shape);      // single canvas + source-code sync on release
        }
        slider.PreviewMouseUp += (_, __) => Commit();
        slider.LostFocus += (_, __) => Commit();   // keyboard arrow changes commit on blur

        // Editing the value text box moves the slider (and expands the range if out of bounds).
        valueBox.LostFocus += (_, __) =>
        {
            if (_isUpdating) return;
            if (!double.TryParse(valueBox.Text, out var d)) return;
            if (d < slider.Minimum) { slider.Minimum = d; minBox.Text = d.ToString(fmt); }
            if (d > slider.Maximum) { slider.Maximum = d; maxBox.Text = d.ToString(fmt); }
            _isUpdating = true;
            try { slider.Value = d; } finally { _isUpdating = false; }
        };

        minBox.LostFocus += (_, __) =>
        {
            if (double.TryParse(minBox.Text, out var m))
            {
                if (m >= slider.Maximum) m = slider.Maximum - (gp.IsInteger ? 1 : 0.001);
                slider.Minimum = m;
                if (slider.Value < m) { _isUpdating = true; try { slider.Value = m; } finally { _isUpdating = false; } }
            }
            minBox.Text = slider.Minimum.ToString(fmt);
        };
        maxBox.LostFocus += (_, __) =>
        {
            if (double.TryParse(maxBox.Text, out var m))
            {
                if (m <= slider.Minimum) m = slider.Minimum + (gp.IsInteger ? 1 : 0.001);
                slider.Maximum = m;
                if (slider.Value > m) { _isUpdating = true; try { slider.Value = m; } finally { _isUpdating = false; } }
            }
            maxBox.Text = slider.Maximum.ToString(fmt);
        };

        return grid;
    }

    private TextBox SmallBoundBox(double v, string fmt)
    {
        var box = new TextBox
        {
            Text = v.ToString(fmt),
            Width = 42,
            FontSize = 10,
            Background = (Brush)FindResource("BackgroundBrush"),
            Foreground = (Brush)FindResource("MutedForegroundBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            Padding = new Thickness(2, 1, 2, 1),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            ToolTip = "Slider range bound"
        };
        box.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && s is TextBox t) (t.Parent as FrameworkElement)?.Focus();
        };
        return box;
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
        LineWeightBox.Text = first.LineWeight.ToString("F1");

        // Opacity (shape stores 0.0-1.0, display as 0-100%)
        OpacityBox.Text = $"{first.Opacity * 100.0:F0}%";

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

    private void LineWeightBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedShapes.Count == 0) return;
        if (double.TryParse(LineWeightBox.Text, out var weight) && weight > 0)
        {
            foreach (var shape in _selectedShapes)
            {
                shape.LineWeight = weight;
            }
            RaisePropertyChanged(_selectedShapes[0], "LineWeight");
        }
        else
        {
            // Revert to current value
            LineWeightBox.Text = _selectedShapes[0].LineWeight.ToString("F1");
        }
    }

    private void OpacityBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || _selectedShapes.Count == 0) return;
        var text = OpacityBox.Text?.Trim().TrimEnd('%') ?? "";
        if (double.TryParse(text, out var pct) && pct >= 0 && pct <= 100)
        {
            double normalizedOpacity = pct / 100.0;
            foreach (var shape in _selectedShapes)
            {
                shape.Opacity = normalizedOpacity;
            }
            OpacityBox.Text = $"{pct:F0}%";
            RaisePropertyChanged(_selectedShapes[0], "Opacity");
        }
        else
        {
            // Revert to current value
            OpacityBox.Text = $"{_selectedShapes[0].Opacity * 100.0:F0}%";
        }
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

    private void RaiseLivePreview(Shape shape)
    {
        ShapeLivePreview?.Invoke(this, new ShapePropertyChangedEventArgs(shape));
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
