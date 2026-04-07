using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using XboxControllerStudio.Core;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Dashboard page with summary cards for connected controllers.
/// Reuses the live collection from ControllersViewModel.
/// </summary>
public sealed class HomeViewModel : ObservableObject
{
    private readonly ControllersViewModel _controllersVm;

    public ObservableCollection<ControllerViewModel> Controllers => _controllersVm.Controllers;

    private int _connectedCount;
    public int ConnectedCount
    {
        get => _connectedCount;
        private set
        {
            if (SetProperty(ref _connectedCount, value))
                OnPropertyChanged(nameof(ConnectedSummary));
        }
    }

    public string ConnectedSummary
    {
        get
        {
            string format = GetString("ConnectedSummaryFormat", "{0} online");
            return string.Format(format, ConnectedCount);
        }
    }

    public HomeViewModel(ControllersViewModel controllersVm)
    {
        _controllersVm = controllersVm;

        foreach (var controller in Controllers)
            controller.PropertyChanged += OnControllerPropertyChanged;

        UpdateConnectedCount();
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControllerViewModel.IsConnected))
            UpdateConnectedCount();
    }

    private void UpdateConnectedCount()
    {
        int count = 0;
        foreach (var controller in Controllers)
        {
            if (controller.IsConnected)
                count++;
        }

        ConnectedCount = count;
    }

    public void RefreshLocalization() => OnPropertyChanged(nameof(ConnectedSummary));

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.TryFindResource(key) is string value && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback;
    }
}
