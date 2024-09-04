//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace DataTable.Editor.DataTableTools
{
    public sealed class DataTableGeneratorMenu
    {
        [MenuItem("Tools/Generate DataTables", false, 1)]
        public static void GenerateDataTables()
        {
            foreach (string dataTableName in GetDataTableNames())
            {
                DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(dataTableName);
                if (!DataTableGenerator.CheckRawData(dataTableProcessor, dataTableName))
                {
                    Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", dataTableName));
                    break;
                }

                DataTableGenerator.GenerateDataFile(dataTableProcessor, dataTableName);
            }

            AssetDatabase.Refresh();
        }
        public static void GenerateSkillTemplateDataTables()
        {
            string dataTableName = Application.dataPath + @"/GameMain/Data/DataTables/SkillTemplate";
            DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(dataTableName);
            if (!DataTableGenerator.CheckRawData(dataTableProcessor, dataTableName))
            {
                Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", dataTableName));
                return;
            }
            DataTableGenerator.GenerateDataFile(dataTableProcessor, dataTableName);
            AssetDatabase.Refresh();
        }
        public static void GenerateBuffTemplateDataTables()
        {
            string dataTableName = Application.dataPath + @"/GameMain/Data/DataTables/BuffTemplate";
            DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(dataTableName);
            if (!DataTableGenerator.CheckRawData(dataTableProcessor, dataTableName))
            {
                Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", dataTableName));
                return;
            }
            DataTableGenerator.GenerateDataFile(dataTableProcessor, dataTableName);
            AssetDatabase.Refresh();
        }
        [MenuItem("Tools/Generate DataTable Code", false, 2)]
        private static void GenerateDataTableCode()
        {
            foreach (string dataTableName in GetDataTableNames())
            {
                DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(dataTableName);
                if (!DataTableGenerator.CheckRawData(dataTableProcessor, dataTableName))
                {
                    Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", dataTableName));
                    break;
                }

                DataTableGenerator.GenerateCodeFile(dataTableProcessor, dataTableName);
            }

            AssetDatabase.Refresh();
        }

        private static string[] GetDataTableNames()
        {
            string dataTablesPath = Application.dataPath + @"/GameMain/Data/DataTables";
            DirectoryInfo directoryInfo = new DirectoryInfo(dataTablesPath);
            FileInfo[] fis = directoryInfo.GetFiles("*.txt", SearchOption.AllDirectories);
            string[] dataTableNames = new string[fis.Length];
            for (int i = 0; i < fis.Length; i++)
            {
                dataTableNames[i] = Path.GetFileNameWithoutExtension(fis[i].Name);
            }

            return dataTableNames;
        }

        //[MenuItem("Tools/Generate Config", false, 1)]
        //private static void GenerateConfig()
        //{
        //    string configPath = Application.dataPath + @"/GameMain/Configs/DefaultConfig";

        //    DataTableProcessor dataTableProcessor = DataTableGenerator.CreateDataTableProcessor(configPath);
        //    if (!DataTableGenerator.CheckRawData(dataTableProcessor, configPath))
        //    {
        //        Debug.LogError(Utility.Text.Format("Check raw data failure. DataTableName='{0}'", configPath));
        //        return;
        //    }

        //    DataTableGenerator.GenerateDataFile(dataTableProcessor, configPath);

        //    AssetDatabase.Refresh();
        //}

    }
}
