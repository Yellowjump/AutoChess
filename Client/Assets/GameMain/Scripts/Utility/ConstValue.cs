using System.Collections.Generic;
using UnityEngine;

public class ConstValue
{
    public const float EntityQiziHeight = 1;
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
    public const string SettingKeySoundGroupVolume = "Setting.{0}Volume";
    public static class AssetPriority
    {
        public const int ConfigAsset = 100;
        public const int DataTableAsset = 100;
        public const int DictionaryAsset = 100;
        public const int FontAsset = 50;
        public const int MusicAsset = 20;
        public const int SceneAsset = 0;
        public const int SoundAsset = 30;
        public const int UIFormAsset = 50;
        public const int UISoundAsset = 30;
        public const int ItemAsset = 70;
        public const int EntityAsset = 60;
    }

    public const int StoreItemNum = 12;
}