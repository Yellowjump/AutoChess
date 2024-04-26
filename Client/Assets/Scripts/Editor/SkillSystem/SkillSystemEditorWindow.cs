using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DataTable.Editor.DataTableTools;
using Editor.SkillSystem;
using GameFramework.DataTable;
using SkillSystem;
using UnityEngine;
using UnityEditor;
using UnityGameFramework.Runtime;

public class SkillSystemEditorWindow : EditorWindow
{
    private List<int> SkillIDList =new List<int>();
    private Dictionary<int, (bool, TriggerList)> TriggerListMap = new Dictionary<int, (bool, TriggerList)>();
    private int selectedButtonIndex = -1;
    private Vector2 skillIDGroupScrollPosition = Vector2.zero;
    private Vector2 rightSkillDetailScrollPosition = Vector2.zero;
    private TriggerList CurShowTriggerList;
    readonly string dataTablesPath = Application.dataPath + @"/Data/SkillTemplate";
    readonly string templateTableFilePath = Application.dataPath + @"/Data/DataTables/SkillTemplate.txt";
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
            var SkillID = int.Parse(Path.GetFileNameWithoutExtension(fis[i].Name));
            SkillIDList.Add(SkillID);
            var newEmptyTriggerList = SkillFactory.CreateNewEmptyTriggerList();
            string filePath = dataTablesPath + $"/{SkillID}.bytes"; // 文件路径，根据实际情况修改
            using (var writer = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                newEmptyTriggerList.ReadFromFile(writer);
            }
            TriggerListMap.Add(SkillID,(false,newEmptyTriggerList));
        }
        SkillIDList.Sort();
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
            if (SkillIDList.Contains(0))
            {
                EditorUtility.DisplayDialog("提示", "已经有一个新建模板0未保存了", "确认");
            }
            else
            {
                SkillIDList.Add(0);
                selectedButtonIndex = SkillIDList.Count - 1;
                CurShowTriggerList = SkillFactory.CreateNewEmptyTriggerList();
                TriggerListMap.Add(0,(true,CurShowTriggerList));
                SkillSystemDrawerCenter.ClearDrawInstanceMap();
            }
        }

        if(GUILayout.Button("保存"))
        {
            if (SaveSkillTemp(CurShowTriggerList))
            {
                //保存重写进txt，并且生成byte
                SaveToTxt();
                DataTableGeneratorMenu.GenerateSkillTemplateDataTables();
            }
        }

        if (GUILayout.Button("保存全部"))
        {
            bool needSave = false;
            foreach (var skillID in SkillIDList)
            {
                if (TriggerListMap.TryGetValue(skillID, out var skillTempPair))
                {
                    if (skillTempPair.Item1)
                    {
                        needSave = true;
                        SaveSkillTemp(skillTempPair.Item2);
                    }
                }
            }
            if (needSave)
            {
                //保存重写进txt，并且生成byte
                SaveToTxt();
                DataTableGeneratorMenu.GenerateSkillTemplateDataTables();
            }
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
                if (TriggerListMap[SkillIDList[i]].Item1)
                {
                    GUI.backgroundColor = Color.magenta; // 选中已修改
                }
                else
                {
                    GUI.backgroundColor = Color.blue; // 设置选中按钮的背景色
                }
            }
            else
            {
                if (TriggerListMap[SkillIDList[i]].Item1)
                {
                    GUI.backgroundColor = Color.red; // 恢复默认背景色
                }
                else
                {
                    GUI.backgroundColor = Color.white; // 恢复默认背景色
                }
            }

            if (GUILayout.Button(SkillIDList[i].ToString(), GUILayout.MinWidth(90),GUILayout.MaxWidth(120))) // 设置按钮最小宽度
            {
                if (i != selectedButtonIndex)
                {
                    GUI.FocusControl(null);
                    SkillSystemDrawerCenter.ClearDrawInstanceMap();
                    CurShowTriggerList = TriggerListMap[SkillIDList[i]].Item2;
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
                EditorGUI.BeginChangeCheck();
                SkillSystemDrawerCenter.DrawOneInstance(CurShowTriggerList);
                if (EditorGUI.EndChangeCheck())
                {
                    TriggerListMap[SkillIDList[selectedButtonIndex]] = (true, CurShowTriggerList);
                }
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
    private void SaveSkillFile(TriggerList triggerList)
    {
        string filePath = dataTablesPath + $"/{triggerList.TempleteID}.bytes"; // 文件路径，根据实际情况修改
        using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            CurShowTriggerList.WriteToFile(writer);
        }
    }

    private bool SaveSkillTemp(TriggerList triggerList)
    {
        if (triggerList.TempleteID == 0)
        {
            EditorUtility.DisplayDialog("提示", "模板ID不能保存为0", "确认");
        }
        else
        {
            var triggerEditor = SkillSystemDrawerCenter.GetTriggerListEditor(triggerList);
            if (triggerEditor != null)
            {
                if (triggerEditor.OldTempleteID != triggerList.TempleteID && SkillIDList.Contains(triggerList.TempleteID))
                {
                    EditorUtility.DisplayDialog("提示", $"模板ID{triggerList.TempleteID} 已存在", "确认");
                }
                else
                {
                    if (triggerEditor.OldTempleteID != triggerList.TempleteID)
                    {
                        //删除旧byte文件
                        DelSkillFile(triggerEditor.OldTempleteID.ToString());
                        if (TriggerListMap.ContainsKey(triggerEditor.OldTempleteID))
                        {
                            TriggerListMap.Remove(triggerEditor.OldTempleteID);
                        }
                        if (SkillIDList.Contains(triggerEditor.OldTempleteID))
                        {
                            SkillIDList.Remove(triggerEditor.OldTempleteID);
                        }
                    }
                    SaveSkillFile(triggerList);
                    TriggerListMap[triggerList.TempleteID] = (false,TriggerListMap[triggerList.TempleteID].Item2);
                    return true;
                }
            }
        }
        return false;
    }
    private void SaveToTxt()
    {
        StringBuilder tableBuild = new StringBuilder();
        tableBuild.AppendLine("#\t技能模板表\t");
        tableBuild.AppendLine("#\tId\tSkill");
        tableBuild.AppendLine("#\tint\tTriggerList");
        tableBuild.AppendLine("#\t模板ID\t技能");
        foreach (var skillID in SkillIDList)
        {
            if (TriggerListMap.TryGetValue(skillID, out var skillTempPair))
            {
                if (skillTempPair.Item1)
                {
                    //有修改还未保存使用 byte文件中的数据
                    string filePath = dataTablesPath + $"/{skillID}.bytes"; // 文件路径，根据实际情况修改
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            using (var fileStream = File.Open(filePath, FileMode.Open))
                            {
                                using (var reader = new BinaryReader(fileStream))
                                {
                                    var bytes = reader.ReadBytes((int)fileStream.Length);
                                    tableBuild.AppendLine($"\t{skillID}\t{Convert.ToBase64String(bytes)}");
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogError($"open file{filePath} error:{exception.ToString()}");
                        }
                    }
                }
                else
                {
                    var triggerList = skillTempPair.Item2;
                    using (var newMemorySteam = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(newMemorySteam))
                        {
                            triggerList.WriteToFile(writer);
                        }
                        var array = newMemorySteam.ToArray();
                        tableBuild.AppendLine($"\t{skillID}\t{Convert.ToBase64String(array)}");
                    }
                }
            }
        }
        using (FileStream fileStream = new FileStream(templateTableFilePath, FileMode.Create, FileAccess.Write))
        {
            using (StreamWriter stream = new StreamWriter(fileStream, Encoding.UTF8))
            {
                stream.Write(tableBuild.ToString());
            }
        }
    }
}
