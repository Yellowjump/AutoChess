using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Editor.SkillSystem
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SkillDrawerAttribute : Attribute
    {
        public Type DrawerType { get; private set; }

        public SkillDrawerAttribute(Type drawerType)
        {
            DrawerType = drawerType;
        }
    }
    public class EnumDisplayNameAttribute : PropertyAttribute
    {
        public string DisplayName { get; private set; }

        public EnumDisplayNameAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }

    [CustomPropertyDrawer(typeof(EnumDisplayNameAttribute))]
    public class EnumDisplayNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnumDisplayNameAttribute enumDisplayName = attribute as EnumDisplayNameAttribute;
            EditorGUI.LabelField(position, enumDisplayName.DisplayName);
        }
    }
    public class SkillSystemDrawer
    {
        public static Dictionary<Type, MethodInfo> drawFieldMethods = new Dictionary<Type, MethodInfo>();

        public static void RefreshDrawerMethod()
        {
            drawFieldMethods.Clear();
            var fieldDrawerTypes = new Dictionary<Type, Type>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                object[] attributes = type.GetCustomAttributes(typeof(SkillDrawerAttribute), false);
                if (attributes.Length > 0)
                {
                    SkillDrawerAttribute drawerAttribute = attributes[0] as SkillDrawerAttribute;
                    if (drawerAttribute != null)
                    {
                        fieldDrawerTypes.Add(type, drawerAttribute.DrawerType);
                    }
                }
            }

            foreach (var pair in fieldDrawerTypes)
            {
                Type drawerType = pair.Key;
                MethodInfo method = drawerType.GetMethod("OnGUIDraw");
                if (method != null)
                {
                    drawFieldMethods.Add(pair.Value, method);
                }
            }
        }

        public static void DrawOneInstance(object target)
        {
            if (drawFieldMethods == null)
            {
                return;
            }
            if (drawFieldMethods.TryGetValue(target.GetType(), out var drawMeth))
            {
                drawMeth.Invoke(null,new object[] {target});
            }
        }
    }
}