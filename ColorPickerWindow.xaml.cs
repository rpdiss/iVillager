using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace iVillager;

public partial class ColorPickerWindow : Window
{
    public System.Windows.Media.Color? SelectedColor { get; private set; }

    public ColorPickerWindow()
    {
        InitializeComponent();
        UpdatePreview();
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TextR != null) TextR.Text = ((int)SliderR.Value).ToString();
        if (TextG != null) TextG.Text = ((int)SliderG.Value).ToString();
        if (TextB != null) TextB.Text = ((int)SliderB.Value).ToString();
        if (TextA != null) TextA.Text = ((int)SliderA.Value).ToString();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewBorder != null)
            PreviewBorder.Background = new SolidColorBrush(GetCurrentColor());
    }

    private System.Windows.Media.Color GetCurrentColor()
    {
        return System.Windows.Media.Color.FromArgb(
            (byte)SliderA.Value,
            (byte)SliderR.Value,
            (byte)SliderG.Value,
            (byte)SliderB.Value);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedColor = GetCurrentColor();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedColor = null;
        DialogResult = false;
        Close();
    }
}
