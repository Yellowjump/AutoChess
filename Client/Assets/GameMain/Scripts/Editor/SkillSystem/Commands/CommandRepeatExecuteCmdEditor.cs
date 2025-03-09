using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandRepeatExecuteCmd))]
    public class CommandRepeatExecuteCmdEditor
    {
        public void OnGUIDraw(CommandRepeatExecuteCmd commandRepeatExecuteCmd)
        {
            if (commandRepeatExecuteCmd != null)
            {
                commandRepeatExecuteCmd.CurType = (NumberCheckType)EditorGUILayout.EnumPopup("重复次数类型", commandRepeatExecuteCmd.CurType);
                if (commandRepeatExecuteCmd.CurType == NumberCheckType.FixedNumber)
                {
                    EditorGUILayout.LabelField("固定次数:");
                }
                else if (commandRepeatExecuteCmd.CurType == NumberCheckType.ParentSkillContainSubItemNumber)
                {
                    EditorGUILayout.LabelField("目标子itemID：");
                }
                SkillSystemDrawerCenter.DrawOneInstance(commandRepeatExecuteCmd.ParamInt1);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("添加Command"))
                {
                    commandRepeatExecuteCmd.CurCommandList.Add(SkillFactory.CreateCommand(CommandType.CauseDamage));
                }
                if (GUILayout.Button("添加复制的Command"))
                {
                    var obj = SkillSystemDrawerCenter.PasteObject();
                    if (obj is CommandBase copyCommand)
                    {
                        
                        var newCommand = SkillFactory.CreateCommand(copyCommand.CurCommandType);
                        copyCommand.Clone(newCommand);
                        commandRepeatExecuteCmd.CurCommandList.Add(newCommand);
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (commandRepeatExecuteCmd.CurCommandList != null && commandRepeatExecuteCmd.CurCommandList.Count != 0)
                {
                    EditorGUILayout.BeginVertical();
                    
                    for (var commandIndex = 0; commandIndex < commandRepeatExecuteCmd.CurCommandList.Count; commandIndex++)
                    {
                        var oneCommand = commandRepeatExecuteCmd.CurCommandList[commandIndex];
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.BeginVertical(GUILayout.Width(20));
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            commandRepeatExecuteCmd.CurCommandList.Remove(oneCommand);
                        }
                        if (GUILayout.Button("C", GUILayout.Width(20)))//clone
                        {
                            SkillSystemDrawerCenter.CopyOneObject(oneCommand);
                            EditorUtility.DisplayDialog("提示", "已复制到粘贴板", "确认");
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
                            commandRepeatExecuteCmd.CurCommandList[commandIndex] = SkillFactory.CreateCommand(newCommandType);
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