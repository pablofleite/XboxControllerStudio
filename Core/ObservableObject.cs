using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XboxControllerStudio.Core;

/// <summary>
/// Base class for all ViewModels and observable objects.
/// Implements INotifyPropertyChanged so the WPF binding engine
/// reacts to property changes automatically.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets the backing field and raises PropertyChanged only when the value actually changed.
    /// Usage: SetProperty(ref _field, value);
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
