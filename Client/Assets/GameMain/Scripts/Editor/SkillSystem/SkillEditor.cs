using SkillSystem;
using UnityEditor;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(Skill))]
    public class SkillEditor
    {
        public int OldTempleteID = -1;
        public void OnGUIDraw(Skill skill)
        {
            if (OldTempleteID == -1)
            {
                OldTempleteID = skill.TempleteID;
            }
            skill.TempleteID = EditorGUILayout.IntField("当前技能模板ID:", skill.TempleteID);
            TriggerListEditor.DrawTriggerList(skill);
        }
        
    }
}