using System.Windows;
using System.Windows.Controls;

namespace XboxControllerStudio.Views;

/// <summary>
/// Draws a dot inside a circle to visualise a 2-axis stick position.
/// StickX and StickY are expected in the range [-1, 1].
/// The dot is positioned by remapping those values to canvas coordinates.
/// </summary>
public partial class StickVisualiser : UserControl
{
    private const double Radius = 60.0;  // canvas half-size
    private const double DotRadius = 7.0;   // half of Dot width/height
    private const double TravelZone = 46.0;  // usable radius inside the ring

    public static readonly DependencyProperty StickXProperty =
        DependencyProperty.Register(nameof(StickX), typeof(float), typeof(StickVisualiser),
            new PropertyMetadata(0f, OnStickChanged));

    public static readonly DependencyProperty StickYProperty =
        DependencyProperty.Register(nameof(StickY), typeof(float), typeof(StickVisualiser),
            new PropertyMetadata(0f, OnStickChanged));

    public static readonly DependencyProperty DeadzoneRadiusProperty =
        DependencyProperty.Register(nameof(DeadzoneRadius), typeof(float), typeof(StickVisualiser),
            new PropertyMetadata(0.12f, OnStickChanged));

    public float StickX
    {
        get => (float)GetValue(StickXProperty);
        set => SetValue(StickXProperty, value);
    }

    public float StickY
    {
        get => (float)GetValue(StickYProperty);
        set => SetValue(StickYProperty, value);
    }

    /// <summary>
    /// Deadzone radius in [0, 1] shown as an inner ring.
    /// </summary>
    public float DeadzoneRadius
    {
        get => (float)GetValue(DeadzoneRadiusProperty);
        set => SetValue(DeadzoneRadiusProperty, value);
    }

    public StickVisualiser()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisuals();
    }

    private static void OnStickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StickVisualiser v) v.UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Remap [-1,1] → pixel offset inside the travel zone
        // Y axis is inverted because canvas Y grows downward
        double cx = Radius + StickX * TravelZone - DotRadius;
        double cy = Radius - StickY * TravelZone - DotRadius;

        Canvas.SetLeft(Dot, cx);
        Canvas.SetTop(Dot, cy);

        double deadzone = Math.Clamp(DeadzoneRadius, 0f, 1f);
        double dzRadius = deadzone * TravelZone;
        double diameter = dzRadius * 2.0;

        DeadzoneRing.Width = diameter;
        DeadzoneRing.Height = diameter;
        Canvas.SetLeft(DeadzoneRing, Radius - dzRadius);
        Canvas.SetTop(DeadzoneRing, Radius - dzRadius);
    }
}
