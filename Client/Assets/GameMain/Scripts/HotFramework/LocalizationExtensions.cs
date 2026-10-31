using DataTable;
using UnityGameFramework.Runtime;

public static class LocalizationExtensions
{
    public static string GetString(this LocalizationComponent localization, EnumLanguage key)
    {
        return localization.GetString(key.ToString());
    }
}
