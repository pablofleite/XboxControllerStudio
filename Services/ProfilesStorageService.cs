using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XboxControllerStudio.Models;

namespace XboxControllerStudio.Services;

/// <summary>
/// Persists the list of user-created MappingProfiles to JSON in AppData\Local.
/// The built-in read-only Default profile is never serialised — it is always
/// reconstructed at startup so its name stays localised.
/// </summary>
public sealed class ProfilesStorageService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XboxControllerStudio",
        "profiles.json");

    private sealed class ProfilesData
    {
        [JsonPropertyName("activeProfileId")]
        public Guid ActiveProfileId { get; set; }

        [JsonPropertyName("profiles")]
        public List<ProfileData> Profiles { get; set; } = new();
    }

    private sealed class ProfileData
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("targetProcess")]
        public string? TargetProcess { get; set; }

        [JsonPropertyName("useRightStickAsMouse")]
        public bool UseRightStickAsMouse { get; set; }

        [JsonPropertyName("rightStickMouseSensitivity")]
        public float RightStickMouseSensitivity { get; set; } = 14f;

        [JsonPropertyName("deadzone")]
        public DeadzoneData Deadzone { get; set; } = new();

        [JsonPropertyName("mappings")]
        public List<MappingData> Mappings { get; set; } = new();
    }

    private sealed class DeadzoneData
    {
        [JsonPropertyName("leftInner")]
        public float LeftInnerDeadzone { get; set; } = 0.12f;

        [JsonPropertyName("rightInner")]
        public float RightInnerDeadzone { get; set; } = 0.12f;

        [JsonPropertyName("outer")]
        public float OuterDeadzone { get; set; } = 1f;

        [JsonPropertyName("anti")]
        public float AntiDeadzone { get; set; }
    }

    private sealed class MappingData
    {
        [JsonPropertyName("button")]
        public string Button { get; set; } = string.Empty;

        [JsonPropertyName("virtualKey")]
        public ushort VirtualKey { get; set; }
    }

    /// <summary>
    /// Loads the persisted user-created profiles.
    /// Returns an empty list if the file does not exist or is corrupt.
    /// </summary>
    public (List<MappingProfile> profiles, Guid activeProfileId) Load()
    {
        if (!File.Exists(FilePath))
            return (new(), Guid.Empty);

        try
        {
            string json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<ProfilesData>(json);
            if (data is null)
                return (new(), Guid.Empty);

            var profiles = data.Profiles.Select(p => new MappingProfile
            {
                Id = p.Id,
                Name = p.Name,
                IsReadOnly = false,
                TargetProcess = p.TargetProcess,
                UseRightStickAsMouse = p.UseRightStickAsMouse,
                RightStickMouseSensitivity = p.RightStickMouseSensitivity,
                Deadzone = new DeadzoneSettings
                {
                    LeftInnerDeadzone = p.Deadzone.LeftInnerDeadzone,
                    RightInnerDeadzone = p.Deadzone.RightInnerDeadzone,
                    OuterDeadzone = p.Deadzone.OuterDeadzone,
                    AntiDeadzone = p.Deadzone.AntiDeadzone
                },
                Mappings = p.Mappings
                    .Select(m => new ButtonMapping
                    {
                        Button = Enum.TryParse<ControllerButton>(m.Button, out var btn) ? btn : ControllerButton.None,
                        VirtualKey = m.VirtualKey
                    })
                    .Where(m => m.Button != ControllerButton.None)
                    .ToList()
            }).ToList();

            return (profiles, data.ActiveProfileId);
        }
        catch
        {
            return (new(), Guid.Empty);
        }
    }

    /// <summary>
    /// Persists only the non-read-only profiles and the active profile id.
    /// </summary>
    public void Save(IEnumerable<MappingProfile> profiles, Guid activeProfileId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var data = new ProfilesData
            {
                ActiveProfileId = activeProfileId,
                Profiles = profiles
                    .Where(p => !p.IsReadOnly)
                    .Select(p => new ProfileData
                    {
                        Id = p.Id,
                        Name = p.Name,
                        TargetProcess = p.TargetProcess,
                        UseRightStickAsMouse = p.UseRightStickAsMouse,
                        RightStickMouseSensitivity = p.RightStickMouseSensitivity,
                        Deadzone = new DeadzoneData
                        {
                            LeftInnerDeadzone = p.Deadzone.LeftInnerDeadzone,
                            RightInnerDeadzone = p.Deadzone.RightInnerDeadzone,
                            OuterDeadzone = p.Deadzone.OuterDeadzone,
                            AntiDeadzone = p.Deadzone.AntiDeadzone
                        },
                        Mappings = p.Mappings
                            .Select(m => new MappingData
                            {
                                Button = m.Button.ToString(),
                                VirtualKey = m.VirtualKey
                            }).ToList()
                    }).ToList()
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Non-critical; ignore write failures.
        }
    }
}
