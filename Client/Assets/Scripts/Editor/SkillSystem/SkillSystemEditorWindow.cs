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
    private enum ToggleName
    {
        [InspectorName("技能模板")]
        SkillTemplate,
        [InspectorName("buff模板")]
        BuffTemplate,
    }
    private int selectedButtonIndex = -1;
    private Vector2 idGroupScrollPosition = Vector2.zero;
    private Vector2 rightDetailScrollPosition = Vector2.zero;
    private int selectedToggleIndex = 0;
    //-------buff模板start
    private List<Buff> BuffIDList =new List<Buff>();
    private Dictionary<Buff,bool> BuffMap = new Dictionary<Buff,bool>();
    private Buff CurShowBuff;
    readonly string buffTemplatePath = Application.dataPath + @"/Data/BuffTemplate";
    readonly string buffTemplateTableFilePath = Application.dataPath + @"/Data/DataTables/BuffTemplate.txt";
    //-------buff模板end
    
    //-------技能模板start
    private List<Skill> SkillIDList =new List<Skill>();
    private Dictionary<Skill, bool> SkillMap = new Dictionary<Skill,bool>();
    private Skill CurShowSkill;
    readonly string skillTemplatePath = Application.dataPath + @"/Data/SkillTemplate";
    readonly string skillTemplateTableFilePath = Application.dataPath + @"/Data/DataTables/SkillTemplate.txt";
    //-------技能模板end
    
    [MenuItem("Window/技能编辑器")]
    public static void ShowWindow()
    {
        SkillSystemEditorWindow window = GetWindow<SkillSystemEditorWindow>("Custom Editor Window");
        window.minSize = new Vector2(400, 300); // 设置最小宽高
        
    }

    

    private void OnEnable()
    {
        ReadAllBuffFile();
        ReadAllSkillFile();
        SkillSystemDrawerCenter.RefreshDrawerMethod();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        //第一排放 skill模板还是技能模板 的单选框
        // 遍历变量列表，创建Toggle List
        Array toggleArray = Enum.GetValues(typeof(ToggleName));
        foreach (ToggleName oneToggle in toggleArray)
        {
            bool isSelected = selectedToggleIndex == (int)oneToggle;
            var newSelect = GUILayout.Toggle(isSelected, oneToggle.ToString());

            // 如果该单选框被选中，更新选中的索引
            if (!isSelected&&newSelect)
            {
                selectedToggleIndex = (int)oneToggle;
                //切换了toggle
                selectedButtonIndex = -1;
                idGroupScrollPosition = Vector2.zero;
                rightDetailScrollPosition = Vector2.zero;
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        string addNewTemp = selectedToggleIndex == (int)ToggleName.SkillTemplate ? "新建技能模板" : "新建buff模板";
        if (GUILayout.Button(addNewTemp))
        {
            OnClickAddNew();
        }
        string saveTemp = selectedToggleIndex == (int)ToggleName.SkillTemplate ? "保存当前技能模板" : "保存当前buff模板";
        if(GUILayout.Button(saveTemp))
        {
            OnClickSaveCurTemplate();
        }
        string saveAllTemp = selectedToggleIndex == (int)ToggleName.SkillTemplate ? "保存所有技能模板" : "保存所有buff模板";
        if (GUILayout.Button(saveAllTemp))
        {
            OnClickSaveAllTemplate();
        }
        GUILayout.EndHorizontal();

        if (selectedToggleIndex == (int)ToggleName.SkillTemplate)
        {
            DrawSkillTemplate();
        }
        else if (selectedToggleIndex == (int)ToggleName.BuffTemplate)
        {
            DrawBuffTemplate();
        }
        
    }
    private void OnClickAddNew()
    {
        if (selectedToggleIndex == (int)ToggleName.SkillTemplate)
        {
            if (SkillIDList.Count(skill=>skill.TempleteID==0)>=1)
            {
                EditorUtility.DisplayDialog("提示", "已经有一个新建技能模板0未保存了", "确认");
            }
            else
            {
                selectedButtonIndex = SkillIDList.Count - 1;
                CurShowSkill = SkillFactory.CreateDefaultSkill();
                SkillIDList.Add(CurShowSkill);
                SkillMap.Add(CurShowSkill,true);
                SkillSystemDrawerCenter.ClearDrawInstanceMap();
            }
        }
        else if(selectedToggleIndex == (int)ToggleName.BuffTemplate)
        {
            if (BuffIDList.Count(buff=>buff.TempleteID==0)>=1)
            {
                EditorUtility.DisplayDialog("提示", "已经有一个新建buff模板0未保存了", "确认");
            }
            else
            {
                
                selectedButtonIndex = BuffIDList.Count - 1;
                CurShowBuff = SkillFactory.CreateNewBuff();
                BuffIDList.Add(CurShowBuff);
                BuffMap.Add(CurShowBuff,true);
                SkillSystemDrawerCenter.ClearDrawInstanceMap();
            }
        }
    }

    private void OnClickSaveCurTemplate()
    {
        if (selectedToggleIndex == (int)ToggleName.SkillTemplate)
        {
            if (SaveSkillTemp(CurShowSkill))
            {
                //保存重写进txt，并且生成byte
                SaveSkillTemplateToTxt();
                DataTableGeneratorMenu.GenerateSkillTemplateDataTables();
            }
        }
        else if(selectedToggleIndex == (int)ToggleName.BuffTemplate)
        {
            if (SaveBuffTemp(CurShowBuff))
            {
                //保存重写进txt，并且生成byte
                SaveBuffTemplateToTxt();
                DataTableGeneratorMenu.GenerateSkillTemplateDataTables();
            }
        }
    }

    private void OnClickSaveAllTemplate()
    {
        bool needSave = false;
        if (selectedToggleIndex == (int)ToggleName.SkillTemplate)
        {
            var keys = SkillMap.ToList();
            foreach (var skillPair in keys)
            {
                if (skillPair.Value)
                {
                    needSave = true;
                    SaveSkillTemp(skillPair.Key);
                }
            }
            if (needSave)
            {
                //保存重写进txt，并且生成byte
                SaveSkillTemplateToTxt();
                DataTableGeneratorMenu.GenerateSkillTemplateDataTables();
            }
        }
        else if (selectedToggleIndex == (int)ToggleName.BuffTemplate)
        {
            var keys = BuffMap.ToList();
            foreach (var buffPair in keys)
            {
                if (buffPair.Value)
                {
                    needSave = true;
                    SaveBuffTemp(buffPair.Key);
                }
            }
            if (needSave)
            {
                //保存重写进txt，并且生成byte
                SaveBuffTemplateToTxt();
                DataTableGeneratorMenu.GenerateBuffTemplateDataTables();
            }
        }
    }
    #region 技能模板
    /// <summary>
    /// 读取所有skill二进制文件名并保存到skillIDList
    /// </summary>
    private void ReadAllSkillFile()
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(skillTemplatePath);
        FileInfo[] fis = directoryInfo.GetFiles("*.bytes", SearchOption.AllDirectories);
        SkillIDList.Clear();
        for (int i = 0; i < fis.Length; i++)
        {
            var SkillID = int.Parse(Path.GetFileNameWithoutExtension(fis[i].Name));
            var newEmptySkill = SkillFactory.CreateNewSkill();
            string filePath = skillTemplatePath + $"/{SkillID}.bytes"; // 文件路径，根据实际情况修改
            // 假设你需要使用的缓冲区大小是1024字节
            const int bufferSize = 1024;

            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                using (var bufferedStream = new BufferedStream(fileStream, bufferSize))
                {
                    using (var reader = new BinaryReader(bufferedStream))
                    {
                        newEmptySkill.ReadFromFile(reader);
                    }
                }
            }
            SkillIDList.Add(newEmptySkill);
            SkillMap.Add(newEmptySkill,false);
        }
        SkillIDList.Sort((a,b)=>a.TempleteID.CompareTo(b.TempleteID));
    }
    private void DrawSkillTemplate()
    {
        GUILayout.BeginHorizontal();

        // Left Area with Vertical Scroll View
        GUILayout.BeginVertical(GUILayout.Width(150), GUILayout.MinWidth(150)); // 设置最小宽度
        idGroupScrollPosition = GUILayout.BeginScrollView(idGroupScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

        for (int i = 0; i < SkillIDList.Count; i++)
        {
            if (selectedButtonIndex == i)
            {
                if (SkillMap[SkillIDList[i]])
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
                if (SkillMap[SkillIDList[i]])
                {
                    GUI.backgroundColor = Color.red; // 恢复默认背景色
                }
                else
                {
                    GUI.backgroundColor = Color.white; // 恢复默认背景色
                }
            }

            if (GUILayout.Button(SkillIDList[i].TempleteID.ToString(), GUILayout.MinWidth(90),GUILayout.MaxWidth(120))) // 设置按钮最小宽度
            {
                if (i != selectedButtonIndex)
                {
                    GUI.FocusControl(null);
                    SkillSystemDrawerCenter.ClearDrawInstanceMap();
                    CurShowSkill = SkillIDList[i];
                }
                selectedButtonIndex = i;
            }
        }

        GUI.backgroundColor = Color.white; // 恢复默认背景色
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Right Area
        GUILayout.BeginVertical();
        rightDetailScrollPosition = GUILayout.BeginScrollView(rightDetailScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
        if (selectedButtonIndex != -1)
        {
            GUILayout.Space(10);
            if (CurShowSkill != null)
            {
                EditorGUI.BeginChangeCheck();
                SkillSystemDrawerCenter.DrawOneInstance(CurShowSkill);
                if (EditorGUI.EndChangeCheck())
                {
                    SkillMap[CurShowSkill] = true;
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }
    private void DelSkillFile(string sKillName)
    {
        string filePath = skillTemplatePath + $"/{sKillName}.bytes"; // 文件路径，根据实际情况修改

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    private void SaveSkillFile(Skill skill)
    {
        string filePath = skillTemplatePath + $"/{skill.TempleteID}.bytes"; // 文件路径，根据实际情况修改
        using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            skill.WriteToFile(writer);
        }
    }

    private bool SaveSkillTemp(Skill skillTemplate)
    {
        if (skillTemplate == null)
        {
            return false;
        }
        if (skillTemplate.TempleteID == 0)
        {
            EditorUtility.DisplayDialog("提示", "模板ID不能保存为0", "确认");
        }
        else
        {
            var skillEditor = SkillSystemDrawerCenter.GetSkillEditor(skillTemplate);
            if (skillEditor != null)
            {
                if (skillEditor.OldTempleteID != skillTemplate.TempleteID && SkillIDList.Count(skill=> skill.TempleteID==skillTemplate.TempleteID)>1)
                {
                    EditorUtility.DisplayDialog("提示", $"模板ID{skillTemplate.TempleteID} 已存在", "确认");
                }
                else
                {
                    if (skillEditor.OldTempleteID != skillTemplate.TempleteID)
                    {
                        //删除旧byte文件
                        DelSkillFile(skillEditor.OldTempleteID.ToString());
                    }
                    SaveSkillFile(skillTemplate);
                    SkillMap[skillTemplate] = false;
                    return true;
                }
            }
        }
        return false;
    }
    private void SaveSkillTemplateToTxt()
    {
        StringBuilder tableBuild = new StringBuilder();
        tableBuild.AppendLine("#\t技能模板表\t");
        tableBuild.AppendLine("#\tId\tSkillTemplate");
        tableBuild.AppendLine("#\tint\tSkill");
        tableBuild.AppendLine("#\t模板ID\t技能模板");
        foreach (var skill in SkillIDList)
        {
            if (SkillMap.TryGetValue(skill, out var hasChange))
            {
                if (hasChange)
                {
                    //有修改还未保存使用 byte文件中的数据
                    string filePath = skillTemplatePath + $"/{skill}.bytes"; // 文件路径，根据实际情况修改
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            using (var fileStream = File.Open(filePath, FileMode.Open))
                            {
                                using (var reader = new BinaryReader(fileStream))
                                {
                                    var bytes = reader.ReadBytes((int)fileStream.Length);
                                    tableBuild.AppendLine($"\t{skill.TempleteID}\t{Convert.ToBase64String(bytes)}");
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
                    using (var newMemorySteam = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(newMemorySteam))
                        {
                            skill.WriteToFile(writer);
                        }
                        var array = newMemorySteam.ToArray();
                        tableBuild.AppendLine($"\t{skill.TempleteID}\t{Convert.ToBase64String(array)}");
                    }
                }
            }
        }
        using (FileStream fileStream = new FileStream(skillTemplateTableFilePath, FileMode.Create, FileAccess.Write))
        {
            using (StreamWriter stream = new StreamWriter(fileStream, Encoding.UTF8))
            {
                stream.Write(tableBuild.ToString());
            }
        }
    }

    #endregion
    #region buff模板
    /// <summary>
    /// 读取所有skill二进制文件名并保存到skillIDList
    /// </summary>
    private void ReadAllBuffFile()
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(buffTemplatePath);
        FileInfo[] fis = directoryInfo.GetFiles("*.bytes", SearchOption.AllDirectories);
        BuffIDList.Clear();
        for (int i = 0; i < fis.Length; i++)
        {
            var buffID = int.Parse(Path.GetFileNameWithoutExtension(fis[i].Name));
            
            var newEmptyBuff = SkillFactory.CreateNewBuff();
            string filePath = buffTemplatePath + $"/{buffID}.bytes"; // 文件路径，根据实际情况修改
            using (var writer = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                newEmptyBuff.ReadFromFile(writer);
            }
            BuffIDList.Add(newEmptyBuff);
            BuffMap.Add(newEmptyBuff,false);
        }
        BuffIDList.Sort((a,b)=>a.TempleteID.CompareTo(b.TempleteID));
    }
    private void DrawBuffTemplate()
    {
        GUILayout.BeginHorizontal();

        // Left Area with Vertical Scroll View
        GUILayout.BeginVertical(GUILayout.Width(150), GUILayout.MinWidth(150)); // 设置最小宽度
        idGroupScrollPosition = GUILayout.BeginScrollView(idGroupScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

        for (int i = 0; i < BuffIDList.Count; i++)
        {
            if (selectedButtonIndex == i)
            {
                if (BuffMap[BuffIDList[i]])
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
                if (BuffMap[BuffIDList[i]])
                {
                    GUI.backgroundColor = Color.red; // 恢复默认背景色
                }
                else
                {
                    GUI.backgroundColor = Color.white; // 恢复默认背景色
                }
            }

            if (GUILayout.Button(BuffIDList[i].TempleteID.ToString(), GUILayout.MinWidth(90),GUILayout.MaxWidth(120))) // 设置按钮最小宽度
            {
                if (i != selectedButtonIndex)
                {
                    GUI.FocusControl(null);
                    SkillSystemDrawerCenter.ClearDrawInstanceMap();
                    CurShowBuff = BuffIDList[i];
                }
                selectedButtonIndex = i;
            }
        }

        GUI.backgroundColor = Color.white; // 恢复默认背景色
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Right Area
        GUILayout.BeginVertical();
        rightDetailScrollPosition = GUILayout.BeginScrollView(rightDetailScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
        if (selectedButtonIndex != -1)
        {
            GUILayout.Space(10);
            if (CurShowBuff != null)
            {
                EditorGUI.BeginChangeCheck();
                SkillSystemDrawerCenter.DrawOneInstance(CurShowBuff);
                if (EditorGUI.EndChangeCheck())
                {
                    BuffMap[CurShowBuff] = true;
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }
    private bool SaveBuffTemp(Buff buff)
    {
        if (buff == null)
        {
            return false;
        }
        if (buff.TempleteID == 0)
        {
            EditorUtility.DisplayDialog("提示", "模板ID不能保存为0", "确认");
        }
        else
        {
            var buffEditor = SkillSystemDrawerCenter.GetBuffEditor(buff);
            if (buffEditor != null)
            {
                if (buffEditor.OldTempleteID != buff.TempleteID && BuffIDList.Count(existBuff=>existBuff.TempleteID== buff.TempleteID)>1)
                {
                    EditorUtility.DisplayDialog("提示", $"模板ID{buff.TempleteID} 已存在", "确认");
                }
                else
                {
                    if (buffEditor.OldTempleteID != buff.TempleteID)
                    {
                        //删除旧byte文件
                        DelBuffFile(buffEditor.OldTempleteID.ToString());
                    }
                    SaveBuffFile(buff);
                    BuffMap[buff] = false;
                    return true;
                }
            }
        }
        return false;
    }
    private void DelBuffFile(string buffName)
    {
        string filePath = buffTemplatePath + $"/{buffName}.bytes"; // 文件路径，根据实际情况修改

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    private void SaveBuffFile(Buff buff)
    {
        string filePath = buffTemplatePath + $"/{buff.TempleteID}.bytes"; // 文件路径，根据实际情况修改
        using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            buff.WriteToFile(writer);
        }
    }
    private void SaveBuffTemplateToTxt()
    {
        StringBuilder tableBuild = new StringBuilder();
        tableBuild.AppendLine("#\tbuff模板表\t");
        tableBuild.AppendLine("#\tId\tBuffTemplate");
        tableBuild.AppendLine("#\tint\tBuff");
        tableBuild.AppendLine("#\t模板ID\tbuff模板");
        foreach (var buff in BuffIDList)
        {
            if (BuffMap.TryGetValue(buff, out var buffTempPair))
            {
                if (buffTempPair)
                {
                    //有修改还未保存使用 byte文件中的数据
                    string filePath = buffTemplatePath + $"/{buff.TempleteID}.bytes"; // 文件路径，根据实际情况修改
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            using (var fileStream = File.Open(filePath, FileMode.Open))
                            {
                                using (var reader = new BinaryReader(fileStream))
                                {
                                    var bytes = reader.ReadBytes((int)fileStream.Length);
                                    tableBuild.AppendLine($"\t{buff.TempleteID}\t{Convert.ToBase64String(bytes)}");
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
                    using (var newMemorySteam = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(newMemorySteam))
                        {
                            buff.WriteToFile(writer);
                        }
                        var array = newMemorySteam.ToArray();
                        tableBuild.AppendLine($"\t{buff.TempleteID}\t{Convert.ToBase64String(array)}");
                    }
                }
            }
        }
        using (FileStream fileStream = new FileStream(buffTemplateTableFilePath, FileMode.Create, FileAccess.Write))
        {
            using (StreamWriter stream = new StreamWriter(fileStream, Encoding.UTF8))
            {
                stream.Write(tableBuild.ToString());
            }
        }
    }

    #endregion
    
}
