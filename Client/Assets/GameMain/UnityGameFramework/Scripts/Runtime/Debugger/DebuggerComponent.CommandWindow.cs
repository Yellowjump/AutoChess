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
            private int itemNum = 1;
            private string itemIDStr = "1";
            private IDataTable<DRItem> _itemTablel;
            private List<int> _itemIDList = new List<int>();
            private int _changeCoinNum = 0;
#if UNITY_EDITOR
            private readonly List<int> _previewItems = new List<int>();
            private bool _threePreviewTargets;
            private bool _seedPreviewBurn;
#endif
            public override void OnEnter()
            {
                base.OnEnter();
                _itemTablel = GameEntry.DataTable.GetDataTable<DRItem>("Item");
                _itemIDList.Clear();
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
                    GUILayout.Label($"{GameEntry.Localization.GetString(_itemTablel[_itemIDList[_curSliderSelectIndex]].Name)},ID:{_itemIDList[_curSliderSelectIndex]}");
                    // 获取输入的文本
                    string newText = GUILayout.TextField(itemNum.ToString(), GUILayout.Width(200));
                    // 只保留整数字符（包括负号）
                    itemNum = RemoveNonIntegerCharactersAndConvertToInt(newText);
#if UNITY_EDITOR
                    var timeline = UnityEngine.Object.FindFirstObjectByType<WorldTimelineDebug.WorldTimelineDebugView>();
                    if (timeline != null)
                    {
                        if (GUILayout.Button("添加道具到轮回背包"))
                        {
                            if (!int.TryParse(itemIDStr, out int grantId) || !_itemTablel.HasDataRow(grantId))
                                Debug.LogWarning("发放失败。请输入 Item 表中存在的道具 ID。");
                            else if (timeline.TryGrantNativeItem(grantId, itemNum))
                            {
                                Debug.Log($"已发放 {itemNum} 件 {GameEntry.Localization.GetString(_itemTablel[grantId].Name)} 到本世轮回背包。");
                                WorldTimelineInventoryCtrl.ShowGrantedItem(timeline, grantId);
                                HotGameEntry.Debugger.ShowFullWindow = false;
                            }
                            else
                                Debug.LogWarning("发放失败。请在轮回地图等待选择时输入 1～99 件，并检查存档是否可写。");
                        }
                    }
                    else
#endif
                    if (GUILayout.Button("添加道具"))
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(_itemIDList[_curSelectIndex],itemNum));
#if UNITY_EDITOR
                    if (timeline != null)
                    {
                        int selected = _itemIDList[_curSelectIndex];
                        GUILayout.Label($"试招装备（{_previewItems.Count} 件）");
                        if (GUILayout.Button("把当前道具加入试招") && !_previewItems.Contains(selected))
                            _previewItems.Add(selected);
                        int removeAt = -1;
                        for (int i = 0; i < _previewItems.Count; i++)
                        {
                            int id = _previewItems[i];
                            GUILayout.BeginHorizontal();
                            GUILayout.Label($"{GameEntry.Localization.GetString(_itemTablel[id].Name)} ({id})");
                            if (GUILayout.Button("移除", GUILayout.Width(80))) removeAt = i;
                            GUILayout.EndHorizontal();
                        }
                        if (removeAt >= 0) _previewItems.RemoveAt(removeAt);
                        if (_previewItems.Count > 0 && GUILayout.Button("清空试招装备")) _previewItems.Clear();
                        bool guiEnabled = GUI.enabled;
                        GUI.enabled = guiEnabled && !timeline.IsBusy;
                        _threePreviewTargets = GUILayout.Toggle(_threePreviewTargets, "三固定靶（两个相邻，一个远处）");
                        if (GUILayout.Button("试招：使用本世角色和背包"))
                        {
                            if (WorldTimelineBattleResolver.TryPreviewCurrentCharacter(timeline, _threePreviewTargets))
                                HotGameEntry.Debugger.ShowFullWindow = false;
                            else
                                Debug.LogWarning("请在轮回地图等待选择、且没有剧情弹窗时开始试招。");
                        }
                        if (GUILayout.Button("试招：雷印连锁") &&
                            WorldTimelineBattleResolver.TryPreview(timeline, new[] { 1, 9151, 9152, 9153 },
                                threeTargets: true, lightningFormation: true))
                            HotGameEntry.Debugger.ShowFullWindow = false;
                        if (GUILayout.Button("试招：飞剑归鞘") &&
                            WorldTimelineBattleResolver.TryPreview(timeline, new[] { 9, 10, 9104 }, threeTargets: true))
                            HotGameEntry.Debugger.ShowFullWindow = false;
                        if (GUILayout.Button("查看四位剧情角色（试招）"))
                        {
                            if (WorldTimelineBattleResolver.TryPreview(timeline, new[] { selected }, previewCast: true))
                                HotGameEntry.Debugger.ShowFullWindow = false;
                        }
                        GUILayout.BeginHorizontal();
                        PreviewMonster("试招：沉舟水妖", "WreckBeast");
                        PreviewMonster("试招：守隘石傀", "GateGolem");
                        PreviewMonster("试招：归墟阵灵", "RitualWraith");
                        GUILayout.EndHorizontal();

                        void PreviewMonster(string label, string appearance)
                        {
                            if (GUILayout.Button(label) &&
                                WorldTimelineBattleResolver.TryPreview(timeline, new[] { selected }, previewMonster: appearance))
                                HotGameEntry.Debugger.ShowFullWindow = false;
                        }
                        if (_threePreviewTargets)
                            _seedPreviewBurn = GUILayout.Toggle(_seedPreviewBurn, "主靶首次被主动技能命中前补满燃烧");
                        if (GUILayout.Button(_previewItems.Count == 0 ? "试招：使用当前道具" : "试招：使用整套装备"))
                        {
                            IReadOnlyList<int> items = _previewItems.Count == 0 ? new[] { selected } : _previewItems;
                            if (WorldTimelineBattleResolver.TryPreview(timeline, items, _threePreviewTargets,
                                    _threePreviewTargets && _seedPreviewBurn))
                                HotGameEntry.Debugger.ShowFullWindow = false;
                            else
                                Debug.LogWarning("请在轮回地图等待选择、且没有剧情弹窗时开始试招。");
                        }
                        GUI.enabled = guiEnabled;
                    }
#endif
                    if (GUILayout.Button("添加道具合成测试"))
                    {
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(1,5));
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(2,3));
                        GameEntry.Event.Fire(this,CMDGetItemEventArgs.Create(5,2));
                    }
                    if (GUILayout.Button("测试存档"))
                    {
                        HotGameEntry.HeroManager.Save();
                    }
                    if (GUILayout.Button("测试读档"))
                    {
                        HotGameEntry.HeroManager.Load();
                    }
                    if (GUILayout.Button("回到主界面"))
                    {
                        GameEntry.Event.Fire(this,ReturnToTitleEventArgs.Create());
                    }
                    // 获取输入的文本
                    string newCoinText = GUILayout.TextField(_changeCoinNum.ToString(), GUILayout.Width(200));
                    // 只保留整数字符（包括负号）
                    _changeCoinNum = RemoveNonIntegerCharactersAndConvertToInt(newCoinText);
                    if (GUILayout.Button("修改金币"))
                    {
                        HotGameEntry.HeroManager.TryAddCoin(_changeCoinNum);
                    }

                    if (GUILayout.Button("测试AI"))
                    {
                        HotGameEntry.ChatAI.SendMsg("所以你是谁啊？",OnAiChatTestCallback);
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

            private void OnAiChatTestCallback(string str)
            {
                Log.Info("AI Response:"+str);
            }
        }
    }
}
