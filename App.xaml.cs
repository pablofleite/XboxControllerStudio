using System.ComponentModel;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using XboxControllerStudio.Services;
using XboxControllerStudio.ViewModels;

namespace XboxControllerStudio;

public partial class App : Application
{
    // Services are created here so they live for the entire application lifetime
    private InputPollingService? _pollingService;
    private Views.MainWindow? _mainWindow;
    private MainViewModel? _mainVm;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _trayOpenItem;
    private Forms.ToolStripMenuItem? _trayExitItem;
    private bool _isExplicitExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build shared services
        var xinputService = new XInputService();
        var sendInputService = new SendInputService();
        var localizationService = new LocalizationService();
        _pollingService = new InputPollingService(xinputService);

        // Wire the root ViewModel and inject dependencies
        _mainVm = new MainViewModel(_pollingService, sendInputService, localizationService);
        _mainVm.Settings.PropertyChanged += OnSettingsPropertyChanged;

        InitializeTrayIcon();

        _mainWindow = new Views.MainWindow { DataContext = _mainVm };
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.StateChanged += OnMainWindowStateChanged;
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow.StateChanged -= OnMainWindowStateChanged;
        }

        if (_mainVm is not null)
            _mainVm.Settings.PropertyChanged -= OnSettingsPropertyChanged;

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        // Stop background polling cleanly before the process exits
        _pollingService?.Dispose();
        base.OnExit(e);
    }

    private void InitializeTrayIcon()
    {
        _trayOpenItem = new Forms.ToolStripMenuItem();
        _trayExitItem = new Forms.ToolStripMenuItem();
        _trayOpenItem.Click += (_, _) => ShowMainWindow();
        _trayExitItem.Click += (_, _) => ExitFromTray();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(new Forms.ToolStripItem[] { _trayOpenItem, _trayExitItem });

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        UpdateTrayLocalization();
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow is null || _mainVm is null)
            return;

        if (_mainWindow.WindowState == WindowState.Minimized && _mainVm.Settings.MinimizeToTray)
            HideMainWindow();
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExplicitExit || _mainVm is null)
            return;

        if (_mainVm.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideMainWindow();
        }
    }

    private void HideMainWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Hide();
        _mainWindow.ShowInTaskbar = false;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.ShowInTaskbar = true;
        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
    }

    private void ExitFromTray()
    {
        _isExplicitExit = true;
        _mainWindow?.Close();
        Shutdown();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedLanguageCode))
            UpdateTrayLocalization();
    }

    private void UpdateTrayLocalization()
    {
        if (_trayIcon is null || _trayOpenItem is null || _trayExitItem is null)
            return;

        _trayIcon.Text = GetString("TrayTooltip", "Xbox Controller Studio");
        _trayOpenItem.Text = GetString("TrayOpen", "Open");
        _trayExitItem.Text = GetString("TrayExit", "Exit");
    }

    private static string GetString(string key, string fallback)
    {
        if (Current?.TryFindResource(key) is string value && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback;
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "app-icon.ico");
        if (System.IO.File.Exists(iconPath))
            return new Drawing.Icon(iconPath);

        return Drawing.SystemIcons.Application;
    }
}
