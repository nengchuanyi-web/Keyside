# 玻璃界面参考与实现

参考 [lulu-loopp/glance](https://github.com/lulu-loopp/glance)，读取版本为 `35a68f7404fba2cc386e59a049502e6a1a5a20a6`。其 [backdrop.rs](https://github.com/lulu-loopp/glance/blob/35a68f7404fba2cc386e59a049502e6a1a5a20a6/src/ui/backdrop.rs) 读取后方屏幕像素，[skins.rs](https://github.com/lulu-loopp/glance/blob/35a68f7404fba2cc386e59a049502e6a1a5a20a6/src/ui/skins.rs) 单独模糊背景并叠加着色，[window.rs](https://github.com/lulu-loopp/glance/blob/35a68f7404fba2cc386e59a049502e6a1a5a20a6/src/ui/window.rs) 保持窗口层不透明而使用内容自己的 alpha。文字独立于玻璃层绘制。

Keyside 的独立 C# / WPF 实现位于 `src/DesktopGlass.cs` 和 `src/ThemeService.cs`：捕获时临时排除自己的窗口，只把桌面背景图放入单独 Image 并施加模糊；SurfaceBrush 控制着色，标题、列表和 emoji 的 alpha 保持 255，Window.Opacity 始终为 1。每 600 毫秒更新背景，明暗切换使用现有亮度滞回。图片只保存在内存，不写入正式数据目录。

[Microsoft SetWindowDisplayAffinity 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) 说明 WDA_EXCLUDEFROMCAPTURE 从 Windows 10 2004 起支持。代码检查系统版本并保存 / 恢复捕获前的 affinity；较旧系统或捕获失败时使用原生 Acrylic 兼容效果。弹窗和菜单期间暂停取样，避免把本应用的编辑窗画入背景；弹窗本身使用实色底。

验证用专用蓝粉背景与 3 DIP 条纹，读取实际系统合成图；模糊保留两侧背景颜色，并将相邻像素变化由 51.91 降至 1.37。背景改亮后文字自动变深。截图见 `preview-v0.6.png`、`preview-v0.6-light.png`，完整 200 项记录为 `verification-v0.6.txt`。
