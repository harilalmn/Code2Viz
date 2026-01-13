using System.Windows;
using System.Windows.Media;

namespace Code2Viz;

public partial class ExportOptionsWindow : Window
{
    public Brush SelectedBackground { get; private set; } = Brushes.Transparent;
    public bool IncludeGrid { get; private set; } = true;

    public ExportOptionsWindow()
    {
        InitializeComponent();
    }
    
    public void SetDefault(string preference)
    {
        switch (preference)
        {
            case "Transparent": RadioTransparent.IsChecked = true; break;
            case "Canvas Background": RadioCanvas.IsChecked = true; break;
            case "White": RadioWhite.IsChecked = true; break;
            case "Black": RadioBlack.IsChecked = true; break;
            case "Light (White Smoke)": 
            case "Light": RadioLight.IsChecked = true; break;
        }
    }
    
    public void SetGridDefault(bool include)
    {
        CheckShowGrid.IsChecked = include;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (RadioTransparent.IsChecked == true)
            SelectedBackground = Brushes.Transparent;
        else if (RadioCanvas.IsChecked == true)
            SelectedBackground = null; // Signal to use current canvas brush
        else if (RadioWhite.IsChecked == true)
            SelectedBackground = Brushes.White;
        else if (RadioBlack.IsChecked == true)
            SelectedBackground = Brushes.Black;
        else if (RadioLight.IsChecked == true)
            SelectedBackground = Brushes.WhiteSmoke;
            
        IncludeGrid = CheckShowGrid.IsChecked == true;

        DialogResult = true;
        Close();
    }
}
