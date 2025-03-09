using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(Buff))]
    public class BuffEditor
    {
        public int OldTempleteID = -1;
        private TriggerListEditor _triggerListEditorInstance;
        public void OnGUIDraw(Buff buff)
        {
            if (OldTempleteID == -1)
            {
                OldTempleteID = buff.TempleteID;
            }
            buff.TempleteID = EditorGUILayout.IntField("当前buff模板ID:", buff.TempleteID);
            buff.EditorDesc = EditorGUILayout.TextField("描述:", buff.EditorDesc);
            buff.OwnBuffTag = (BuffTag)EditorGUILayout.EnumFlagsField("buff Tag",  buff.OwnBuffTag);
            _triggerListEditorInstance ??= new TriggerListEditor();
            _triggerListEditorInstance.OnGUIDraw(buff);
        }
        
    }
}