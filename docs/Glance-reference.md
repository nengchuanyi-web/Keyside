# Glance 参考与 0.2 改动

阅读时间：2026-10-08。[lulu-loopp/glance](https://github.com/lulu-loopp/glance) 的原生 Windows 面板使用边缘触发、鼠标移开收起和固定状态。此次只参考相关面板交互与视觉组织。

## 阅读的源码

- [src/panel.rs](https://github.com/lulu-loopp/glance/blob/main/src/panel.rs)：`track` 读取指针、按钮是否按下与固定状态；外部按下会开始关闭，面板内开始的拖动会延缓关闭。
- [src/detector.rs](https://github.com/lulu-loopp/glance/blob/main/src/detector.rs)：区分有意推压屏幕边缘与简单经过；绝对定位设备使用边缘停留。Keyside 保留现有悬停唤出方式。
- [src/ui/window.rs](https://github.com/lulu-loopp/glance/blob/main/src/ui/window.rs)：无边框、置顶、不激活的工具窗口。
- [src/ui/theme.rs](https://github.com/lulu-loopp/glance/blob/main/src/ui/theme.rs)、[src/ui/skins.rs](https://github.com/lulu-loopp/glance/blob/main/src/ui/skins.rs)：细描边、圆角、浅深主题和面板材质的组织。
- [src/settings.rs](https://github.com/lulu-loopp/glance/blob/main/src/settings.rs)：边缘选择与面板开启偏好。

## 应用于 Keyside

1. 外部按下立即收起；在面板内开始的拖动、缩放和编辑仍受保护，固定状态不收起。
2. 保留无边框窗口、轻量描边、磨砂和浅深主题；补充明确的“已吸附”状态和固定按钮高亮。
3. 用八个 WPF 边缘/角落拖拉控件调整尺寸，吸附边缘保持贴合；尺寸按 DIP 保存。
4. 设置中加入透明度实时滑杆，设置弹窗保持清晰；取消恢复原透明度。

以上为独立实现，没有移植 Glance 的 Rust / Direct2D / DirectComposition 代码，也没有加入系统监控模块。

WPF 透明绘制的实现依据：[HwndTarget.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndTarget.cs)。其窗口样式处理会在 UsesPerPixelOpacity 为 false 时移除 WS_EX_LAYERED，说明在不透明 WPF HWND 上强行设置整窗 alpha 不可靠。Keyside 使用 AllowsTransparency 与 Window.Opacity。
