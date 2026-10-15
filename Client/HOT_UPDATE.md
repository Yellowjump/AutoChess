# HybridCLR 与 GameFramework 热更新

项目采用一个热更程序集 `Assembly-CSharp.dll`。Player 仅内置首场景 `Assets/GameMain/Scenes/bootstrap.unity`；`Zizouqi.Bootstrap` 和启动所需的 `UnityGameFramework.Runtime` 框架程序集留在 AOT。`qipan` 棋盘场景及其 `GameFramework` 预制体放在热更场景包，`HeroComponent`、聊天、声音与调试业务组件留在热更 DLL 中。启动流程为 `ProcedureBootstrapInit → ProcedureCheckVersion → ProcedureUpdateResources → ProcedureLoadHotUpdate`，然后加载热更场景包并进入 `qipan`。

## 构建主包

切换到目标平台并使用 IL2CPP，打开 Unity 菜单 **HybridCLR > Hot Update Build Window**，检查 VPS、SSH 用户和密钥路径；可用 **选择文件夹** 指定 Player 输出目录，再点击 **构建全包并上传 VPS**。窗口先调用 **HybridCLR > Build Local One DLL Player** 对应的构建入口：生成本次主包的 `aot-compatibility.txt`、AOT 元数据、热更 DLL、`qipan` 场景 AB、GameFramework Full 与 Packed 资源，将 Packed 资源复制到 `StreamingAssets`，再执行 `BuildPipeline.BuildPlayer`。Player 成功后，窗口还会生成与它兼容的远端热更资源并上传。Windows Player 默认输出到 `Client/Builds/StandaloneWindows64/Zizouqi.exe`；窗口中更改目录后会输出到所选目录，窗口会显示完整 EXE 路径。每次主包构建都会产生新的 AOT 兼容 ID。

`StreamingAssets` 包含 Packed 资源、`GameFrameworkList.dat`、压缩的 `GameFrameworkVersion.dat`、`version.json` 和 AOT 元数据。启动器第一次离线运行时会将版本列表解压到可写目录，调用 GameFramework `CheckResources`，随后从资源包读取 DLL 和场景包，调用 `Assembly.Load`，再打开热更棋盘场景。

## 发布热更

先构建主包，再在 **HybridCLR > Hot Update Build Window** 点击 **构建热更包并上传 VPS**。它会重新编译 `Assembly-CSharp.dll`，重建 `qipan` 场景 AB 和 GameFramework Full 资源；若资源文件、应用版本或 AOT 兼容 ID 有变化，将完整文件及 `version.json` 复制到 `Client/Builds/HotUpdateServer/<BuildTarget>/`。本地内容相同时保留原发布目录与版本文件；本地比较使用资源文件的 SHA-256，忽略因构建编号变化而生成的版本列表。随后总会核对 VPS：远端内容与版本快照完整一致时跳过上传，缺失或不同的文件才补传。热更包不重建 Player，也不改动 `StreamingAssets`。原来的 **HybridCLR > Publish Local Hot Update Files** 菜单只在本地生成文件，不上传。仅修改热更代码或资源时无需重建 Player；修改 AOT 代码时必须重建 Player，并用新的兼容 ID 重新发布热更。

服务器原样提供该目录。上传资源文件后，最后上传 `version.json`。启动器请求 `<服务地址>/version.json`，检查应用版本及 AOT 兼容 ID，调用 GameFramework `CheckVersionList`、`UpdateVersionList`、`CheckResources`、`UpdateResources`，然后从 GameFramework 资源中读取 DLL。资源下载时，`bootstrap` 显示进度条及“百分比 已下载 / 总大小”，大小按压缩后的实际下载字节计算；无待下载资源时不显示。`hot-update-url.txt` 已设置为正式 HTTPS 地址；也可在启动参数中指定 `-hotUpdateUrl http://127.0.0.1:8000/StandaloneWindows64` 覆盖。

本地 HTTP 示例：在 `Client/Builds/HotUpdateServer` 目录运行 `python -m http.server 8000`。Unity Editor 从 `bootstrap` 场景 Play 时直接加载 `qipan`，业务脚本使用编辑器已编译的程序集，不执行远端版本检查或 DLL 加载。编辑器进入 Play 前临时启用 `terrain` 场景，供 GameFramework 编辑器资源模式加载；退出 Play 后恢复禁用，主包仍只构建 `bootstrap`。验证 IL2CPP 热更新时，用构建后的 Player 查看日志中的 `GameFramework resources ready` 和 `Loaded Assembly-CSharp from GameFramework resource`。

## 远端静态服务器与自动上传

发布目录包含供人工查看的 `manifest.json`，列出当前 GameFramework 清单里的资源名、服务器文件名、文件大小与哈希。客户端仍使用二进制 `GameFrameworkVersion.*.dat`。如果热更内容未变，但当前发布目录尚无可读清单，再次发布会补齐并上传它，资源版本号不变。也可在 Unity 菜单 **HybridCLR > Export Readable Hot Update Manifest** 从当前本地发布文件直接导出，无需重新构建；随后用上传脚本的 `UploadOnly` 模式传到服务器。上传后可在 `https://lostnoob.xyz/StandaloneWindows64/manifest.json` 查看当前版本。

上传时还会建立 `StandaloneWindows64/releases/<资源版本号>/`。每个版本文件夹包含该版的 `version.json`、`manifest.json` 与全部资源文件；资源文件用 Linux 硬链接指向根目录中的相同内容，不重复占用磁盘数据。客户端继续读取 `StandaloneWindows64/` 根目录，发布仍在最后替换根目录的 `version.json`。首次采用这个目录结构时，仅从当前发布版本开始建立快照，不会凭旧的零散文件推测旧版本内容。
已有版本可在 **HybridCLR > Hot Update Build Window** 点击 **同步当前版本目录到 VPS**，直接从本地发布目录生成可读清单并上传，建立当前版本文件夹；不会重新构建 DLL、AB 或 Player，也不会增加资源版本号。

清理脚本 `Tools/Clean-OldHotUpdateFiles.ps1` 默认只预览，并保留最新 1 个版本文件夹。执行 `-Apply` 后会删掉其他版本文件夹，再删掉根目录中所有保留版本都不用的旧资源文件。可用 `-KeepVersions 2` 保留最新两个，或用 `-RemoveVersions 45` 明确选择要删的版本。清理前会核对本地和服务器的当前 `version.json`；当前版本始终不能删除。清理旧版本可能使正在使用旧清单下载的客户端遇到 404，应在合适的发布时间操作。
构建窗口也有“要删除的版本文件夹”输入框；填写 `releases/` 下的数字版本号，先点 **预览清理**，再点 **删除此版本及无用文件**。执行记录写入 `Client/Builds/cleanup-hot-update.log`。

当前 VPS 是 Debian，地址 `64.176.56.225`，SSH 用户为 `root`。已在 `/etc/caddy/sites/zizouqi-ip.conf` 和 `/etc/caddy/sites/zizouqi-domain.conf` 增加热更新站点，配置副本分别为 `Client/Tools/Caddy-zizouqi-ip.conf`、`Client/Tools/Caddy-zizouqi-domain.conf`，原有站点保留。服务器只需提供静态文件，不需要运行 Unity、HybridCLR 或游戏后端。远端文件目录为：

```bash
mkdir -p /srv/zizouqi-hotupdate/StandaloneWindows64
chown -R root:root /srv/zizouqi-hotupdate
chmod -R u=rwX,g=rX,o=rX /srv/zizouqi-hotupdate
```

正式地址是 `https://lostnoob.xyz/StandaloneWindows64/version.json`，已验证返回 200 且 `version.json` 的响应设置了 `Cache-Control: no-store`。Player 的 `BaseUrl` 是 `https://lostnoob.xyz/StandaloneWindows64`。Caddy 的 `file_server` 会把请求路径接到站点根目录后面；添加路由时不要剥除 `StandaloneWindows64` 前缀。此服务器的 Caddy 设置了 `admin off`，修改站点配置后用 `caddy validate --config /etc/caddy/Caddyfile` 检查，再用 `systemctl restart caddy` 生效；`systemctl reload caddy` 无法使用。

本机已创建专用密钥 `C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519`，公钥已安装到 VPS。若以后需要重新安装公钥，可在自己的 PowerShell 运行下面的命令，在 SSH 提示时核对主机指纹并输入 VPS 密码。它只把公钥追加到服务器的 `authorized_keys`，私钥始终留在本机：

```powershell
Get-Content -Raw 'C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519.pub' |
  ssh root@64.176.56.225 'umask 077; mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys'
```

确认 `ssh -i 'C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519' root@64.176.56.225` 无需 VPS 密码即可登录。完成 Caddy 路由配置后，可先上传当前发布目录，验证 IP 地址：

```powershell
cd D:\UnityProjects\zizouqi\Client
.\Tools\Publish-HotUpdate.ps1 -Mode UploadOnly `
  -Server 64.176.56.225 -User root `
  -KeyPath 'C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519'
```

第一次构建内置远端地址的 Player：

```powershell
cd D:\UnityProjects\zizouqi\Client
.\Tools\Publish-HotUpdate.ps1 -Mode Player `
  -Server 64.176.56.225 -User root `
  -KeyPath 'C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519' `
  -BaseUrl https://lostnoob.xyz/StandaloneWindows64
```

之后仅修改热更 DLL、`qipan` 场景或资源时，不必重建 Player：

```powershell
.\Tools\Publish-HotUpdate.ps1 -Mode HotUpdate `
  -Server 64.176.56.225 -User root `
  -KeyPath 'C:\Users\香蕉君\.ssh\zizouqi_deploy_ed25519'
```

窗口按钮在当前 Unity 编辑器中完成构建，再调用同一脚本的 `UploadOnly` 模式；窗口等待上传结果，输出记录在 `Client/Builds/upload-hot-update.log`。命令行脚本的 `Player` 与 `HotUpdate` 模式会调用 Unity 的批处理构建入口，上传 `Builds/HotUpdateServer/StandaloneWindows64`。上传脚本通过 SSH 对 VPS 当前文件计算 SHA-256，与本地逐一比较；远端完全一致且版本文件夹完整时不上传。服务器版本号高于本地，或相同资源版本号对应不同 `version.json` 时会拒绝覆盖。所有新资源上传完成后，脚本才原子替换根目录的 `version.json`。如果已通过 Unity 菜单完成发布，可用 `-Mode UploadOnly` 单独同步当前目录。`-RemoteRoot` 默认为 `/srv/zizouqi-hotupdate`，SSH 不是 22 端口时加 `-Port`。使用命令行构建模式时，先保存并关闭正在打开本项目的 Unity 编辑器。

先访问版本 URL，确认得到本次 `version.json`，再用 Player 日志确认 `Selected GameFramework resource version` 和 `GameFramework resources ready`。旧 Player 还未内置远端地址时，可以临时用 `-hotUpdateUrl https://lostnoob.xyz/StandaloneWindows64` 启动验证；正式发布的 Player 应通过上述 `-Mode Player -BaseUrl ...` 构建。

`version.json` 目前没有数字签名。公开部署时应使用 HTTPS，并为版本信息增加签名校验。
## Unity 2023.2.2f1 的本地 GC 兼容修正

当前 HybridCLR 8.12.0 的 `BoehmGC.cpp` 使用了 Unity 2023.2.2f1 自带 BDWGC 头文件没有提供的接口。已在 `HybridCLRData/LocalIl2CppData-WindowsEditor/il2cpp/libil2cpp/gc/BoehmGC.cpp` 中将相关调用改为此 Unity 版本提供的接口；修正后的 Windows x64 IL2CPP Player 于 2026-10-12 构建成功。该目录是 HybridCLR Installer 生成的本地副本，重新执行 Installer 会覆盖此修改。覆盖后可在项目 `Client` 目录执行：

```powershell
git -C HybridCLRData/LocalIl2CppData-WindowsEditor/il2cpp apply (Resolve-Path HybridCLRCompat/Unity2023.2.2f1-BoehmGC.patch)
```

补丁仅适用于 Unity 2023.2.2f1 和当前 HybridCLR 8.12.0；升级任一版本后应先用官方兼容版本重新安装并构建，不要直接套用旧补丁。

