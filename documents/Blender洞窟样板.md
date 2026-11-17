# 黑牙巢洞窟样板

- Blender 源文件：`ArtSource/WorldTimeline/BlackfangCave.blend`。
- 生成脚本：`Tools/Blender/build_blackfang_cave.py`，Blender 4.1 后台执行；无需安装 Python 扩展。
- 游戏资产：`Client/Assets/GameMain/Art/WorldTimeline/BlackfangCave/`。
- 可复用预制体：`BlackfangCave.prefab`，Unity LODGroup，三个等级共用 CavePalette 材质和 64×64 色板。
- Blender 导出三角面：1267 / 607 / 268；Unity 导入会去除退化三角形，实际数量以导入日志为准。
- 已替换 `BirthRegion/Regions/Region_0_m9.prefab` 的黑牙巢地标，清理洞口内部穿插的装饰。模型位于节点后方 8 米，依据当前 Terrain 取地面高度。
- 地图区域继续使用既有区域显隐：近、中景一个模型 Renderer，远景一个低模 Renderer；独立预制体的 LODGroup 不叠加到区域版本上。

编辑器菜单：Tools → World Timeline → Install Blender Cave Landmark（Ctrl+Alt+F7）。必须退出 Play 后使用。此操作保存资产，按需附加打开 terrain 场景，不推进模拟或修改存档；不保存场景相机修改。

`CaveUnityBefore.png` 与 `CaveUnityAfter.png` 是相同透视参数的 Unity 场景预览，不含运行时节点、棋子和 UI。`ArtSource/WorldTimeline/BlackfangCave_Blender.png` 是 Blender 灯光下的模型展示，不能作为游戏截图。

本次完成地标模型与导入落地检查。周围旧树石材质、Terrain 地表纹理和整体光照仍待统一。本次没有做 Play 模式整场景 Draw Call 对比，单材质不意味着整个洞窟只有一次绘制，阴影和深度通道仍会增加提交。
