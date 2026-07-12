# 便利贴 (StickyNotes)

Windows 桌面便利贴应用，基于 .NET 8 + WPF 开发。（本项目使用Claude Code开发）

## 功能

- **三种笔记类型**：文字、图片、文件引用
- **6 种颜色主题**：经典黄、樱花粉、薄荷绿、天空蓝、暖橙、薰衣草紫
- **系统托盘**：最小化到托盘，后台运行
- **悬浮小球模式**：收起为浮动绿色圆球，显示笔记数量
- **剪贴板监听**：自动检测剪贴板中的文字/图片/文件并创建笔记
- **边缘吸附**：拖拽窗口靠近屏幕边缘自动吸附，悬停展开
- **拖拽排序**：支持拖拽重排笔记顺序
- **多选模式**：批量删除、导出笔记
- **开机自启**：通过注册表管理
- **自动清理**：7 天以上的旧笔记自动清理

## 技术栈

| 层级 | 技术 |
|---|---|
| 框架 | .NET 8.0 (Windows), WPF |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf 2.0.1 |
| 数据库 | SQLite（Microsoft.Data.Sqlite 10.0.9） |

## 构建

```bash
# 安装 .NET 8 SDK 后
dotnet build

# 发布单文件
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## 下载

从 [Releases](../../releases) 页面下载最新的 `StickyNotes.exe`（需安装 [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)）。

## 数据存储

- 笔记数据：`%APPDATA%\StickyNotes\StickyNotes.db`（SQLite）
- 设置：`%APPDATA%\StickyNotes\settings.json`

## 许可

MIT
