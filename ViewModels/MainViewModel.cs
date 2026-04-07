using System.ComponentModel;
using System.Windows;
using XboxControllerStudio.Core;
using XboxControllerStudio.Services;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Root ViewModel — owned by App.xaml.cs and set as DataContext on MainWindow.
/// Holds child view models for each navigation section and tracks which
/// section is active so the sidebar can highlight the correct item.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    // --- Child sections ---
    public HomeViewModel Home { get; }
    public ControllersViewModel Controllers { get; }
    public ProfilesViewModel Profiles { get; }
    public SettingsViewModel Settings { get; }
    private readonly LocalizationService _localization;

    private string _latestNotification = "No alerts.";
    public string LatestNotification
    {
        get => _latestNotification;
        private set => SetProperty(ref _latestNotification, value);
    }

    // --- Navigation ---
    private ObservableObject _currentPage;
    public ObservableObject CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsHomeActive));
                OnPropertyChanged(nameof(IsControllersActive));
                OnPropertyChanged(nameof(IsProfilesActive));
                OnPropertyChanged(nameof(IsSettingsActive));
            }
        }
    }

    public bool IsHomeActive => ReferenceEquals(CurrentPage, Home);
    public bool IsControllersActive => ReferenceEquals(CurrentPage, Controllers);
    public bool IsProfilesActive => ReferenceEquals(CurrentPage, Profiles);
    public bool IsSettingsActive => ReferenceEquals(CurrentPage, Settings);

    public RelayCommand NavigateHomeCommand { get; }
    public RelayCommand NavigateControllersCommand { get; }
    public RelayCommand NavigateProfilesCommand { get; }
    public RelayCommand NavigateSettingsCommand { get; }

    public MainViewModel(InputPollingService polling, SendInputService sendInput, LocalizationService localization)
    {
        _localization = localization;
        Settings = new SettingsViewModel();
        Controllers = new ControllersViewModel(polling, sendInput, Settings);
        Home = new HomeViewModel(Controllers);
        Profiles = new ProfilesViewModel();

        Controllers.LowBatteryAlertRaised += msg => LatestNotification = msg;
        Profiles.ActiveProfileApplied += profile => Controllers.ApplyProfileToAll(profile);
        Settings.PropertyChanged += OnSettingsChanged;

        // Ensure the initial active profile (Default) is applied to all controllers.
        if (Profiles.ActiveProfile is not null)
            Controllers.ApplyProfileToAll(Profiles.ActiveProfile);

        LatestNotification = GetString("NoAlerts", "No alerts.");

        // Default page
        _currentPage = Home;

        NavigateHomeCommand = new RelayCommand(() => CurrentPage = Home);
        NavigateControllersCommand = new RelayCommand(() => CurrentPage = Controllers);
        NavigateProfilesCommand = new RelayCommand(() => CurrentPage = Profiles);
        NavigateSettingsCommand = new RelayCommand(() => CurrentPage = Settings);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.LowBatteryThresholdPercent))
        {
            string format = GetString("LowBatteryThresholdSet", "Low battery threshold set to {0}%.");
            LatestNotification = string.Format(format, Settings.LowBatteryThresholdPercent);
        }

        if (e.PropertyName == nameof(SettingsViewModel.SelectedLanguageCode))
        {
            _localization.ApplyLanguage(Settings.SelectedLanguageCode);
            Controllers.RefreshLocalization();
            Home.RefreshLocalization();
            Profiles.RefreshLocalization();
            LatestNotification = GetString("NoAlerts", "No alerts.");
        }
    }

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.TryFindResource(key) is string text && !string.IsNullOrWhiteSpace(text))
            return text;

        return fallback;
    }
}
