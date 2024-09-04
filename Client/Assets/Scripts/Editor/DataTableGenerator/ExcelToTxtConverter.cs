using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.Text;

public class ExcelToTxtConverter : EditorWindow
{
    // 使用相对路径获取源目录和目标目录
    private string sourceDirectory = Path.Combine(Application.dataPath, "../../Config"); // 修改为你的相对源目录路径
    private string targetDirectory = Path.Combine(Application.dataPath, "Data/DataTables"); // 修改为你的相对目标目录路径

    private List<string> excelFiles = new List<string>();
    private List<bool> fileSelections = new List<bool>();

    [MenuItem("Tools/Excel To Txt Converter")]
    public static void ShowWindow()
    {
        GetWindow<ExcelToTxtConverter>("Excel To Txt Converter");
    }

    private void Awake()
    {
        LoadExcelFiles();
    }

    private void OnGUI()
    {
        GUILayout.Label("Excel To Txt Converter", EditorStyles.boldLabel);
        GUILayout.Label("Select Excel Files to Convert", EditorStyles.label);
        for (int i = 0; i < excelFiles.Count; i++)
        {
            fileSelections[i] = GUILayout.Toggle(fileSelections[i], Path.GetFileName(excelFiles[i]));
        }

        if (GUILayout.Button("Convert Selected Files"))
        {
            ConvertSelectedExcelToTxt();
            DataTable.Editor.DataTableTools.DataTableGeneratorMenu.GenerateDataTables();
        }

        if (GUILayout.Button("Convert All Files"))
        {
            ConvertAllExcelToTxt();
            DataTable.Editor.DataTableTools.DataTableGeneratorMenu.GenerateDataTables();
        }
    }

    private void LoadExcelFiles()
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Debug.LogError("Source directory does not exist!");
            return;
        }

        excelFiles.Clear();
        fileSelections.Clear();
        var files = Directory.GetFiles(sourceDirectory, "*.xlsx");
        foreach (var file in files)
        {
            // 检查文件名是否以 "~$" 开头，如果不是，则将文件添加到列表中
            if (!Path.GetFileName(file).StartsWith("~$"))
            {
                excelFiles.Add(file);
                fileSelections.Add(false); // 初始化勾选框状态为未选中
            }
        }
    }

    private void ConvertSelectedExcelToTxt()
    {
        if (string.IsNullOrEmpty(targetDirectory))
        {
            Debug.LogError("Target directory is empty!");
            return;
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        for (int i = 0; i < excelFiles.Count; i++)
        {
            if (fileSelections[i])
            {
                ConvertSingleFile(excelFiles[i], targetDirectory);
            }
        }

        Debug.Log("Conversion of selected files completed!");
    }

    private void ConvertAllExcelToTxt()
    {
        if (string.IsNullOrEmpty(targetDirectory))
        {
            Debug.LogError("Target directory is empty!");
            return;
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        foreach (var file in excelFiles)
        {
            ConvertSingleFile(file, targetDirectory);
        }

        Debug.Log("Conversion of all files completed!");
    }

    private void ConvertSingleFile(string filePath, string targetDirectory)
    {
        try
        {
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(file);
                ISheet sheet = workbook.GetSheetAt(0);

                // 获取所有行中最大的列数
                int maxCellNum = 0;
                for (int i = 0; i <= sheet.LastRowNum; i++)
                {
                    IRow row = sheet.GetRow(i);
                    if (row != null && row.LastCellNum > maxCellNum)
                    {
                        maxCellNum = row.LastCellNum;
                    }
                }

                string targetPath = Path.Combine(targetDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.txt");
                using (StreamWriter writer = new StreamWriter(targetPath, false, System.Text.Encoding.UTF8))
                {
                    for (int i = 0; i <= sheet.LastRowNum; i++)
                    {
                        IRow row = sheet.GetRow(i);
                        if (row != null)
                        {
                            for (int j = 0; j < maxCellNum; j++)
                            {
                                ICell cell = row.GetCell(j);
                                if (cell != null)
                                {
                                    writer.Write(cell.ToString());
                                }
                                if (j < maxCellNum - 1)
                                {
                                    writer.Write("\t");
                                }
                            }
                            writer.WriteLine();
                        }
                        else
                        {
                            // 如果这一行是空的，则写入空行
                            writer.WriteLine();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error while converting file: {ex.Message}");
        }
    }
}
