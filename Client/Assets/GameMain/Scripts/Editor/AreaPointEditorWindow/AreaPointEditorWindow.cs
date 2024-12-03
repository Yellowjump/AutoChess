using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cinemachine;
using DataTable;
using GameFramework;
using GameFramework.DataTable;
using GameFramework.Event;
using Maze;
using Unity.Mathematics;
using UnityEngine.Serialization;
using UnityGameFramework.Runtime;

namespace GameMain.Scripts.Editor.AreaPointEditorWindow
{
    [Serializable]
    public class AreaPointEditor
    {
        public int Index;
        public Vector3 Pos;
        public Vector3 CameraPosRelate;//相对pos的偏移
        public Vector3 CameraRotation;
        public AreaPointType CurPointType;
        public List<int> LinkPoint = new List<int>();
        public GameObject obj; // Reference to the associated GameObject
    }

    public class AreaPointEditorWindow : EditorWindow
    {
        public List<AreaPointEditor> areaPoints = new List<AreaPointEditor>();
        private GameObject parentObject;
        private CinemachineVirtualCamera _demoCamera;
        private Terrain terrain;
        private DataTableComponent dataTableComponent;
        private SerializedObject serializedObject;
        private Vector2 panelSize = new Vector2(500, 500);
        private Vector2 panelOffset = new Vector2(50, 0); // 右移50单位

        private bool _showAllAreaPointData = false;

        private int? draggedPointIndex = null; // 当前拖动的 AreaPoint 索引
        private Vector2 dragStartPos;
        private int selectAreaPointIndex;
        readonly string areaPointTableFilePath = Application.dataPath + @"/GameMain/Data/DataTables/AreaPoint.txt";
        private GUIStyle startPointStyle;
        private GUIStyle endPointStyle;
        private GUIStyle otherPointStyle;
        [MenuItem("Window/Area Point Editor")]
        public static void ShowWindow()
        {
            GetWindow<AreaPointEditorWindow>("Area Point Editor");
        }

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            // 订阅场景变化事件
            SceneView.duringSceneGui += OnSceneGUI;
            InitializeParentObject();
            InitializeDemoCamera();
            InitializeTerrain();
            InitLoadTableData();
        }

        private void OnDestroy()
        {
            var demoCameraObj = GameObject.Find("DemoCamera");
            if (demoCameraObj != null)
            {
                DestroyImmediate(demoCameraObj);
            }

            if (parentObject != null)
            {
                DestroyImmediate(parentObject);
                parentObject = null;
            }
        }

        private void InitDrawColoredStyle()
        {
            // 创建一个临时 GUIStyle
            startPointStyle = new GUIStyle(GUI.skin.button);

            // 设置背景颜色
            Texture2D bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.1f,0.5f,0.1f));
            bgTexture.Apply();
            startPointStyle.normal.background = bgTexture;
            
            endPointStyle = new GUIStyle(GUI.skin.button);
            Texture2D endBgTexture = new Texture2D(1, 1);
            endBgTexture.SetPixel(0, 0, new Color(0.5f,0.1f,0.1f));
            endBgTexture.Apply();
            endPointStyle.normal.background = endBgTexture;
            otherPointStyle = new GUIStyle(GUI.skin.button);
        }
        private void InitializeParentObject()
        {
            // Try to find an existing object named "TerrainEditorTempPa"
            parentObject = GameObject.Find("TerrainEditorTempPa");

            // If not found, create it and set to DontSave
            if (parentObject == null)
            {
                parentObject = new GameObject("TerrainEditorTempPa");
                parentObject.hideFlags = HideFlags.DontSave;
                Debug.Log("Created temporary parent object: TerrainEditorTempPa");
            }
            else
            {
                foreach (Transform child in parentObject.transform)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void InitializeDemoCamera()
        {
            var demoCameraObj = GameObject.Find("DemoCamera");
            if (demoCameraObj != null)
            {
                _demoCamera = demoCameraObj.GetOrAddComponent<CinemachineVirtualCamera>();
            }
            else
            {
                demoCameraObj = new GameObject("DemoCamera");
                demoCameraObj.hideFlags = HideFlags.DontSave;
                _demoCamera = demoCameraObj.AddComponent<CinemachineVirtualCamera>();
            }
            _demoCamera.Priority = 20;
        }
        private void InitializeTerrain()
        {
            // Find the first active Terrain in the scene
            terrain = FindObjectOfType<Terrain>();

            if (terrain == null)
            {
                Debug.LogWarning("No Terrain found in the scene. Please add a Terrain component.");
            }
        }

        private void InitLoadTableData()
        {
            DataTableEditor<DRAreaPoint> dataTable = new DataTableEditor<DRAreaPoint>("AreaPoint");
            dataTable.ParseByteFile($"Assets/GameMain/Data/DataTables/AreaPoint.bytes");
            Log.Info(dataTable[0].Position);
            foreach (var oneAreaData in dataTable.GetAllDataRows())
            {
                AddNewAreaPoint(oneAreaData);
            }
        }
        private void OnGUI()
        {
            if (startPointStyle == null)
            {
                InitDrawColoredStyle();
            }
            GUILayout.Label("Area Point List", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // Set the parent GameObject
            parentObject = (GameObject)EditorGUILayout.ObjectField("Parent GameObject", parentObject, typeof(GameObject), true);

            // Set the Terrain object
            terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
            _showAllAreaPointData = GUILayout.Toggle(_showAllAreaPointData, "显示所有点");
            if (_showAllAreaPointData)
            {
                for (int i = 0; i < areaPoints.Count; i++)
                {
                    DrawOneAreaPoint(areaPoints[i]);
                }
            }
            else
            {
                for (int i = 0; i < areaPoints.Count; i++)
                {
                    CheckPointPosChange(areaPoints[i]);
                }
            }
            // Display each AreaPointEditor with spacing in between


            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Get the terrain size and position
            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;

            // 绘制背景面板
            Rect controlRect = EditorGUILayout.GetControlRect(false, panelSize.y + panelOffset.y);
            Rect panelRect = new Rect(controlRect.x + panelOffset.x, controlRect.y + panelOffset.y, panelSize.x, panelSize.y);
            GUI.Box(panelRect, GUIContent.none, new GUIStyle { normal = { background = Texture2D.whiteTexture } });
            Event e = Event.current;
            // Draw each AreaPointEditor as a button inside the panel
            for (int i = 0; i < areaPoints.Count; i++)
            {
                // Map the x, z coordinates from the terrain's world space to the panel space
                float xPos = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, areaPoints[i].Pos.x) * panelSize.x;
                float zPos = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, areaPoints[i].Pos.z) * panelSize.y;
                xPos += panelRect.x;
                zPos = panelSize.y + panelRect.y - zPos;
                foreach (var linkIndex in areaPoints[i].LinkPoint)
                {
                    if (linkIndex > i && linkIndex < areaPoints.Count)
                    {
                        var target = areaPoints[linkIndex];
                        // Map the linked AreaPoint's position to the panel space
                        float targetX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, target.Pos.x) * panelSize.x;
                        float targetZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, target.Pos.z) * panelSize.y;

                        // Account for the panel's position offset
                        targetX += panelRect.x;
                        targetZ = panelSize.y + panelRect.y - targetZ;
                        Handles.color = Color.black;
                        // Draw line from the current point to the linked point
                        Handles.DrawAAPolyLine(2f, new Vector2(xPos, zPos), new Vector2(targetX, targetZ));
                        //Handles.DrawLine(new Vector2(xPos,  zPos), new Vector2(targetX,targetZ),20);
                    }
                }

                // Button size is 20x20
                Rect buttonRect = new Rect(xPos - 10, zPos - 10, 20, 20);
                // 检测鼠标点击和拖动事件
                if (buttonRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        draggedPointIndex = i;
                        dragStartPos = e.mousePosition;
                        Selection.activeGameObject = areaPoints[i].obj;
                        selectAreaPointIndex = i;
                        e.Use();
                    }
                }

                GUIStyle buttonStyle = otherPointStyle;
                switch (areaPoints[i].CurPointType)
                {
                    case AreaPointType.Start:
                        buttonStyle = startPointStyle;
                        break;
                    case AreaPointType.End:
                        buttonStyle = endPointStyle;
                        break;
                }
                if (GUI.Button(buttonRect, areaPoints[i].Index.ToString(),buttonStyle))
                {
                    // When the button is clicked, select the corresponding GameObject in the scene
                    Selection.activeGameObject = areaPoints[i].obj;
                    Debug.Log("Selected GameObject: " + areaPoints[i].obj.name);
                    selectAreaPointIndex = i;
                }

                if (draggedPointIndex == i && e.type == EventType.MouseDrag && e.button == 0)
                {
                    // 更新拖动位置
                    Vector2 delta = e.mousePosition - dragStartPos;
                    dragStartPos = e.mousePosition;

                    float newX = areaPoints[i].Pos.x + delta.x / panelSize.x * terrainSize.x;
                    float newZ = areaPoints[i].Pos.z - delta.y / panelSize.y * terrainSize.z;

                    areaPoints[i].Pos.x = Mathf.Clamp(newX, terrainPosition.x, terrainPosition.x + terrainSize.x);
                    areaPoints[i].Pos.z = Mathf.Clamp(newZ, terrainPosition.z, terrainPosition.z + terrainSize.z);

                    if (terrain != null)
                    {
                        areaPoints[i].Pos.y = terrain.SampleHeight(areaPoints[i].Pos) + terrainPosition.y;
                    }

                    areaPoints[i].obj.transform.position = areaPoints[i].Pos;
                    Repaint();
                }
            }

            // 结束拖动
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggedPointIndex = null;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add Area Point"))
            {
                AddNewAreaPoint();
            }

            if (GUILayout.Button("SaveData"))
            {
                SaveData();
            }

            if (areaPoints.Count > selectAreaPointIndex)
            {
                DrawOneAreaPoint(areaPoints[selectAreaPointIndex]);
            }
        }

        private void DrawOneAreaPoint(AreaPointEditor areaPoint)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Index:", GUILayout.Width(50));
            areaPoint.Index = EditorGUILayout.IntField(areaPoint.Index, GUILayout.Width(50));

            //EditorGUILayout.LabelField("Position:", GUILayout.Width(60));
            Vector3 newPos = EditorGUILayout.Vector3Field("Position:", areaPoint.Pos);
            areaPoint.CurPointType = (AreaPointType)EditorGUILayout.EnumPopup("指定点类型", areaPoint.CurPointType);
            var obj = areaPoint.obj;
            // Only allow changes to X and Z; Y will be updated based on terrain height
            if (newPos.x != areaPoint.Pos.x || newPos.z != areaPoint.Pos.z)
            {
                areaPoint.Pos.x = newPos.x;
                areaPoint.Pos.z = newPos.z;
                if (terrain != null)
                {
                    // Set the Y coordinate to the height of the terrain at this X,Z position
                    areaPoint.Pos.y = terrain.SampleHeight(newPos) + terrain.GetPosition().y;
                }

                obj.transform.position = areaPoint.Pos;
            }
            else
            {
                CheckPointPosChange(areaPoint);
            }

            serializedObject.Update();
            var listProp = serializedObject.FindProperty("areaPoints");
            SerializedProperty elementProp = listProp.GetArrayElementAtIndex(areaPoint.Index);
            if (elementProp != null)
            {
                var listP = elementProp.FindPropertyRelative("LinkPoint");
                EditorGUILayout.PropertyField(listP, true);
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (var linkPointIndex in areaPoint.LinkPoint)
                    {
                        var linkPoint = areaPoints[linkPointIndex];
                        if (!linkPoint.LinkPoint.Contains(areaPoint.Index))
                        {
                            linkPoint.LinkPoint.Add(areaPoint.Index);
                        }
                    }

                    foreach (var onePoint in areaPoints)
                    {
                        if (onePoint != areaPoint && onePoint.LinkPoint.Contains(areaPoint.Index) && !areaPoint.LinkPoint.Contains(onePoint.Index))
                        {
                            onePoint.LinkPoint.Remove(areaPoint.Index);
                        }
                    }
                }
            }
            areaPoint.CameraPosRelate = EditorGUILayout.Vector3Field("CameraPosOffset:", areaPoint.CameraPosRelate);
            areaPoint.CameraRotation = EditorGUILayout.Vector3Field("CameraRotate:",areaPoint.CameraRotation);
            if (GUILayout.Button("显示相机效果"))
            {
                _demoCamera.transform.position = areaPoint.CameraPosRelate+areaPoint.Pos;
                _demoCamera.transform.rotation = quaternion.Euler(areaPoint.CameraRotation);
            }
            if (GUILayout.Button("使用相机效果"))
            {
                areaPoint.CameraPosRelate = areaPoint.Pos - _demoCamera.transform.position;
                areaPoint.CameraRotation = _demoCamera.transform.rotation.eulerAngles;
            }
            // Display the associated GameObject (as read-only)
            EditorGUI.BeginDisabledGroup(true); // Disable editing
            areaPoint.obj = (GameObject)EditorGUILayout.ObjectField("Associated GameObject", areaPoint.obj, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup(); // Re-enable editing for the rest
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(10); // Adds space between AreaPoints for better readability
        }

        private void CheckPointPosChange(AreaPointEditor areaPoint)
        {
            var obj = areaPoint.obj;
            if (obj.transform.position != areaPoint.Pos)
            {
                areaPoint.Pos.x = obj.transform.position.x;
                areaPoint.Pos.z = obj.transform.position.z;
                if (terrain != null)
                {
                    // Set the Y coordinate to the height of the terrain at this X,Z position
                    areaPoint.Pos.y = terrain.SampleHeight(areaPoint.Pos) + terrain.GetPosition().y;
                }

                obj.transform.position = areaPoint.Pos;
            }
        }
        private void AddNewAreaPoint(DRAreaPoint oneAreaPointData = null)
        {
            if (parentObject == null)
            {
                Debug.LogWarning("Please set a parent GameObject first.");
                return;
            }

            // Create new AreaPointEditor with default values
            AreaPointEditor newPoint = new AreaPointEditor
            {
                Index = oneAreaPointData?.Id ?? areaPoints.Count,
                Pos = oneAreaPointData?.Position ?? Vector3.zero,
                CurPointType = oneAreaPointData!=null?(AreaPointType)oneAreaPointData?.AreaPointType:AreaPointType.Empty,
                CameraPosRelate = oneAreaPointData?.CameraPosRelate??Vector3.up*20,
                CameraRotation = oneAreaPointData?.CameraRotate ?? Vector3.zero,
            };
            if (oneAreaPointData != null)
            {
                newPoint.LinkPoint.AddRange(oneAreaPointData.LinkArea);
            }
            if (terrain != null)
            {
                // Set the Y coordinate to the height of the terrain at this X,Z position
                newPoint.Pos.y = terrain.SampleHeight(newPoint.Pos) + terrain.GetPosition().y;
            }

            // Add new AreaPointEditor to list
            areaPoints.Add(newPoint);

            // Create a new empty GameObject as a child of the specified parentObject
            GameObject newAreaObject = new GameObject("AreaPoint_" + newPoint.Index);
            newAreaObject.hideFlags = HideFlags.DontSave;
            newAreaObject.transform.SetParent(parentObject.transform);
            newAreaObject.transform.position = newPoint.Pos;
            newPoint.obj = newAreaObject;
        }

        private void OnDisable()
        {
            // 取消订阅场景变化事件
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // 只有在场景发生变化时刷新窗口
            Repaint();
        }

        private void SaveData()
        {
            StringBuilder tableBuild = new StringBuilder();
            tableBuild.AppendLine("#\t地图区域\t\t\t\t\t");
            tableBuild.AppendLine("#\tId\tPosition\tAreaPointType\tLinkArea\tCameraPosRelate\tCameraRotate");
            tableBuild.AppendLine("#\tint\tvector3\tint\tint[]\tvector3\tvector3");
            tableBuild.AppendLine("#\tIndex\t世界坐标\t类型\t连接的区域\t相机坐标\t相机旋转");
            foreach (var oneAreaPoint in areaPoints)
            {
                tableBuild.Append($"\t{oneAreaPoint.Index}\t{oneAreaPoint.Pos.x},{oneAreaPoint.Pos.y},{oneAreaPoint.Pos.z}\t{(int)oneAreaPoint.CurPointType}\t");
                for (var linkIndex = 0; linkIndex < oneAreaPoint.LinkPoint.Count; linkIndex++)
                {
                    var oneLinkP = oneAreaPoint.LinkPoint[linkIndex];
                    if (linkIndex == oneAreaPoint.LinkPoint.Count - 1)
                    {
                        tableBuild.Append(oneLinkP);
                    }
                    else
                    {
                        tableBuild.Append($"{oneLinkP},");
                    }
                }

                tableBuild.AppendLine($"\t{oneAreaPoint.CameraPosRelate.x},{oneAreaPoint.CameraPosRelate.y},{oneAreaPoint.CameraPosRelate.z}\t{oneAreaPoint.CameraRotation.x},{oneAreaPoint.CameraRotation.y},{oneAreaPoint.CameraRotation.z}");
            }

            using (FileStream fileStream = new FileStream(areaPointTableFilePath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter stream = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    stream.Write(tableBuild.ToString());
                }
            }

            DataTable.Editor.DataTableTools.DataTableGeneratorMenu.GenerateAreaDataTables();
        }
    }
}