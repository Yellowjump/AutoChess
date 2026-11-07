using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DataTable;
using SkillSystem;
using UnityEngine;
using WorldTimeline;

namespace UnityGameFramework.Runtime
{
    public partial class HeroComponent
    {
        internal const string TimelineSlashDescriptionKey = "Timeline_Technique_SlashDescription";
        private Dictionary<string, AttributeType> attributeTypeMap;
        private Dictionary<AttributeType, Color> attrHighlightColor;
        private Color whiteColor = new Color(0.93f, 0.93f, 0.93f);
        public string FormatItemDesc(string desc, int heroUID) =>
            FormatItemDesc(desc, heroUID == 0 ? null : type => GetHeroAttrValue(heroUID, type));

        public string FormatTimelineItemDesc(string desc, ActorState actor) =>
            FormatItemDesc(desc, type => type switch
            {
                AttributeType.Hp => actor.Health,
                AttributeType.MaxHp => actor.MaxHealth,
                AttributeType.AttackDamage => actor.CombatAttack,
                _ => (int?)null
            });

        private string FormatItemDesc(string desc, Func<AttributeType, int?> getAttribute)
        {
            if (string.IsNullOrEmpty(desc))
            {
                return string.Empty;
            }
            return Regex.Replace(desc, @"\{0\} = \[(.*?)\]", match =>
            {
                string formula = match.Groups[1].Value;
                string formattedFormula = HighlightFormula(formula);
                if (getAttribute != null && TryEvaluateFormula(formula, getAttribute, out float result))
                    return $"<color=#FF5500>{result:0}</color> = {formattedFormula}";
                return formattedFormula;
            });
        }
        private bool TryEvaluateFormula(string formula, Func<AttributeType, int?> getAttribute, out float result)
        {
            bool available = true;
            string processedFormula = Regex.Replace(formula, @"(\d+)%(\w+)", match =>
            {
                if (attributeTypeMap.TryGetValue(match.Groups[2].Value, out var type) &&
                    getAttribute(type) is int value)
                {
                    float percent = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 100f;
                    return (percent * value).ToString(CultureInfo.InvariantCulture);
                }
                available = false;
                return match.Value;
            });
            result = available ? Evaluate(processedFormula) : 0;
            return available;
        }

        private string HighlightFormula(string formula)
        {
            // 让 **独立数值** 变色（排除 "10%" 这种情况）
            string highlighted = Regex.Replace(formula, @"(?<![#%])\b(\d+)\b(?!%)", "<color=#eeeeee>$1</color>");
            // 让百分比属性变色
            highlighted = Regex.Replace(highlighted, @"(\d+)%(\w+)", DelAttrAndColor);
            return highlighted;
        }
        private string DelAttrAndColor(Match match)
        {
            string attr = match.Groups[2].Value;
            if (attributeTypeMap.TryGetValue(attr, out AttributeType attrType))
            {
                if (!attrHighlightColor.TryGetValue(attrType, out Color color))
                {
                    color = whiteColor;
                }
                var hex = ColorUtility.ToHtmlStringRGB(color);
                var localizedAttr = GameEntry.Localization.GetString(attr);
                return $"<color=#{hex}>{match.Groups[1].Value}%{localizedAttr}</color>";
            }
            return match.Value;
        }
        public void InitSkillDecsFormat()
        {
            attributeTypeMap = new Dictionary<string, AttributeType>()
            {
                { EnumLanguage.Attr_Hp.ToString(), AttributeType.Hp },
                { EnumLanguage.Attr_MaxHp.ToString(), AttributeType.MaxHp },
                { EnumLanguage.Attr_HuDun.ToString(), AttributeType.HuDun },
                { EnumLanguage.Attr_HuDunBoost.ToString(), AttributeType.HuDunBoost },
                { EnumLanguage.Attr_CooldownReduce.ToString(), AttributeType.CooldownReduce },
                { EnumLanguage.Attr_ATK.ToString(), AttributeType.AttackDamage },
            };
            var hpColor = new Color(0, 0.4f, 0);
            var shieldColor = new Color(0.6f, 0.6f, 0.1f);
            var atkColor = new Color(0.8f, 0.5f, 0.1f);
            attrHighlightColor = new Dictionary<AttributeType, Color>()
            {
                { AttributeType.Hp, hpColor },
                { AttributeType.MaxHp, hpColor },
                { AttributeType.HuDunBoost, shieldColor },
                { AttributeType.AttackDamage, atkColor },
                { AttributeType.CooldownReduce, whiteColor },
            };
        }
        public float Evaluate(string expression)
        {
            try
            {
                // 1️⃣ 用正则提取所有数值（支持小数）
                MatchCollection matches = Regex.Matches(expression, @"-?\d+(\.\d+)?");

                float sum = 0;
                foreach (Match match in matches)
                {
                    sum += float.Parse(match.Value, CultureInfo.InvariantCulture);
                }

                return sum;
            }
            catch (Exception e)
            {
                Console.WriteLine("解析失败：" + e.Message);
                return 0;
            }
        }
        private int GetHeroAttrValue(int heroUID, AttributeType targetType)
        {
            var entity = GetEntityByUID(heroUID);
            if (entity != null)
            {
                var attr = entity.GetAttribute(targetType);
                if (attr != null)
                {
                    return (int)attr.GetFinalValue();
                }
            }
            return 0;
        }
    }
}
