using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DataTable;
using SkillSystem;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    public partial class HeroComponent
    {
        private Dictionary<string, AttributeType> attributeTypeMap;
        private Dictionary<AttributeType, Color> attrHighlightColor;
        private Color whiteColor = new Color(0.93f, 0.93f, 0.93f);
        public string FormatItemDesc(string desc, int heroUID)
        {
            currentHeroUID = heroUID;
            return Regex.Replace(desc, @"\{0\} = \[(.*?)\]", MatchSkillDescription);
        }
        private string MatchSkillDescription(Match match)
        {
            return FormatSkillDescription(match.Value, currentHeroUID);
        }

        private int currentHeroUID; // 临时存储 heroUID
        private string FormatSkillDescription(string template, int heroUID)
        {
            
            string formula = ExtractFormula(template); // 获取 [20 + 10%最大生命值]
            if (heroUID!=0)
            {
                float result = EvaluateFormula(formula,heroUID); // 计算最终数值
                string formattedResult = $"<color=#FF5500>{result:0}</color>"; // 橙色最终值
                string formattedFormula = HighlightFormula(formula); // 让公式变色

                return template.Replace("{0}", formattedResult).Replace($"[{formula}]", formattedFormula);
            }
            else
            {
                // 仅高亮公式，不计算最终值
                return template.Replace("{0} = ", "").Replace($"[{formula}]", HighlightFormula(formula));
            }
        }
        private string ExtractFormula(string template)
        {
            Match match = Regex.Match(template, @"\[(.*?)\]"); // 匹配 [ ] 之间的内容
            return match.Success ? match.Groups[1].Value : "";
        }
        private float EvaluateFormula(string formula,int heroUID)
        {
            float result = 0;

            // 解析百分比属性，例如 "10%最大生命值"
            string processedFormula = Regex.Replace(formula, @"(\d+)%(\w+)", match =>
            {
                float percent = float.Parse(match.Groups[1].Value) / 100f;
                string attr = match.Groups[2].Value;
                var localizedAttr = GameEntry.Localization.GetString(attr);
                if (attributeTypeMap.TryGetValue(attr, out AttributeType attrType))
                {
                    return (percent * GetHeroAttrValue(heroUID,attrType)).ToString();
                }
                return match.Value;
            });
            try
            {
                result = Evaluate(processedFormula);
            }
            catch
            {
                Console.WriteLine("计算失败: " + processedFormula);
            }
            return result;
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
                { EnumLanguage.Attr_Power.ToString(), AttributeType.Power },
                { EnumLanguage.Attr_MaxPower.ToString(), AttributeType.MaxPower },
                { EnumLanguage.Attr_HuDun.ToString(), AttributeType.HuDun },
                { EnumLanguage.Attr_HuDunBoost.ToString(), AttributeType.HuDunBoost },
                { EnumLanguage.Attr_CooldownReduce.ToString(), AttributeType.CooldownReduce },
                { EnumLanguage.Attr_ATK.ToString(), AttributeType.AttackDamage },
            };
            var hpColor = new Color(0, 0.4f, 0);
            var mpColor = new Color(0, 0.4f, 0.8f);
            var shieldColor = new Color(0.6f, 0.6f, 0.1f);
            var atkColor = new Color(0.8f, 0.5f, 0.1f);
            attrHighlightColor = new Dictionary<AttributeType, Color>()
            {
                { AttributeType.Hp, hpColor },
                { AttributeType.MaxHp, hpColor },
                { AttributeType.Power, mpColor },
                { AttributeType.MaxPower, mpColor },
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
                    sum += float.Parse(match.Value);
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