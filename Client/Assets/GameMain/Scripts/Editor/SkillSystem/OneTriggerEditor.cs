using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(OneTrigger))]
    public class OneTriggerEditor
    {
        public static void OnGUIDraw(OneTrigger oneTrigger)
        {
            if (oneTrigger !=null)
            {
                oneTrigger.CurTriggerType =(TriggerType)EditorGUILayout.EnumPopup("触发类型",oneTrigger.CurTriggerType);
                var oldConditionType = oneTrigger.CurCondition.CurConditionType;
                GUILayout.Space(10); // 添加一点空隙
                GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 绘制横线
                GUILayout.Space(10); // 添加一点空隙
                var newConditionType = (ConditionType)EditorGUILayout.EnumPopup("触发条件",oneTrigger.CurCondition.CurConditionType);
                if (newConditionType != oldConditionType)
                {
                    oneTrigger.CurCondition = SkillFactory.CreateCondition(newConditionType);
                }
                SkillSystemDrawerCenter.DrawOneInstance(oneTrigger.CurCondition);
                GUILayout.Space(10); // 添加一点空隙
                GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 绘制横线
                GUILayout.Space(10); // 添加一点空隙
                var oldTargetPickerType = oneTrigger.CurTargetPicker.CurTargetPickerType;
                var newTargetPickerType = (TargetPickerType)EditorGUILayout.EnumPopup("目标选择",oneTrigger.CurTargetPicker.CurTargetPickerType);
                if (oldTargetPickerType != newTargetPickerType)
                {
                    oneTrigger.CurTargetPicker = SkillFactory.CreateTargetPicker(newTargetPickerType);
                }
                SkillSystemDrawerCenter.DrawOneInstance(oneTrigger.CurTargetPicker);
                GUILayout.Space(10); // 添加一点空隙
                GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // 绘制横线
                GUILayout.Space(10); // 添加一点空隙
                if (GUILayout.Button("添加Command"))
                {
                    oneTrigger.CurCommandList.Add(SkillFactory.CreateCommand(CommandType.CauseDamage));
                }

                if (oneTrigger.CurCommandList != null && oneTrigger.CurCommandList.Count != 0)
                {
                    EditorGUILayout.BeginVertical();
                    
                    for (var commandIndex = 0; commandIndex < oneTrigger.CurCommandList.Count; commandIndex++)
                    {
                        var oneCommand = oneTrigger.CurCommandList[commandIndex];
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.BeginVertical(GUILayout.Width(20));
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            oneTrigger.CurCommandList.Remove(oneCommand);
                        }
                        if (GUILayout.Button("C", GUILayout.Width(20)))//clone
                        {
                            var cloneCmd = SkillFactory.CreateCommand(oneCommand.CurCommandType);
                            oneCommand.Clone(cloneCmd);
                            oneTrigger.CurCommandList.Add(cloneCmd);
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        // 绘制边界线
                        Rect rect = GUILayoutUtility.GetRect(0, 0);
                        EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y, 2, rect.height), Color.black);
                        var oldCommandType = oneCommand.CurCommandType;
                        var newCommandType =
                            (CommandType)EditorGUILayout.EnumPopup("Command类型", oneCommand.CurCommandType);
                        if (oldCommandType != newCommandType)
                        {
                            oneTrigger.CurCommandList[commandIndex] = SkillFactory.CreateCommand(newCommandType);
                        }
                        SkillSystemDrawerCenter.DrawOneInstance(oneCommand);
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(5);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }
                
            }
        }
    }
}