using SkillSystem;
using UnityEditor;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(Skill))]
    public class SkillEditor
    {
        public int OldTempleteID = -1;
        private TriggerListEditor _triggerListEditorInstance;
        public void OnGUIDraw(Skill skill)
        {
            if (OldTempleteID == -1)
            {
                OldTempleteID = skill.TempleteID;
            }
            skill.TempleteID = EditorGUILayout.IntField("当前技能模板ID:", skill.TempleteID);
            skill.EditorDesc = EditorGUILayout.TextField("描述:", skill.EditorDesc);
            _triggerListEditorInstance ??= new TriggerListEditor();
            _triggerListEditorInstance.OnGUIDraw(skill);
            //SkillSystemDrawerCenter.DrawOneInstance((TriggerList)skill);
            //TriggerListEditor.DrawTriggerList(skill);
        }
        
    }
}