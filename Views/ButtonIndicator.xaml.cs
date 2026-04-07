using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XboxControllerStudio.Views;

/// <summary>
/// Small button indicator that lights up (changes background/foreground)
/// when IsActive is true.  Label and ActiveColor are dependency properties
/// so they can be set inline in XAML without a ViewModel.
/// </summary>
public partial class ButtonIndicator : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ButtonIndicator),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ButtonIndicator),
            new PropertyMetadata(false, OnActiveChanged));

    public static readonly DependencyProperty ActiveColorProperty =
        DependencyProperty.Register(nameof(ActiveColor), typeof(string), typeof(ButtonIndicator),
            new PropertyMetadata("#107C10"));  // Default = Xbox green

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Hex colour string for the active state background.</summary>
    public string ActiveColor
    {
        get => (string)GetValue(ActiveColorProperty);
        set => SetValue(ActiveColorProperty, value);
    }

    public ButtonIndicator()
    {
        InitializeComponent();
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ButtonIndicator ctrl)
            ctrl.LabelText.Text = e.NewValue as string;
    }

    private static void OnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ButtonIndicator ctrl)
            ctrl.Refresh();
    }

    private void Refresh()
    {
        var border = (System.Windows.Controls.Border)Content;
        var textBlock = LabelText;

        if (IsActive)
        {
            border.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(ActiveColor));
            border.BorderBrush = border.Background;
            textBlock.Foreground = Brushes.White;
        }
        else
        {
            border.Background = (Brush)FindResource("BrushSurface");
            border.BorderBrush = (Brush)FindResource("BrushSeparator");
            textBlock.Foreground = (Brush)FindResource("BrushTextSecondary");
        }
    }
}
