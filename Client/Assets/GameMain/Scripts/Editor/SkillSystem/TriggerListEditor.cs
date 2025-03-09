using System;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(TriggerList))]
    public class TriggerListEditor
    {
        public bool ShowDetail = true;
        public void OnGUIDraw(TriggerList triggerList)
        {
            DrawTriggerList(triggerList);
        }

        public void DrawTriggerList(TriggerList triggerList)
        {
            if (triggerList != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("添加触发器"))
                {
                    triggerList.CurTriggerList.Add(SkillFactory.CreateNewDefaultTrigger());
                }
                if (GUILayout.Button("添加复制的触发器"))
                {
                    var obj = SkillSystemDrawerCenter.PasteObject();
                    if (obj is OneTrigger copyTrigger)
                    {
                        var cloneOneTrigger = SkillFactory.CreateNewDefaultTrigger();
                        copyTrigger.Clone(cloneOneTrigger);
                        triggerList.CurTriggerList.Add(cloneOneTrigger);
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(); // 开始水平布局
                var tempBtnStr = ShowDetail ? "^" : "v";
                if (GUILayout.Button(tempBtnStr, GUILayout.Width(20)))//clone
                {
                    ShowDetail = !ShowDetail;
                }
                // 左边界右移 20 像素
                //GUILayout.Space(20);
                EditorGUILayout.BeginVertical();
                if (ShowDetail)
                {
                    for (var triggerIndex = 0; triggerIndex < triggerList.CurTriggerList.Count; triggerIndex++)
                    {
                        var oneTrigger = triggerList.CurTriggerList[triggerIndex];
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.BeginVertical(GUILayout.Width(20));
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            triggerList.CurTriggerList.Remove(oneTrigger);
                        }

                        if (GUILayout.Button("C", GUILayout.Width(20))) //copy
                        {
                            SkillSystemDrawerCenter.CopyOneObject(oneTrigger);
                            EditorUtility.DisplayDialog("提示", "已复制到粘贴板", "确认");
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        // 绘制边界线
                        Rect rect = GUILayoutUtility.GetRect(0, 0);
                        EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, 2, rect.height), Color.black);
                        SkillSystemDrawerCenter.DrawOneInstance(oneTrigger);
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(5);

                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label($"缩略的触发器 一共{triggerList.CurTriggerList.Count}个");
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal(); // 结束水平布局
            }
        }
    }
}