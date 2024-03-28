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
    private Vector2 skillIDGroupScrollPosition = Vector2.zero;
    private Vector2 rightSkillDetailScrollPosition = Vector2.zero;
    private TriggerList CurShowTriggerList;
    readonly string dataTablesPath = Application.dataPath + @"/Data/SkillTemplate";
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
        DirectoryInfo directoryInfo = new DirectoryInfo(dataTablesPath);
        FileInfo[] fis = directoryInfo.GetFiles("*.bytes", SearchOption.AllDirectories);
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
        SkillSystemDrawerCenter.RefreshDrawerMethod();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("新建"))
        {
            SkillIDList.Add("0");
            selectedButtonIndex = SkillIDList.Count - 1;
            CurShowTriggerList = SkillFactory.CreateNewEmptyTriggerList();
            SkillSystemDrawerCenter.ClearDrawInstanceMap();
        }

        if(GUILayout.Button("保存"))
        {
            var triggerEditor = SkillSystemDrawerCenter.GetTriggerListEditor(CurShowTriggerList);
            if (triggerEditor != null&&triggerEditor.OldTempleteID!= CurShowTriggerList.TempleteID)
            {
                //删除旧byte文件
                DelSkillFile(triggerEditor.OldTempleteID.ToString());
            }

            SaveSkillFile(CurShowTriggerList.TempleteID.ToString());
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        // Left Area with Vertical Scroll View
        GUILayout.BeginVertical(GUILayout.Width(150), GUILayout.MinWidth(150)); // 设置最小宽度
        skillIDGroupScrollPosition = GUILayout.BeginScrollView(skillIDGroupScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

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
                if (i != selectedButtonIndex)
                {
                    SkillSystemDrawerCenter.ClearDrawInstanceMap();
                    ReadSkillFile(SkillIDList[i]);
                }
                selectedButtonIndex = i;
            }
        }

        GUI.backgroundColor = Color.white; // 恢复默认背景色
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Right Area
        GUILayout.BeginVertical();
        rightSkillDetailScrollPosition = GUILayout.BeginScrollView(rightSkillDetailScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
        if (selectedButtonIndex != -1)
        {
            EditorGUILayout.LabelField("Selected Button: " + SkillIDList[selectedButtonIndex]);

            GUILayout.Space(10);
            if (CurShowTriggerList != null)
            {
                SkillSystemDrawerCenter.DrawOneInstance(CurShowTriggerList);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DelSkillFile(string sKillName)
    {
        string filePath = dataTablesPath + $"/{sKillName}.bytes"; // 文件路径，根据实际情况修改

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    private void SaveSkillFile(string sKillName)
    {
        string filePath = dataTablesPath + $"/{sKillName}.bytes"; // 文件路径，根据实际情况修改

        /*if (!File.Exists(filePath))
        {
            File.Create(filePath);
        }*/

        using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            CurShowTriggerList.WriteToFile(writer);
        }
    }

    private void ReadSkillFile(string sKillName)
    {
        string filePath = dataTablesPath + $"/{sKillName}.bytes"; // 文件路径，根据实际情况修改

        /*if (!File.Exists(filePath))
        {
            File.Create(filePath);
        }*/
        CurShowTriggerList = SkillFactory.CreateNewEmptyTriggerList();
        using (var writer = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
            CurShowTriggerList.ReadFromFile(writer);
        }
    }
}
