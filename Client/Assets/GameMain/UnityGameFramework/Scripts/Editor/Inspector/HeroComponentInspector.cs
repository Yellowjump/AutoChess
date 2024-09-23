//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Entity;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace UnityGameFramework.Editor
{
    [CustomEditor(typeof(HeroComponent))]
    internal sealed class HeroComponentInspector : GameFrameworkInspector
    {

        private bool m_ShowHeroFriend = false;
        private bool m_ShowHeroEnemy = false;
        public override void OnInspectorGUI()
        {
            base.ShowUnityInspectorGUI();

            serializedObject.Update();

            HeroComponent heroComponent = (HeroComponent)target;

            if (EditorApplication.isPlaying && IsPrefabInHierarchy(heroComponent.gameObject))
            {
                m_ShowHeroFriend = EditorGUILayout.Foldout(m_ShowHeroFriend, "友方");
                if (m_ShowHeroFriend)
                {
                    ShowHeroList(heroComponent.QiziCSList);
                }
                m_ShowHeroEnemy = EditorGUILayout.Foldout(m_ShowHeroEnemy, "敌方");
                if (m_ShowHeroEnemy)
                {
                    ShowHeroList(heroComponent.DirenList);
                }
            }
            serializedObject.ApplyModifiedProperties();

            Repaint();
        }

        private void ShowHeroList(List<EntityQizi> heroList)
        {
            foreach (var oneQizi in heroList)
            {
                GUILayout.Label("装备道具");
                GUILayout.BeginHorizontal();
                foreach (var item in oneQizi.EquipItemList)
                {
                    GUILayout.Label($"道具ID:{item}");
                }
                GUILayout.EndHorizontal();
                foreach (var oneSkill in oneQizi.NormalSkillList)
                {
                    GUILayout.Label($"SkillID:{oneSkill.SkillID},LeftCD:{oneSkill.LeftSkillCD}");
                }
                foreach (var oneSkill in oneQizi.NoAnimAtkSkillList)
                {
                    GUILayout.Label($"SkillID:{oneSkill.SkillID},LeftCD:{oneSkill.LeftSkillCD}");
                }
                foreach (var oneBuff in oneQizi.CurBuffList)
                {
                    if (oneBuff.DurationMs == 0)
                    {
                        GUILayout.Label($"BuffID:{oneBuff.BuffID},LeftTime:NoLimit");
                    }
                    else
                    {
                        GUILayout.Label($"BuffID:{oneBuff.BuffID},LeftTime:{oneBuff.RemainMs}");
                    }
                }
            }
        }
        private void OnEnable()
        {
        }
    }
}
