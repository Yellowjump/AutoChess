using System.Collections.Generic;
using GameFramework;
using GameFramework.Localization;
using DataTable;
using UnityGameFramework.Runtime;

public static class LocalizationExtensions
{
    public static string GetString(this LocalizationComponent localization, EnumLanguage key)
    {
        return localization.GetString(key.ToString());
    }

    internal static bool ApplyLanguage(Language language)
    {
        if (language != Language.ChineseSimplified && language != Language.English) return false;
        var table = GameEntry.DataTable.GetDataTable<DRLanguage>("Language");
        if (table == null || table.Count == 0) return false;

        var strings = new Dictionary<string, string>(table.Count);
        foreach (var row in table.GetAllDataRows())
        {
            if (string.IsNullOrEmpty(row.EnumName) || strings.ContainsKey(row.EnumName)) return false;
            strings.Add(row.EnumName, language == Language.English && !string.IsNullOrWhiteSpace(row.English)
                ? row.English : row.Chinese);
        }

        var manager = GameFrameworkEntry.GetModule<ILocalizationManager>();
        manager.RemoveAllRawStrings();
        foreach (var entry in strings) manager.AddRawString(entry.Key, entry.Value);
        manager.Language = language;

        // Refresh open forms in place. Forms opened later read the same dictionary in OnOpen.
        var ui = GameEntry.UI;
        if (ui != null)
        {
            foreach (var form in ui.GetAllLoadedUIForms())
            {
                if (form.Logic != null && form.Logic.Available)
                    form.Logic.RefreshLocalizedText();
            }
        }
        return true;
    }
}
