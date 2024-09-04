using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [SkillDrawer(typeof(Buff))]
    public class BuffEditor
    {
        public int OldTempleteID = -1;
        public void OnGUIDraw(Buff buff)
        {
            if (OldTempleteID == -1)
            {
                OldTempleteID = buff.TempleteID;
            }
            buff.TempleteID = EditorGUILayout.IntField("当前buff模板ID:", buff.TempleteID);
            buff.OwnBuffTag = (BuffTag)EditorGUILayout.EnumFlagsField("buff Tag",  buff.OwnBuffTag);
            TriggerListEditor.DrawTriggerList(buff);
        }
        
    }
}