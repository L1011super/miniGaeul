# miniGaeul

基于 .NET 8、C# 和 WPF 的 Windows 透明桌面宠物。仓库只包含一个运行角色 `01_gaeul_kitsch`，不依赖动画 JSON、CSV 或多角色适配层。

## 功能

- 透明、置顶且不显示在任务栏中的桌宠窗口
- 单击随机播放互动动作，拖动不会误触发互动
- 拖动期间使用左右专属形态，松开后保持该形态至角色完全回正，再恢复正常动画
- 透明像素点击穿透，支持 Indexed8 PNG 的 Alpha 命中检测
- 五档互动频率（关闭、20 分钟、5 分钟、1 分钟、3 秒）、托盘控制、大小与位置设置
- 设置面板可即时播放 8 个互动动作、“向右走”和“向左走”；向右走默认播放两个 8 帧步行循环，向左走直接播放 8 帧步态，连续模式会保持到取消勾选
- 设置面板首次弹出时与角色保持 100px 水平间隔并限制在屏幕上下边界内；弹出后可自由移动，面板打开期间角色不可拖动
- 全屏应用检测、开机启动和设置持久化

## 项目结构

```text
miniGaeul/
├─ src/GaeulDesktopPet/
│  ├─ Assets/Icon/                      # PNG 源图标与 Windows ICO 图标
│  ├─ Assets/Sprites/01_gaeul_kitsch/  # 120 张动画帧与 2 张拖动形态
│  ├─ Interop/                         # Win32 窗口与屏幕接口
│  ├─ Models/                          # 动画和设置模型
│  ├─ Services/                        # 动画、缓存、托盘、全屏与设置服务
│  ├─ Views/                           # 设置窗口
│  ├─ App.xaml                         # 应用入口
│  └─ MainWindow.xaml                  # 桌宠透明窗口
├─ tests/GaeulDesktopPet.Tests/        # 动画资产与核心逻辑测试
├─ tools/publish.ps1                   # win-x64 自包含发布脚本
├─ installer/GaeulDesktopPet.iss       # Inno Setup 安装脚本
└─ GaeulDesktopPet.sln
```

动画定义集中在 `Services/AnimationCatalog.cs`。角色包含 1 个循环待机动作、8 个非循环互动动作和 2 个移动动作，共 122 张运行时 PNG；idle 按“静止—眨眼—静止”播放：首张静止帧在 0.3–1.5 秒间浮动，三张眨眼帧按 15 FPS 播放，末尾静止帧固定 2 秒。向右走直接按 8 张步态帧循环两次，以 10 FPS 播放并产生位移；向左走直接播放 8 帧步态并产生位移。两种移动均在屏幕工作区四边留出安全间隔。另有 `drag_left.png`、`drag_right.png` 两张静态拖动形态。

完整的 01–07 原始动画资产保存在仓库外的 `../animationFrameSource/`，不纳入本仓库版本控制。

## 开发

环境要求：Windows 10/11 与 .NET 8 SDK。

```powershell
dotnet restore
dotnet build
dotnet test tests/GaeulDesktopPet.Tests/GaeulDesktopPet.Tests.csproj
dotnet run --project src/GaeulDesktopPet/GaeulDesktopPet.csproj
```

VSCode 中也可直接运行 `restore`、`build`、`test`、`run` 和 `publish` 任务。

## 发布

```powershell
powershell -ExecutionPolicy Bypass -File tools/publish.ps1
```

发布结果生成在 `dist/GaeulDesktopPet-win-x64/`，同时生成对应 ZIP。脚本会验证发布包内恰好包含 122 张运行时 PNG。

程序设置和日志保存在 `%LOCALAPPDATA%\GaeulDesktopPet\`，不写入仓库目录。
