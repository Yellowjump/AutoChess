using System.Collections.Generic;
using UnityEngine;

public class ConstValue
{
    public static List<Color> RarityColorList = new List<Color>()
    {
        Color.white,
        Color.cyan,
        Color.magenta,
        Color.yellow,
    };

    public static List<string> RarityNameList = new List<string>()
    {
        "普通",
        "稀有",
        "史诗",
        "传说",
    };
    public const string SettingKeyLanguage = "Setting.Language";
}