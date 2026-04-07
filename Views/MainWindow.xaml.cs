using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XboxControllerStudio.ViewModels;

namespace XboxControllerStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnMainVmPropertyChanged;

        if (e.NewValue is MainViewModel newVm)
            newVm.PropertyChanged += OnMainVmPropertyChanged;
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentPage))
            return;

        Dispatcher.InvokeAsync(AnimateContentSwitch);
    }

    private void AnimateContentSwitch()
    {
        MainContentHost.Opacity = 0;
        MainContentHost.RenderTransform = new TranslateTransform(0, 10);

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        var slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        MainContentHost.BeginAnimation(OpacityProperty, fade);
        ((TranslateTransform)MainContentHost.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
