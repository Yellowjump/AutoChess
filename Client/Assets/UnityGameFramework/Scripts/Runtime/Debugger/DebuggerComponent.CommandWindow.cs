//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using DataTable;
using GameFramework.DataTable;
using SelfEventArg;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    public sealed partial class DebuggerComponent : GameFrameworkComponent
    {
        private sealed class CommandWindow : ScrollableDebuggerWindowBase
        {
            private int _curInputSelectIndex = 0;
            private int _curSliderSelectIndex = 0;
            private int _curSelectIndex = 0;
            private int itemNum = 0;
            private string itemIDStr = "1";
            private IDataTable<DRItem> _itemTablel;
            private List<int> _itemIDList = new List<int>();
            public override void OnEnter()
            {
                base.OnEnter();
                _itemTablel = GameEntry.DataTable.GetDataTable<DRItem>("Item");
                foreach (var oneItem in _itemTablel)
                {
                    _itemIDList.Add(oneItem.Id);
                }
            }

            protected override void OnDrawScrollableWindow()
            {
                GUILayout.Label("<b>Command</b>");
                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("itemID:", GUILayout.Width(200));
                    string inputItemIDStr = GUILayout.TextField(itemIDStr, GUILayout.Width(200));
                    var idInt = RemoveNonIntegerCharactersAndConvertToInt(inputItemIDStr);
                    itemIDStr = idInt.ToString();
                    for (var itemIndex = 0; itemIndex < _itemIDList.Count; itemIndex++)
                    {
                        var oneID = _itemIDList[itemIndex];
                        if (oneID == idInt)
                        {
                            _curInputSelectIndex = itemIndex;
                            break;
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (_curInputSelectIndex != _curSelectIndex)
                    {
                        _curSelectIndex = _curInputSelectIndex;
                        _curSliderSelectIndex = _curSelectIndex;
                    }
                    float newSliderValue = GUILayout.HorizontalSlider(_curSliderSelectIndex, 0, _itemIDList.Count - 1);
                    _curSliderSelectIndex = (int)Math.Round(newSliderValue);
                    if (_curSliderSelectIndex != _curSelectIndex)
                    {
                        _curSelectIndex = _curSliderSelectIndex;
                        _curInputSelectIndex = _curSelectIndex;
                        itemIDStr = _itemIDList[_curSliderSelectIndex].ToString();
                    }
                    GUILayout.Label($"{_itemTablel[_itemIDList[_curSliderSelectIndex]].Name},ID:{_itemIDList[_curSliderSelectIndex]}");
                    // 获取输入的文本
                    string newText = GUILayout.TextField(itemNum.ToString(), GUILayout.Width(200));
                    // 只保留整数字符（包括负号）
                    itemNum = RemoveNonIntegerCharactersAndConvertToInt(newText);
                    if (GUILayout.Button("添加道具"))
                    {
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(_itemIDList[_curSelectIndex],itemNum));
                    }
                    if (GUILayout.Button("添加道具合成测试"))
                    {
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(1,5));
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(2,3));
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(5,2));
                    }
                    if (GUILayout.Button("测试存档"))
                    {
                        GameEntry.HeroManager.Save();
                    }
                    if (GUILayout.Button("测试读档"))
                    {
                        GameEntry.HeroManager.Load();
                    }
                }
                GUILayout.EndVertical();
            }
            private int RemoveNonIntegerCharactersAndConvertToInt(string text)
            {
                // 移除所有非整数字符，保留负号
                char[] validChars = new char[text.Length];
                int validCharCount = 0;
                bool isNegative = false;

                foreach (char c in text)
                {
                    if (c == '-' && validCharCount == 0)
                    {
                        isNegative = true;
                        validChars[validCharCount++] = c;
                    }
                    else if (char.IsDigit(c))
                    {
                        validChars[validCharCount++] = c;
                    }
                }

                // 将有效字符数组转换为字符串
                string validString = new string(validChars, 0, validCharCount);

                // 尝试转换为整数，如果失败则返回0
                if (int.TryParse(validString, out int result))
                {
                    return result;
                }
                return 0;
            }
        }
    }
}
