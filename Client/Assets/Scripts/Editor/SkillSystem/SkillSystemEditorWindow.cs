using System;
using System.Collections.Generic;
using System.IO;
using Editor.SkillSystem;
using SkillSystem;
using UnityEngine;
using UnityEditor;

public class SkillSystemEditorWindow : EditorWindow
{
    private List<string> SkillIDList =new List<string>();
    private int selectedButtonIndex = -1;
    private Vector2 scrollPosition = Vector2.zero;
    private TriggerList CurShowTriggerList;
    [MenuItem("Window/技能编辑器")]
    public static void ShowWindow()
    {
        SkillSystemEditorWindow window = GetWindow<SkillSystemEditorWindow>("Custom Editor Window");
        window.minSize = new Vector2(400, 300); // 设置最小宽高
        
    }

    /// <summary>
    /// 读取所有skill二进制文件名并保存到skillIDList
    /// </summary>
    private void ReadAllSkillFile()
    {
        string dataTablesPath = Application.dataPath + @"/Data/SkillTemplate";
        DirectoryInfo directoryInfo = new DirectoryInfo(dataTablesPath);
        FileInfo[] fis = directoryInfo.GetFiles("*.byte", SearchOption.AllDirectories);
        SkillIDList.Clear();
        for (int i = 0; i < fis.Length; i++)
        {
            SkillIDList.Add(Path.GetFileNameWithoutExtension(fis[i].Name));
        }
        SkillIDList.Sort(string.Compare);
    }

    private void OnEnable()
    {
        ReadAllSkillFile();
        SkillSystemDrawer.RefreshDrawerMethod();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("新建"))
        {
            SkillIDList.Add("0");
            selectedButtonIndex = SkillIDList.Count - 1;
            CurShowTriggerList = SkillFactory.CreateNewEmptyTriggerList();
        }
        GUILayout.Button("保存");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        // Left Area with Vertical Scroll View
        GUILayout.BeginVertical(GUILayout.Width(150), GUILayout.MinWidth(150)); // 设置最小宽度
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

        for (int i = 0; i < SkillIDList.Count; i++)
        {
            if (selectedButtonIndex == i)
            {
                GUI.backgroundColor = Color.blue; // 设置选中按钮的背景色
            }
            else
            {
                GUI.backgroundColor = Color.white; // 恢复默认背景色
            }

            if (GUILayout.Button(SkillIDList[i], GUILayout.MinWidth(90),GUILayout.MaxWidth(120))) // 设置按钮最小宽度
            {
                selectedButtonIndex = i;
            }
        }

        GUI.backgroundColor = Color.white; // 恢复默认背景色
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Right Area
        GUILayout.BeginVertical();

        if (selectedButtonIndex != -1)
        {
            EditorGUILayout.LabelField("Selected Button: " + SkillIDList[selectedButtonIndex]);

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Enter some text:");
            EditorGUI.BeginChangeCheck();
            string textFieldValue = EditorGUILayout.TextField("Text Field", "");
            if (EditorGUI.EndChangeCheck())
            {
                Debug.Log("Text Field Value Changed: " + textFieldValue);
            }

            if (CurShowTriggerList != null)
            {
                SkillSystemDrawer.DrawOneInstance(CurShowTriggerList);
            }
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }
}
