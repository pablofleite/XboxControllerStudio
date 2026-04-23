using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using XboxControllerStudio.Core;
using XboxControllerStudio.Models;
using XboxControllerStudio.Services;

namespace XboxControllerStudio.ViewModels;

/// <summary>
/// Manages the list of MappingProfiles and exposes the active profile.
/// Future: add save/load from JSON, per-game auto-switch logic.
/// </summary>
public sealed class ProfilesViewModel : ObservableObject
{
    public event Action<MappingProfile>? ActiveProfileApplied;

    private readonly ProfilesStorageService _storage = new();

    private static readonly ControllerButton[] MappableButtons =
        Enum.GetValues(typeof(ControllerButton))
            .Cast<ControllerButton>()
            .Where(b => b != ControllerButton.None)
            .ToArray();

    public ObservableCollection<MappingProfile> Profiles { get; } = new();
    public ObservableCollection<ButtonMapping> ActiveMappings { get; } = new();

    private MappingProfile? _activeProfile;
    public MappingProfile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (SetProperty(ref _activeProfile, value))
            {
                OnPropertyChanged(nameof(CanEditActiveProfile));
                CancelCapture();
                LoadActiveMappings();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanEditActiveProfile => ActiveProfile is not null && !ActiveProfile.IsReadOnly;

    private ButtonMapping? _selectedMapping;
    public ButtonMapping? SelectedMapping
    {
        get => _selectedMapping;
        set
        {
            if (SetProperty(ref _selectedMapping, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusMessage = GetString("ProfilesReady", "Ready");
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isCapturePending;
    public bool IsCapturePending
    {
        get => _isCapturePending;
        private set
        {
            if (SetProperty(ref _isCapturePending, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public RelayCommand AddProfileCommand { get; }
    public RelayCommand<MappingProfile> RemoveProfileCommand { get; }
    public RelayCommand StartCaptureCommand { get; }
    public RelayCommand ClearSelectedMappingCommand { get; }
    public RelayCommand SaveProfileCommand { get; }

    public ProfilesViewModel()
    {
        // Always seed the built-in read-only default profile
        var defaultProfile = new MappingProfile
        {
            Name = GetString("ProfilesDefaultName", "Default"),
            IsReadOnly = true
        };
        EnsureAllButtonsMapped(defaultProfile);
        Profiles.Add(defaultProfile);

        // Restore user-created profiles from disk
        var (saved, activeId) = _storage.Load();
        foreach (var p in saved)
        {
            EnsureAllButtonsMapped(p);
            Profiles.Add(p);
        }

        // Restore last active profile, fallback to default
        ActiveProfile = Profiles.FirstOrDefault(p => p.Id == activeId) ?? defaultProfile;

        AddProfileCommand = new RelayCommand(AddProfile);
        RemoveProfileCommand = new RelayCommand<MappingProfile>(RemoveProfile,
            p => p is not null && Profiles.Count > 1 && !p.IsReadOnly);
        StartCaptureCommand = new RelayCommand(StartCapture, () => CanEditActiveProfile && SelectedMapping is not null);
        ClearSelectedMappingCommand = new RelayCommand(ClearSelectedMapping, () => CanEditActiveProfile && SelectedMapping is not null);
        SaveProfileCommand = new RelayCommand(SaveProfile, () => CanEditActiveProfile && ActiveProfile is not null);
    }

    private void AddProfile()
    {
        int editableCount = Profiles.Count(p => !p.IsReadOnly) + 1;
        string profileNameFormat = GetString("ProfilesNewProfileName", "Profile {0}");
        var profile = new MappingProfile
        {
            Name = string.Format(profileNameFormat, editableCount),
            IsReadOnly = false
        };
        EnsureAllButtonsMapped(profile);
        Profiles.Add(profile);
        ActiveProfile = profile;
        ActiveProfileApplied?.Invoke(profile);
        PersistProfiles();
    }

    private void RemoveProfile(MappingProfile? profile)
    {
        if (profile is null) return;
        int idx = Profiles.IndexOf(profile);
        Profiles.Remove(profile);
        ActiveProfile = Profiles[Math.Max(0, idx - 1)];
        StatusMessage = GetString("ProfilesRemoved", "Profile removed.");
        if (ActiveProfile is not null)
            ActiveProfileApplied?.Invoke(ActiveProfile);
        PersistProfiles();
    }

    private void StartCapture()
    {
        if (!CanEditActiveProfile || SelectedMapping is null)
        {
            if (!CanEditActiveProfile)
                StatusMessage = GetString("ProfilesDefaultLockedHint", "Default profile is read-only. Create a new profile to edit mappings.");
            return;
        }

        IsCapturePending = true;
        StatusMessage = GetString("ProfilesCaptureHint", "Press a keyboard key or mouse button to map selected controller button.");
    }

    private void ClearSelectedMapping()
    {
        if (!CanEditActiveProfile || SelectedMapping is null)
            return;

        SetMappingVirtualKey(SelectedMapping, 0);
        StatusMessage = GetString("ProfilesCaptureCleared", "Mapping cleared.");
    }

    private void SaveProfile()
    {
        if (!CanEditActiveProfile || ActiveProfile is null)
            return;

        SyncActiveMappingsToProfile();
        string format = GetString("ProfilesSavedAt", "Profile '{0}' saved at {1:HH:mm:ss}.");
        StatusMessage = string.Format(format, ActiveProfile.Name, DateTime.Now);
        PersistProfiles();
    }

    private void LoadActiveMappings()
    {
        ActiveMappings.Clear();

        if (ActiveProfile is null)
            return;

        EnsureAllButtonsMapped(ActiveProfile);

        foreach (var mapping in ActiveProfile.Mappings)
            ActiveMappings.Add(mapping);

        SelectedMapping = ActiveMappings.FirstOrDefault();
        if (!CanEditActiveProfile)
            StatusMessage = GetString("ProfilesDefaultLockedHint", "Default profile is read-only. Create a new profile to edit mappings.");

        ActiveProfileApplied?.Invoke(ActiveProfile);
    }

    public bool TryCaptureInputFromVirtualKey(int virtualKey)
    {
        if (!CanEditActiveProfile || !IsCapturePending || SelectedMapping is null)
            return false;

        if (virtualKey is <= 0 or > ushort.MaxValue)
            return false;

        SetMappingVirtualKey(SelectedMapping, (ushort)virtualKey);
        IsCapturePending = false;

        string format = GetString("ProfilesCaptureDone", "Mapped {0} to {1}.");
        StatusMessage = string.Format(format, SelectedMapping.Button, SelectedMapping.AssignedInput);
        return true;
    }

    public bool TryCaptureInputFromMouse(MouseButton mouseButton)
    {
        if (!CanEditActiveProfile || !IsCapturePending || SelectedMapping is null)
            return false;

        ushort code = mouseButton switch
        {
            MouseButton.Left => ButtonMapping.MouseLeft,
            MouseButton.Right => ButtonMapping.MouseRight,
            MouseButton.Middle => ButtonMapping.MouseMiddle,
            MouseButton.XButton1 => ButtonMapping.MouseX1,
            MouseButton.XButton2 => ButtonMapping.MouseX2,
            _ => 0
        };

        return code != 0 && TryCaptureInputFromVirtualKey(code);
    }

    public void RefreshLocalization()
    {
        var defaultProfile = Profiles.FirstOrDefault(p => p.IsReadOnly);
        if (defaultProfile is not null)
        {
            string localizedName = GetString("ProfilesDefaultName", "Default");
            if (!string.Equals(defaultProfile.Name, localizedName, StringComparison.Ordinal))
            {
                defaultProfile.Name = localizedName;
                int idx = Profiles.IndexOf(defaultProfile);
                if (idx >= 0)
                    Profiles[idx] = defaultProfile;

                if (ReferenceEquals(ActiveProfile, defaultProfile))
                    OnPropertyChanged(nameof(ActiveProfile));
            }
        }

        if (ActiveProfile?.IsReadOnly == true)
        {
            StatusMessage = GetString("ProfilesDefaultLockedHint", "Default profile is read-only. Create a new profile to edit mappings.");
            return;
        }

        if (IsCapturePending)
            StatusMessage = GetString("ProfilesCaptureHint", "Press a keyboard key or mouse button to map selected controller button.");
    }

    public void CancelCapture()
    {
        if (!IsCapturePending)
            return;

        IsCapturePending = false;
        StatusMessage = GetString("ProfilesReady", "Ready");
    }

    private void SetMappingVirtualKey(ButtonMapping mapping, ushort virtualKey)
    {
        int idx = ActiveMappings.IndexOf(mapping);
        if (idx < 0)
            return;

        var updated = new ButtonMapping
        {
            Button = mapping.Button,
            VirtualKey = virtualKey
        };

        ActiveMappings[idx] = updated;
        SelectedMapping = updated;
        SyncActiveMappingsToProfile();
    }

    private void SyncActiveMappingsToProfile()
    {
        if (ActiveProfile is null)
            return;

        ActiveProfile.Mappings = ActiveMappings.ToList();
        ActiveProfileApplied?.Invoke(ActiveProfile);
        PersistProfiles();
    }

    private void PersistProfiles()
    {
        _storage.Save(Profiles, ActiveProfile?.Id ?? Guid.Empty);
    }

    private static void EnsureAllButtonsMapped(MappingProfile profile)
    {
        foreach (var button in MappableButtons)
        {
            if (profile.Mappings.Any(m => m.Button == button))
                continue;

            profile.Mappings.Add(new ButtonMapping
            {
                Button = button,
                VirtualKey = 0
            });
        }

        profile.Mappings = profile.Mappings
            .OrderBy(m => Array.IndexOf(MappableButtons, m.Button))
            .ToList();
    }

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.TryFindResource(key) is string value && !string.IsNullOrWhiteSpace(value))
            return value;

        return fallback;
    }

}
