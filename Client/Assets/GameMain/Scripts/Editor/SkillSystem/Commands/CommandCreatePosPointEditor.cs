using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Commands
{
    [SkillDrawer(typeof(CommandCreatePosPoint))]
    public class CommandCreatePosPointEditor
    {
        public void OnGUIDraw(CommandCreatePosPoint commandCreateBullet)
        {
            if (commandCreateBullet != null)
            {
                EditorGUILayout.LabelField("创建坐标点");
                commandCreateBullet.CurPositionType = (PosPointPositionType)EditorGUILayout.EnumPopup("位置类型", commandCreateBullet.CurPositionType);
                EditorGUILayout.LabelField("位置偏移");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.PosOffset);
                EditorGUILayout.LabelField("位置偏移随机最大值");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.PosOffsetMax);
                EditorGUILayout.LabelField("持续时间ms");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.DurationMS);
                EditorGUILayout.LabelField("是否看向目标");
                if (commandCreateBullet.LookAtTarget.CurMatchTable == GenerateEnumDataTables.None)
                {
                    commandCreateBullet.LookAtTarget.CurMatchTable = (GenerateEnumDataTables)EditorGUILayout.EnumPopup("选择读表", commandCreateBullet.LookAtTarget.CurMatchTable);
                    var tempBool = commandCreateBullet.LookAtTarget.Value == 1;
                    tempBool = EditorGUILayout.Toggle("是否看向目标", tempBool);
                    commandCreateBullet.LookAtTarget.Value = tempBool ? 1 : 0;
                }
                else
                {
                    SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.LookAtTarget);
                }
                EditorGUILayout.LabelField("子弹上的触发器");
                SkillSystemDrawerCenter.DrawOneInstance(commandCreateBullet.PosPointTrigger);
            }
        }
    }
}