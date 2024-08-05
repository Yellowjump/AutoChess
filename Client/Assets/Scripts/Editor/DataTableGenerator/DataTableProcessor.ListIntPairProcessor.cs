using System.Collections.Generic;
using System.IO;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace DataTable.Editor.DataTableTools
{
    public sealed partial class DataTableProcessor
    {
        private sealed class ListIntPairProcessor : GenericDataProcessor<List<(int,int)>>
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
                    return "List<(int,int)>";
                }
            }

            public override string[] GetTypeStrings()
            {
                return new string[]
                {
                    "list<(int,int)>",
                };
            }

            public override List<(int,int)> Parse(string value)
            {
                List<(int,int)> result = new();
                if (string.IsNullOrEmpty(value))
                {
                    return result;
                }
                string[] splitValue = value.Split(';');
                for (int i = 0; i < splitValue.Length; i++)
                {
                    string[] splitInt = splitValue[i].Split(",");
                    if (splitInt.Length != 2)
                    {
                        Log.Error("Format Error");
                        throw new GameFrameworkException("Format Error");
                        continue;
                    }
                    result.Add((int.Parse(splitInt[0]),int.Parse(splitInt[1])));
                }

                return result;
            }

            public override void WriteToStream(DataTableProcessor dataTableProcessor, BinaryWriter binaryWriter, string value)
            {
                var listIntPair = Parse(value);
                binaryWriter.Write(listIntPair.Count);
                foreach (var elementValue in listIntPair)
                {
                    binaryWriter.Write(elementValue.Item1);
                    binaryWriter.Write(elementValue.Item2);
                }
            }
        }
    }
}
