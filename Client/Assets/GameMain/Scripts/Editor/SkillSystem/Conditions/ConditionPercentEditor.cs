using SkillSystem;
namespace Editor.SkillSystem.Conditions
{
    [SkillDrawer(typeof(ConditionPercent))]
    public class ConditionPercentEditor
    {
        public void OnGUIDraw(ConditionPercent conditionPercent)
        {
            if (conditionPercent == null) return;
            SkillSystemDrawerCenter.DrawOneInstance(conditionPercent.PercentTarget);
            ConditionBaseEditor.ConditionBaseDraw(conditionPercent);
        }
    }
}