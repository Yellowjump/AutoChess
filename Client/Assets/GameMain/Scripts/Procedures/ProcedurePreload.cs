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
        private Dictionary<string,(Type,bool)> _dataTableFlag = new Dictionary<string, (Type, bool)>();
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
            InitCurrentVariant();
            if (GameEntry.Base.EditorResourceMode)
            {
                OnInitResourceComplete();
            }
            else
            {
                GameEntry.Resource.InitResources(OnInitResourceComplete);
            }
            
        }
        private void InitLanguageSettings()
        {
            if (GameEntry.Base.EditorResourceMode && GameEntry.Base.EditorLanguage != Language.Unspecified)
            {
                // 编辑器资源模式直接使用 Inspector 上设置的语言
                GameEntry.Setting.SetInt(ConstValue.SettingKeyLanguage, (int)GameEntry.Base.EditorLanguage);
                GameEntry.Setting.Save();
                return;
            }

            Language language = (Language)GameEntry.Setting.GetInt(ConstValue.SettingKeyLanguage, (int)Language.ChineseSimplified);

            if (language != Language.English && language != Language.ChineseSimplified)
            {
                language = Language.ChineseSimplified;
                GameEntry.Setting.SetString(ConstValue.SettingKeyLanguage, language.ToString());
                GameEntry.Setting.Save();
            }
            GameEntry.Localization.Language = language;
            Log.Info("Init language settings complete, current language is '{0}'.", language.ToString());
        }
        private void InitCurrentVariant()
        {
            if (GameEntry.Base.EditorResourceMode)
            {
                // 编辑器资源模式不使用 AssetBundle，也就没有变体了
                return;
            }

            string currentVariant = "zh_cn";
            switch (GameEntry.Localization.Language)
            {
                case Language.English:
                    currentVariant = "en_us";
                    break;

                case Language.ChineseSimplified:
                    currentVariant = "zh_cn";
                    break;
            }
            GameEntry.Resource.SetCurrentVariant(currentVariant);
            Log.Info("Init current variant complete.current variant :{0}.", currentVariant);
        }
        private void OnInitResourceComplete()
        {
            GameEntry.UI.OpenUIForm(UICtrlName.LoadingPanel, "top");
            m_ResourceLoaded = true;
            GameEntry.Event.Subscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
            GameEntry.Event.Subscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);
            GameEntry.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoadTerrainSceneSuccess);
            GameEntry.Event.Subscribe(LoadDictionarySuccessEventArgs.EventId, OnLoadLocalDataTableSuccess);
            GameEntry.Event.Subscribe(LoadDictionaryFailureEventArgs.EventId, OnLoadLocalTableFailure);
            m_LoadedFlag.Add("Assets/GameMain/Scenes/terrain.unity",false);
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
            GameEntry.Event.Unsubscribe(LoadDictionarySuccessEventArgs.EventId, OnLoadLocalDataTableSuccess);
            GameEntry.Event.Unsubscribe(LoadDictionaryFailureEventArgs.EventId, OnLoadLocalTableFailure);
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
        }

        private void PreloadDataTable()
        {
            _dataTableFlag.Clear();
            _dataTableFlag.Add("AssetsPath",(typeof(DRAssetsPath),false));
            _dataTableFlag.Add("Hero",(typeof(DRHero),false));
            _dataTableFlag.Add("Skill",(typeof(DRSkill),false));
            _dataTableFlag.Add("Buff",(typeof(DRBuff),false));
            _dataTableFlag.Add("SkillTemplate",(typeof(DRSkillTemplate),false));
            _dataTableFlag.Add("BuffTemplate",(typeof(DRBuffTemplate),false));
            _dataTableFlag.Add("Bullet",(typeof(DRBullet),false));
            _dataTableFlag.Add("EnemyConfig",(typeof(DREnemyConfig),false));
            _dataTableFlag.Add("HeroAttribute",(typeof(DRHeroAttribute),false));
            _dataTableFlag.Add("Item",(typeof(DRItem),false));
            _dataTableFlag.Add("LevelConfig",(typeof(DRLevelConfig),false));
            _dataTableFlag.Add("Sfx",(typeof(DRSfx),false));
            _dataTableFlag.Add("AreaPoint",(typeof(DRAreaPoint),false));
            _dataTableFlag.Add("RewardConfig",(typeof(DRRewardConfig),false));
            foreach (var tableName in _dataTableFlag)
            {
                DataTableBase dataTable = GameEntry.DataTable.CreateDataTable(tableName.Value.Item1, tableName.Key);
                dataTable.ReadData($"Assets/GameMain/Data/DataTables/{tableName.Key}.bytes",0, null);
            }
            _dataTableFlag.Add("Language",(typeof(DRLanguage),false));//交给localizationComponent
            GameEntry.Localization.ReadData("Assets/GameMain/Data/DataTables/Language.bytes",0);
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
                _dataTableFlag[tableName] = new ValueTuple<Type, bool>(_dataTableFlag[tableName].Item1,true);
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
            if (ne==null)
            {
                return;
            }

            Log.Error("Can not load table '{0}' with error: {1}", ne.DataTableAssetName,ne.ErrorMessage);
        }
        private void OnLoadLocalDataTableSuccess(object sender, GameEventArgs e)
        {
            LoadDictionarySuccessEventArgs ne = (LoadDictionarySuccessEventArgs)e;
            if (ne == null)
            {
                return;
            }

            var tableName = ExtractFileName(ne.DictionaryAssetName);
            if (_dataTableFlag.ContainsKey(tableName))
            {
                _dataTableFlag[tableName] = new ValueTuple<Type, bool>(_dataTableFlag[tableName].Item1,true);
                Log.Info("Load Localization config '{0}' OK.", ne.DictionaryAssetName);
            }
            else
            {
                Log.Error("load Localization error table '{0}' ", ne.DictionaryAssetName);
            }
        }
        private void OnLoadLocalTableFailure(object sender, GameEventArgs e)
        {
            LoadDictionaryFailureEventArgs ne = (LoadDictionaryFailureEventArgs)e;
            if (ne==null)
            {
                return;
            }

            Log.Error("Can not load table '{0}' with error: {1}", ne.DictionaryAssetName,ne.ErrorMessage);
        }
        private void OnLoadTerrainSceneSuccess(object sender, GameEventArgs e)
        {
            LoadSceneSuccessEventArgs ne = (LoadSceneSuccessEventArgs)e;
            if (ne==null)
            {
                return;
            }

            if (m_LoadedFlag.ContainsKey(ne.SceneAssetName))
            {
                m_LoadedFlag[ne.SceneAssetName] = true;
            }

            GameEntry.HeroManager.InitStartCamera();
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

