using System;
using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    
    [SkillDrawer(typeof(TriggerList))]
    public class TriggerListEditor
    {
        public int OldTempleteID = -1;
        public void OnGUIDraw(TriggerList triggerList)
        {
            if (triggerList !=null)
            {
                if (OldTempleteID == -1)
                {
                    OldTempleteID = triggerList.TempleteID;
                }
                triggerList.TempleteID = EditorGUILayout.IntField("当前技能模板ID:", triggerList.TempleteID);
                if (GUILayout.Button("添加触发器"))
                {
                    triggerList.CurTriggerList.Add(SkillFactory.CreateNewDefaultTrigger());
                }
                EditorGUILayout.BeginHorizontal(); // 开始水平布局

                // 左边界右移 20 像素
                GUILayout.Space(20);
                EditorGUILayout.BeginVertical();
                for (var triggerIndex = 0; triggerIndex < triggerList.CurTriggerList.Count; triggerIndex++)
                {
                    var oneTrigger = triggerList.CurTriggerList[triggerIndex];
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        triggerList.CurTriggerList.Remove(oneTrigger);
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    // 绘制边界线
                    Rect rect = GUILayoutUtility.GetRect(0, 0);
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, 2, rect.height), Color.black);
                    SkillSystemDrawerCenter.DrawOneInstance(oneTrigger);
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(5);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal(); // 结束水平布局
            }
        }
    }
}