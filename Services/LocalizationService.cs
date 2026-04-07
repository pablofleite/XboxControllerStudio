using System.Windows;

namespace XboxControllerStudio.Services;

/// <summary>
/// Runtime localization by swapping a dedicated resource dictionary.
/// Add new languages by creating a new StringResources.xx-YY.xaml file.
/// </summary>
public sealed class LocalizationService
{
    private const string LocalizationPathPrefix = "Resources/Localization/StringResources.";

    public void ApplyLanguage(string languageCode)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;

        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString;
            if (!string.IsNullOrWhiteSpace(src) && src.Contains(LocalizationPathPrefix, StringComparison.OrdinalIgnoreCase))
                merged.RemoveAt(i);
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"{LocalizationPathPrefix}{languageCode}.xaml", UriKind.Relative)
        };

        merged.Add(dictionary);
    }
}
