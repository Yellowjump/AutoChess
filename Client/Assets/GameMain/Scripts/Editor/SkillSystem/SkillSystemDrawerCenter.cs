using System;
using System.Collections.Generic;
using System.Reflection;
using SkillSystem;
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
    public class SkillSystemDrawerCenter
    {
        private static Dictionary<Type, MethodInfo> _drawFieldMethods = new Dictionary<Type, MethodInfo>();
        private static Dictionary<Type, Type> _drawTypeMap = new Dictionary<Type, Type>();
        private static Dictionary<object,object> _drawInstanceMap = new Dictionary<object,object>();
        //---------粘贴板---------start-//
        private static object copyObject;
        //---------粘贴板---------end-//
        public static void RefreshDrawerMethod()
        {
            _drawFieldMethods.Clear();
            _drawTypeMap.Clear();
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
                        _drawTypeMap.Add(drawerAttribute.DrawerType,type);
                    }
                }
            }

            foreach (var pair in _drawTypeMap)
            {
                Type drawerType = pair.Value;
                MethodInfo method = drawerType.GetMethod("OnGUIDraw");
                if (method != null)
                {
                    _drawFieldMethods.Add(pair.Key, method);
                }
            }
        }

        public static void ClearDrawInstanceMap()
        {
            _drawInstanceMap.Clear();
        }
        public static void DrawOneInstance(object target)
        {
            if (_drawFieldMethods == null||target==null)
            {
                return;
            }
            var targetType = target.GetType();
            if (_drawFieldMethods.TryGetValue(targetType, out var drawMeth))
            {
                _drawInstanceMap.TryGetValue(target, out var drawInstance);
                if (drawInstance == null)
                {
                    _drawTypeMap.TryGetValue(targetType, out var drawType);
                    if (drawType == null)
                    {
                        // 显示错误提示框
                        EditorUtility.DisplayDialog("错误", $"Type：{targetType} 没有对应的 编辑器绘制类型", "确定");
                        return;
                    }
                    // 使用 Activator.CreateInstance 创建对象实例
                    drawInstance = Activator.CreateInstance(drawType);
                    _drawInstanceMap.Add(target,drawInstance);
                }
                drawMeth.Invoke(drawInstance,new object[] {target});
            }
        }

        public static TriggerListEditor GetTriggerListEditor(TriggerList triggerList)
        {
            if (triggerList==null|| _drawInstanceMap == null || _drawInstanceMap.Count == 0)
            {
                return null;
            }

            _drawInstanceMap.TryGetValue(triggerList, out var instance);
            if (instance is not TriggerListEditor triggerListEditor)
            {
                return null;
            }
            return triggerListEditor;
        }
        public static SkillEditor GetSkillEditor(Skill skill)
        {
            if (skill==null|| _drawInstanceMap == null || _drawInstanceMap.Count == 0)
            {
                return null;
            }

            _drawInstanceMap.TryGetValue(skill, out var instance);
            if (instance is not SkillEditor skillEditor)
            {
                return null;
            }
            return skillEditor;
        }
        public static BuffEditor GetBuffEditor(Buff buff)
        {
            if (buff==null|| _drawInstanceMap == null || _drawInstanceMap.Count == 0)
            {
                return null;
            }

            _drawInstanceMap.TryGetValue(buff, out var instance);
            if (instance is not BuffEditor skillEditor)
            {
                return null;
            }
            return skillEditor;
        }

        public static OneTriggerEditor GetOneTriggerEditor(OneTrigger oneTrigger)
        {
            if (oneTrigger==null|| _drawInstanceMap == null || _drawInstanceMap.Count == 0)
            {
                return null;
            }

            _drawInstanceMap.TryGetValue(oneTrigger, out var instance);
            if (instance is not OneTriggerEditor oneTriggerEditor)
            {
                return null;
            }
            return oneTriggerEditor;
        }

        public static void CopyOneObject(object obj)
        {
            copyObject = obj;
        }

        public static object PasteObject()
        {
            return copyObject;
        }
    }
}