//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2020 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using Entity;
using SkillSystem;
using UnityEngine;

namespace DataTable.Editor.DataTableTools
{
    public sealed partial class DataTableProcessor
    {
        private sealed class EnemyInfoProcessor : GenericDataProcessor<EnemyInfo>
        {
            public override bool IsSystem
            {
                get
                {
                    return false;
                }
            }

            public override string LanguageKeyword
            {
                get
                {
                    return "EnemyInfo";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "EnemyInfo",
                    "Entity.EnemyInfo"
                };
            }
            public override void WriteToStream(DataTableProcessor dataTableProcessor, BinaryWriter binaryWriter, string value)
            {
                var info = Parse(value);
                binaryWriter.Write(info.InfoList.Count);
                for (int i = 0; i < info.InfoList.Count; i++)
                {
                    var oneInfo = info.InfoList[i];
                    binaryWriter.Write(oneInfo.Pos.x);
                    binaryWriter.Write(oneInfo.Pos.y);
                    binaryWriter.Write(oneInfo.HeroID);
                }
            }

            public override EnemyInfo Parse(string value)
            {
                EnemyInfo newInfo = new EnemyInfo();
                if (string.IsNullOrEmpty(value))
                {
                    return newInfo;
                }
                string[] splitValue = value.Split('|');
                for (int i = 0; i < splitValue.Length; i++)
                {
                    string[] splitIntValue = splitValue[i].Split(',');
                    if (splitIntValue.Length != 3)
                    {
                        Console.WriteLine("EnemyInfo Error");
                        return newInfo;
                    }
                    OneEnemyInfo oneInfo = new OneEnemyInfo();
                    oneInfo.Pos = new Vector2Int(int.Parse(splitIntValue[0]), int.Parse(splitIntValue[1]));
                    oneInfo.HeroID = int.Parse(splitIntValue[2]);
                    newInfo.InfoList.Add(oneInfo);
                }
                return newInfo;
            }
        }
    }
}
