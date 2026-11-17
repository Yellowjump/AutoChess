using System;
using System.Collections;
using System.Collections.Generic;
using DataTable;
using GameFramework;
using GameFramework.Event;
using UnityGameFramework.Runtime;
using GameFramework.Procedure;
using GameFramework.DataTable;
using GameFramework.Localization;
using UnityEngine;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Procedure
{
    public class ProcedurePreload : ProcedureBase
    {
        private Dictionary<string, (Type, bool)> _dataTableFlag = new Dictionary<string, (Type, bool)>();
        private Dictionary<string, bool> m_LoadedFlag = new Dictionary<string, bool>();
        private bool m_ResourceLoaded = false;

        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
        }

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            InitLanguageSettings();
            InitSoundVolume();
            InitScreenResolution();
            if (GameEntry.Base.EditorResourceMode)
            {
                OnInitResourceComplete();
            }
            else
            {
                GameEntry.Resource.CheckResources((moved, removed, update, length, zipped) =>
                {
                    if (update == 0) OnInitResourceComplete();
                    else Log.Error("Bootstrap did not finish downloading {0} resources.", update);
                });
            }
        }

        private void InitLanguageSettings()
        {
            string saved = GameEntry.Setting.GetString(ConstValue.SettingKeyLanguage, null);
            var fallback = GameEntry.Base.EditorResourceMode &&
                GameEntry.Base.EditorLanguage == Language.English ? Language.English : Language.ChineseSimplified;
            var language = fallback;
            if (Enum.TryParse(saved, out Language parsed) &&
                (parsed == Language.English || parsed == Language.ChineseSimplified))
                language = parsed;

            GameEntry.Localization.Language = language;
            if (saved != null && saved != ((int)language).ToString())
            {
                GameEntry.Setting.SetInt(ConstValue.SettingKeyLanguage, (int)language);
                GameEntry.Setting.Save();
            }
        }

        private void InitScreenResolution()
        {
            var savedFullScreenMode = (FullScreenMode)GameEntry.Setting.GetInt(ConstValue.SettingFullScreenMode, (int)FullScreenMode.Windowed);
            var savedScreenResolution = GameEntry.Setting.GetInt(ConstValue.SettingKeyScreenResolution, UnityExtension.GetCurrentResolutionIndex());
            var savedResolution = UnityExtension.GetResolutionByIndex(savedScreenResolution);
            if (savedResolution != null)
            {
                GameEntry.Setting.SetInt(ConstValue.SettingFullScreenMode,(int)savedFullScreenMode);
                GameEntry.Setting.SetInt(ConstValue.SettingKeyScreenResolution,savedScreenResolution);
                GameEntry.Setting.Save();
                Screen.SetResolution(savedResolution.Value.width,savedResolution.Value.height, savedFullScreenMode);
            }
        }

        private void InitSoundVolume()
        {
            HotGameEntry.Sound.SetMasterVolume(GameEntry.Setting.GetSoundGroupVolume("Master"));
            HotGameEntry.Sound.SetVolume("BGM", GameEntry.Setting.GetSoundGroupVolume("BGM"));
            HotGameEntry.Sound.SetVolume("UI", GameEntry.Setting.GetSoundGroupVolume("UI"));
            HotGameEntry.Sound.SetVolume("Sfx", GameEntry.Setting.GetSoundGroupVolume("Sfx"));
        }

        private void OnInitResourceComplete()
        {
            GameEntry.UI.OpenUIForm(UICtrlName.LoadingPanel, "top");
            m_ResourceLoaded = true;
            GameEntry.Event.Subscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
            GameEntry.Event.Subscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);
            GameEntry.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoadTerrainSceneSuccess);
            m_LoadedFlag.Add("Assets/GameMain/Scenes/terrain.unity", false);
            GameEntry.Scene.LoadScene("Assets/GameMain/Scenes/terrain.unity");
            PreloadDataTable();
            Log.Info("Init resources complete.");
        }

        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (m_ResourceLoaded == false)
            {
                return;
            }

            foreach (var item in m_LoadedFlag)
            {
                if (!item.Value)
                    return;
            }

            if (_dataTableFlag == null)
                return;

            foreach (var item in _dataTableFlag)
            {
                if (!item.Value.Item2)
                    return;
            }

            ChangeState<ProcedureTitle>(procedureOwner);
        }


        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);

            GameEntry.Event.Unsubscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
            GameEntry.Event.Unsubscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);
            GameEntry.Event.Unsubscribe(LoadSceneSuccessEventArgs.EventId, OnLoadTerrainSceneSuccess);
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
        }

        private void PreloadDataTable()
        {
            _dataTableFlag.Clear();
            _dataTableFlag.Add("AssetsPath", (typeof(DRAssetsPath), false));
            _dataTableFlag.Add("Hero", (typeof(DRHero), false));
            _dataTableFlag.Add("Skill", (typeof(DRSkill), false));
            _dataTableFlag.Add("Buff", (typeof(DRBuff), false));
            _dataTableFlag.Add("SkillTemplate", (typeof(DRSkillTemplate), false));
            _dataTableFlag.Add("BuffTemplate", (typeof(DRBuffTemplate), false));
            _dataTableFlag.Add("Bullet", (typeof(DRBullet), false));
            _dataTableFlag.Add("EnemyConfig", (typeof(DREnemyConfig), false));
            _dataTableFlag.Add("HeroAttribute", (typeof(DRHeroAttribute), false));
            _dataTableFlag.Add("Item", (typeof(DRItem), false));
            _dataTableFlag.Add("LevelConfig", (typeof(DRLevelConfig), false));
            _dataTableFlag.Add("Sfx", (typeof(DRSfx), false));
            _dataTableFlag.Add("AreaPoint", (typeof(DRAreaPoint), false));
            _dataTableFlag.Add("RewardConfig", (typeof(DRRewardConfig), false));
            _dataTableFlag.Add("Sound", (typeof(DRSound), false));
            _dataTableFlag.Add("OuterLocations", (typeof(DROuterLocations), false));
            _dataTableFlag.Add("ChosenRoute", (typeof(DRChosenRoute), false));
            _dataTableFlag.Add("StartingCultivators", (typeof(DRStartingCultivators), false));
            _dataTableFlag.Add("StoryEvent", (typeof(DRStoryEvent), false));
            _dataTableFlag.Add("StoryOption", (typeof(DRStoryOption), false));
            _dataTableFlag.Add("StoryItem", (typeof(DRStoryItem), false));
            _dataTableFlag.Add("MerchantOffer", (typeof(DRMerchantOffer), false));
            _dataTableFlag.Add("BattleMapping", (typeof(DRBattleMapping), false));
            _dataTableFlag.Add("BattleArena", (typeof(DRBattleArena), false));
            _dataTableFlag.Add("CultivationLevel", (typeof(DRCultivationLevel), false));
            _dataTableFlag.Add("TechniqueStage", (typeof(DRTechniqueStage), false));
            _dataTableFlag.Add("Language", (typeof(DRLanguage), false));
            foreach (var tableName in _dataTableFlag)
            {
                DataTableBase dataTable = GameEntry.DataTable.CreateDataTable(tableName.Value.Item1, tableName.Key);
                dataTable.ReadData($"Assets/GameMain/Data/DataTables/{tableName.Key}.bytes", 0, null);
            }
        }


        private void OnLoadDataTableSuccess(object sender, GameEventArgs e)
        {
            LoadDataTableSuccessEventArgs ne = (LoadDataTableSuccessEventArgs)e;
            if (ne == null)
            {
                return;
            }

            var tableName = ExtractFileName(ne.DataTableAssetName);
            if (_dataTableFlag.ContainsKey(tableName))
            {
                if (tableName == "Language" && !LocalizationExtensions.ApplyLanguage(GameEntry.Localization.Language))
                {
                    Log.Error("The language table has missing or duplicate keys.");
                    return;
                }
                _dataTableFlag[tableName] = (_dataTableFlag[tableName].Item1, true);
                Log.Info("Load config '{0}' OK.", ne.DataTableAssetName);
            }
            else
            {
                Log.Error("load error table '{0}' ", ne.DataTableAssetName);
            }
        }

        private void OnLoadDataTableFailure(object sender, GameEventArgs e)
        {
            LoadDataTableFailureEventArgs ne = (LoadDataTableFailureEventArgs)e;
            if (ne == null)
            {
                return;
            }

            Log.Error("Can not load table '{0}' with error: {1}", ne.DataTableAssetName, ne.ErrorMessage);
        }

        private void OnLoadTerrainSceneSuccess(object sender, GameEventArgs e)
        {
            LoadSceneSuccessEventArgs ne = (LoadSceneSuccessEventArgs)e;
            if (ne == null)
            {
                return;
            }

            if (m_LoadedFlag.ContainsKey(ne.SceneAssetName))
            {
                m_LoadedFlag[ne.SceneAssetName] = true;
            }

            HotGameEntry.HeroManager.InitTerrainList();
            HotGameEntry.HeroManager.InitStartCamera();
        }

        private string ExtractFileName(string filePath)
        {
            // 找到最后一个斜杠（/）或反斜杠（\）的索引位置
            int lastSlashIndex = filePath.LastIndexOf('/');
            int lastBackslashIndex = filePath.LastIndexOf('\\');

            // 选择索引位置较大的那个，以确保能够正确截取文件名
            int separatorIndex = Math.Max(lastSlashIndex, lastBackslashIndex);

            if (separatorIndex >= 0 && separatorIndex < filePath.Length - 1)
            {
                // 截取文件名部分
                string fileNameWithExtension = filePath.Substring(separatorIndex + 1);

                // 去除文件扩展名部分
                int extensionIndex = fileNameWithExtension.LastIndexOf('.');
                if (extensionIndex > 0)
                {
                    return fileNameWithExtension.Substring(0, extensionIndex);
                }
                else
                {
                    return fileNameWithExtension;
                }
            }

            return null;
        }
    }
}
