# Afterhours 仓库分析

参考：[Tokaku7/Afterhours](https://github.com/Tokaku7/Afterhours)。读取时间：2026-10-08。

本次读取了以下公开文件：

| 文件 | 已确认的实现 |
| --- | --- |
| [README.md](https://github.com/Tokaku7/Afterhours/blob/main/README.md) | 2.0.1 Windows 游戏时间看板；PowerShell/WPF；托盘；本地持久化 |
| [Live.ps1](https://github.com/Tokaku7/Afterhours/blob/main/Live.ps1) | 加载 XAML、窗口定位、拖动、菜单、主题和 DispatcherTimer 调度 |
| [Silver.xaml](https://github.com/Tokaku7/Afterhours/blob/main/Silver.xaml) | 无边框透明窗口、圆角 Border、渐变底色和自定义控件模板 |
| [DesktopLayer.cs](https://github.com/Tokaku7/Afterhours/blob/main/DesktopLayer.cs) | Win32 窗口层级、无焦点点击、圆角区域；现有注释和实现关闭主窗原生 blur，以规避部分 Windows 11 圆角问题 |

仓库提供的是桌面层游戏看板。所读源码中，窗口默认定位到工作区右上角；标题栏使用 DragMove；没有实现工作区边缘吸附、隐藏状态或隐藏边缘悬停唤出的状态机。因此自动收起不能直接从原项目继承，需要新增。

## 在 Keyside 中采用的思路

1. 保留轻量的 WPF 控件、原生窗口接口、独立托盘和本机 JSON。
2. 使用可编译的 C# 分层实现，避免需要 PowerShell 运行上下文的长脚本事件处理。
3. 增加独立边缘触发窗。收起时主窗真正隐藏，避免把窗口移到相邻屏幕、边界泄漏或看不见的窗口夺取输入。
4. 主面板作为边缘叠加窗置于普通应用上方；悬停展开不激活窗口。搜索和编辑弹窗因需要输入而主动获取焦点。没有直接沿用 Afterhours 的 Progman 桌面所有者绑定。
5. 磨砂玻璃采用 Windows 11 的官方 DWM Desktop Acrylic 接口；较旧系统使用兼容路径，失败回退实色。

本项目代码和图标独立编写，没有复制 Afterhours 的源文件、图片或图标。上游链接用于交互和技术分析。

0.2 更新：为加入可调透明度，主窗改用 WPF 分层透明绘制，磨砂采用 Acrylic 兼容接口并裁切原生圆角，不再在主窗调用 DWM 系统背景。新增 Glance 交互参考分析见 `Glance-reference.md`。

原生接口依据：

- [DWM_SYSTEMBACKDROP_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)：`DWMSBT_TRANSIENTWINDOW` 对应 Desktop Acrylic，最低 Windows 11 build 22621。
- [IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper)：读取各显示器壁纸路径与桌面底色。
- [DwmEnableBlurBehindWindow](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmenableblurbehindwindow)：Windows 8 起该旧接口不再产生原有模糊效果，因此没有把它用作本项目的玻璃实现。
