using SkillSystem;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionSummonWave))]
    public sealed class ConditionSummonWaveEditor
    {
        public void OnGUIDraw(ConditionSummonWave condition)
        {
            EditorGUILayout.LabelField("上一波召唤物全部死亡才能再召唤");
            condition.MaxWaves = Mathf.Max(1, EditorGUILayout.IntField("最多召唤波数", condition.MaxWaves));
            condition.RepeatHealthPercent = EditorGUILayout.IntSlider("后续波次气血百分比", condition.RepeatHealthPercent, 0, 100);
            ConditionBaseEditor.ConditionBaseDraw(condition);
        }
    }
}
