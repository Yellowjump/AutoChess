using System.Collections.Generic;
using DataTable;
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

    public static List<EnumLanguage> RarityNameList = new List<EnumLanguage>()
    {
        EnumLanguage.Rarity_Normal,
        EnumLanguage.Rarity_Rare,
        EnumLanguage.Rarity_Epic,
        EnumLanguage.Rarity_Legendary,
    };
    public const string SettingKeyLanguage = "Setting.Language";
    public const string SettingKeySoundGroupVolume = "Setting.{0}Volume";
    public const string SettingKeyGameRecord = "Setting.GameRecord";
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
    public const string WeaponHandleObjName = "PosPoint";
    public const string PosPointObjName = "PosPoint";
    public const int BattleMaxDuration = 120;
}