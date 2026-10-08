# Keyside 0.6 · Windows 快捷键侧边面板

- 已生成可双击运行的 `dist/Keyside.exe`；Windows 10/11 x64，.NET Framework 4.8，无网络依赖。
- 四边吸附、边缘悬停展开、自动收起、软件标签页、快捷键 / 功能 / 备注三列、四种主题和中英界面。
- 独立 C# / WPF 实现；Afterhours 仓库分析和架构说明见 `docs/`。

## 0.6 更新

三列“快捷键组合 / 功能 / 备注”现在都能拖动表头分界调整宽度，包括备注列右边界。松开后保存宽度比例，切换标签、语言和重启会恢复；改变面板宽度时按比例调整。收起备注会让其他两列占满空间，再展开恢复备注宽度。固定时锁定列宽调整，取消固定即可继续拖调。

透明度现在只影响背景，标题、快捷键、功能文字和 emoji 保持不透明。设置 → 外观选择“磨砂玻璃”，再调整“面板透明度”，即可获得透出背景颜色的玻璃效果；浅色、深色和壁纸主题也只调背景透明度。原有透明度数值自动沿用。

玻璃参考 [Glance 的背景分层实现](https://github.com/lulu-loopp/glance/blob/35a68f7404fba2cc386e59a049502e6a1a5a20a6/src/ui/skins.rs)：背景单独模糊，文字和控件单独绘制。展开时每 600 毫秒刷新背景取样，根据背景亮度自动使用明亮或深色文字。取样只在内存中用于显示，不写图片文件；弹窗和菜单打开时暂停更新，编辑窗保持实色清晰。较旧 Windows 或取样不可用时使用原生玻璃兼容外观。技术细节见 `docs/GLANCE-GLASS.md`。

## 0.5 更新

标题与副标题的间距由 1 DIP 增加到 6 DIP，与 KEYSIDE 到标题的 4 DIP 间距区分；小红书图标从 20 DIP 缩小到 16 DIP，仍随整体字号缩放。

三种主色继续使用淡蓝、浅粉、浅绿。大面板以接近 0.2 的浅灰底混入少量所选主色，文字恢复深灰，搜索框为白色；选中标签、按钮和快捷键使用柔和的主色点缀。深色、磨砂和自动主题保留。

点击图钉固定后，面板保持展开，并锁定快捷键、备注、置顶、标签增删改、导入、设置、标题拖动和边框缩放。搜索、切换标签、展开 / 收起备注、备份和下载模板仍可使用。底部显示“● 已固定 · 保持展开 · 编辑锁定”；再次点击图钉恢复编辑。固定状态会保存，重启后同样锁定。

右下角 `···` → “导入快捷键 TXT”下方新增“下载模板 TXT 文件…”。选择保存位置即可获得可直接导入的 UTF-8 `Template.txt`，固定时也能下载。

## 0.4 更新

副标题显示上传的“小红书.png”图标，旁边是“食得咸鱼抵得渴”。图标已内置，运行时不依赖下载目录。

设置中的面板配色简化为三项：淡蓝 `#E0EAFE`、浅粉 `#FEE0EA`、浅绿 `#EAFEE0`，与提供的三色图一致。点击实时预览，保存后记忆，取消恢复原配色；色环、自由色板和 HEX 输入已移除。各主题围绕所选主色调整底色与控件对比度；0.5 将大面积底色进一步调浅。旧版自选色自动迁移到 RGB 最接近的一项。

设置 → 整体字号：80%–150%，同步调整标题、搜索、标签、列表、按钮、菜单、备注和 emoji；实时预览，取消恢复，保存后记忆。面板保留至少一整行快捷键所需的最小高度；大字号时最低高度会适当增加，150% 时为 532 DIP（受屏幕工作区限制）。

列表底部“收起备注 / 展开备注”控制整列显示。收起后快捷键和功能两列自动加宽，备注文字仍保留，右键仍可编辑；切换标签页、重启后记忆选择。设置里也可勾选“显示备注列”。

## 快捷键与备注

右键快捷键可置顶 / 取消置顶，置顶项在各软件标签页内优先显示，组合前有 `↑` 标记；多个置顶项保留原始顺序。

新增“备注”列，双击空格或已有内容添加 / 修改，支持多行文字、搜索和完整内容悬停预览。支持 `Win + .` 或粘贴输入 emoji。面板、悬停和编辑窗的显示预览使用内置 Twemoji 17.0.3 彩色 PNG，离线支持 Unicode Emoji 17.0 的全部 3,944 个完整表情和 5,225 种资格 / 显示序列，包括 ZWJ 组合、肤色、旗帜与键帽。文本编辑框的原始字形仍由 Windows 字体绘制，最终显示以其下方彩色预览为准。后续 Unicode 版本的表情需要更新图库；普通文字保持系统字体。

右下角 `···` → 导入快捷键 TXT：每行 `快捷键@功能`，与所提供 Template.txt 一致。先预览，再选择新建命名的软件标签页或追加到任一已有标签页。相同的快捷键和功能组合跳过并显示数量，保留已有备注与置顶；出错报告行号，不导入半份文件。详见下方格式说明。

旧版数据自动补齐空备注和默认配色，保留全部标签页与设置。先从托盘退出旧程序，再运行新版 EXE；正式数据仍在原目录，无需重新导入。

## 0.2 更新

修复面板拖过左右边缘后被误判为自由悬浮的问题。吸附识别扩大到 64 DIP，越过屏幕边界也算接触；已吸附时点击外部立即收起，鼠标移开仍按 650 毫秒延迟收起。固定模式继续保持展开。

新增四边、四角拖拉缩放和面板透明度滑杆。尺寸、透明度会保存，旧版数据自动补齐新设置并保留全部标签页和快捷键。使用新版本前先退出旧程序；数据目录保持不变。

## 启动

便携包完整解压后双击 `Keyside.exe`。源码工程构建后双击 `dist/Keyside.exe`，或双击根目录 `Start.cmd`。

第一次启动默认在主屏右边缘显示面板，约 1.65 秒后在鼠标位于面板外时收起。鼠标移到该位置的细边缘条，停留 120 毫秒展开；离开面板 650 毫秒后收起。展开后的 1 秒宽限期避免闪烁。

这是查阅和维护快捷键的面板。点击列表不会向其他软件发送按键。

## 操作

| 操作 | 方法 |
| --- | --- |
| 创建软件标签页 | 点击标签栏右侧 `+`，填写名称 |
| 重命名、删除标签页 | 右键该标签页 |
| 添加快捷键 | 点击底部“添加快捷键”，填写组合和功能 |
| 编辑、删除快捷键 | 双击组合或功能格编辑；右键一行管理 |
| 置顶快捷键 | 右键某一行 → 置顶；再次右键 → 取消置顶 |
| 添加 / 修改备注 | 双击“备注”列中的空格或文本；空文本可清除备注 |
| 调整三列宽度 | 拖动表头分界或备注列右边界；松开后记忆，固定时锁定 |
| 选择面板配色 | 设置 → 面板颜色 → 淡蓝 / 浅粉 / 浅绿 → 保存 |
| 整体字号 | 设置 → 整体字号 → 拖动滑杆 → 保存 |
| 收起 / 展开备注列 | 点击列表底部的“收起备注 / 展开备注”；设置里也可切换 |
| TXT 批量导入 | 右下角 `···` → 导入快捷键 TXT → 预览并选择标签页 |
| 下载 TXT 模板 | 右下角 `···` → 下载模板 TXT 文件 → 选择保存位置 |
| 快捷键录入 | 可直接输入多步组合，或在编辑窗点击“按下组合键录入”；系统保留组合请手动填写 |
| 查找 | 按组合键、功能或备注搜索；Esc 可退出搜索并收起 |
| 拖动吸附 | 拖动标题栏靠近任一屏幕工作区边缘；距离不超过 64 DIP 或已经越过边界时自动吸附，否则自由悬浮 |
| 调整面板尺寸 | 鼠标靠近面板四边或四角，出现缩放指针后拖拉；已吸附的边缘保持贴边 |
| 调整背景透明度 | 设置 → 面板透明度，范围 0%–65%；只改变背景，文字不变淡；磨砂玻璃始终保留玻璃效果 |
| 指定吸附边缘 | 设置 → 吸附位置 → 左 / 右 / 上 / 下 / 自由悬浮 |
| 固定与编辑锁定 | 点击右上角图钉保持展开并锁定编辑；搜索仍可用；再次点击恢复编辑 |
| 更换主题和语言 | 设置 → 外观 / 界面语言 → 保存 |
| 备份与退出 | 右下角 `···`；也可以使用系统托盘 |

右上角向下箭头立即收起，并取消固定。自由悬浮模式保持可见；吸附成功后底部明确显示“已吸附左侧 / 右侧 / 顶部 / 底部”。已吸附时点击面板外立即收起。

编辑弹窗、菜单、标题拖动、缩放，以及搜索框刚发生输入的 1.5 秒内会暂缓自动收起。搜索框单纯保留焦点不会无限阻止收起；点击外部也会解除搜索保护。普通应用上方可以唤出；前台应用占满整块屏幕时停止边缘唤出，托盘仍可主动展开。

面板默认 440×660 DIP，可调范围 360–1400 DIP 宽、最高 1600 DIP 高；正常字号最低高度 450 DIP，大字号最低高度相应增加，上限同时受所在屏幕工作区限制。透明度只改变背景，文字与 emoji 保持清晰；设置和编辑弹窗保持不透明。

内置 Photoshop 和 VS Code 的示例快捷键可自行编辑；具体软件版本或自定义键位可能不同。

## 主题

- **磨砂玻璃**：0.6 在独立背景层显示模糊的桌面取样，叠加所选主色的轻微着色；文字根据背景亮度使用浅色或深色，始终保持不透明。背景刷新间隔 600 毫秒。取样不可用时回退原生 Acrylic；原生接口也不可用时保留可调的透明底。
- **浅色 / 深色**：固定实色主题。
- **跟随壁纸亮度**：每 30 秒读取面板所在屏幕的静态壁纸并取 64×64 缩略图平均相对亮度；低于 0.36 用深色，高于 0.44 用浅色，中间保持原主题。未显示壁纸图片时采样桌面底色，读取失败时跟随系统主题。

玻璃与壁纸自动主题是两个独立选项。动态壁纸软件不暴露给 Windows 的视频帧不在 MVP 采样范围内。

## 数据

### TXT 快捷键格式

随包提供 `Template.txt`（源码包位于 `examples/`），也可通过 `···` → 下载模板 TXT 文件保存：

```text
Ctrl + N@新建文档 / New document
Ctrl + O@打开文件 / Open file
Ctrl + S@保存 / Save
```

每个 TXT 表示一组软件快捷键，软件名称可在预览窗修改。第一处 `@` 分隔快捷键与功能；后续 `@` 保留在功能里，例如邮箱。空行忽略，两侧空白去除；不支持嵌套分组标记。TXT 不包含备注和置顶，新导入条目的备注为空；要完整迁移这些内容和配色请使用 JSON 备份。

推荐 UTF-8；也支持带 BOM 的 UTF-8 / UTF-16 和 GB18030（包含常见 GBK 中文）。最大 5 MB、每组 10,000 条，每侧文字不能为空。导入不会替换现有标签页。

默认保存在 `%LOCALAPPDATA%\Keyside\`：

- `state.json`：标签页、快捷键、备注、置顶、活动标签和设置；修改后原子保存。
- `state.json.bak`：上一次有效保存；导入备份时保留被替换的数据。
- `state.json.corrupt-*`：发现损坏时保留的原始文件。
- `diagnostics.log` / `startup-error.log`：仅在发生问题或导出提示时写入。

程序不联网、不采集其他应用的按键、不自动安装开机启动项、不修改系统快捷键。玻璃模式只在内存中取样面板后方画面以绘制背景；未启用玻璃时停止取样。启动单实例；数据仅存本机。卸载时先从托盘退出，再删除解压目录；需要清除个人数据时另行删除上述数据目录。

自定义数据目录：

```powershell
.\dist\Keyside.exe --data-dir "D:\MyShortcuts"
```

## 构建与验证

无需安装 .NET SDK、Python、Node 或 NuGet 包。双击 `Build.cmd`，或在 Windows PowerShell 运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Verify
```

构建使用 Windows 自带的 .NET Framework C# 编译器和已安装 WPF 程序集，输出 `dist/`。若系统没有 .NET Framework 4.8，需要先安装该 Windows 运行组件。

验证使用独立的 `qa/data`，不会写入正式数据。0.6 共 200 项通过，记录见 `docs/verification-v0.6.txt`。新增实际 WPF 三列表头拖动 / 保存 / 备注收起恢复 / 固定锁定，四种主题下的背景 alpha 与标题、快捷键及 emoji 不透明检查，专用测试背景上的实际系统合成、模糊与颜色保留、背景刷新 / 文字对比自动切换和实色编辑弹窗；同时保留全部旧功能回归。`docs/preview-v0.6.png` 为专用彩色测试背景上的实际系统合成截图；其余控件截图以 WPF 渲染为主。报告不代表已完成多台电脑的兼容性测试。

也可用 Visual Studio 的“.NET 桌面开发”工作负载和 4.8 Developer Pack 打开 `ShortcutDock.csproj`。当前机器采用 `build.ps1` 路径完成编译，未验证 Visual Studio 项目构建路径。

## English quick start

Unzip the portable package, then double-click `Keyside.exe` (source build: `dist/Keyside.exe`). The app docks to the right of the primary monitor. Hover the thin edge strip to reveal it; leave to hide. Drag its title near any work-area edge to dock, or choose an edge in Settings.

Use `+` to create software tabs. Right-click a tab to rename or delete it. The three columns are shortcut combination, action, and notes. Double-click a notes cell to edit, including empty cells. Right-click a shortcut to pin it to the top. Offline color emoji rendering supports all Unicode Emoji 17.0 fully-qualified sequences; the notes editor provides a color preview. Entries are reference text and do not trigger keyboard input in other apps.

Drag a header divider, including the notes column's right boundary, to resize any column. Proportions persist across tabs and restarts and adapt to the panel width. Hiding notes expands the remaining columns; showing it restores its width. Column resizing is locked while the panel is pinned.

Settings → Panel color offers exactly three palettes: soft blue (#E0EAFE), soft pink (#FEE0EA), and soft green (#EAFEE0). Version 0.5 uses a lightly tinted neutral panel, dark gray text and soft accent buttons, closer to version 0.2. Dark / glass / wallpaper modes adapt the selected palette. Save persists changes and Cancel restores them. The color wheel and HEX input have been removed.

Settings → Text size scales panel text and emoji from 80% to 150%. The minimum panel height grows when necessary to keep a complete shortcut row visible. Use the bottom Hide notes / Show notes button to collapse or expand the entire notes column without deleting its contents. Settings also has a Show notes column checkbox. These preferences persist across tabs and restarts. The supplied Xiaohongshu image is embedded beside the subtitle.

Use `···` → Import shortcuts TXT for bulk import. The next menu item, Save TXT template, saves a ready-to-import UTF-8 example. Each line is `shortcut@action`. UTF-8 is recommended; UTF-16 with BOM and GB18030 are also supported. Preview before creating a named tab or appending to an existing one. Identical shortcut/action pairs are skipped without changing existing notes or pins. JSON backups preserve the complete state.

Settings includes frosted glass, light, dark and wallpaper-brightness themes, plus Chinese and English UI. Drag any border or corner to resize. The transparency slider now affects the background only, from 0–65%; text and emoji remain opaque. Save remembers it and Cancel restores it. Frosted glass draws a separately blurred backdrop, refreshed every 600 ms, and adapts text contrast to background brightness. Pictures stay in memory; editors stay opaque. Click outside a docked, unpinned panel to hide immediately. Pin keeps it open and locks editing, imports, settings, movement and resizing; search, tab navigation, notes visibility, backups and template download remain available. Unpin restores editing. The system tray can show the panel or exit. Backups are available from `···`; data lives in `%LOCALAPPDATA%\Keyside`.

Requires Windows 10/11 x64 and .NET Framework 4.8. No SDK is needed to run the included executable. Captured glass requires Windows 10 version 2004 or newer; older systems use the native acrylic compatibility path, subject to OS support and transparency settings. This is an unsigned MVP; Windows 10 and physical mixed-DPI monitors still need manual compatibility testing.

## Third-party attribution

Emoji artwork: [Twemoji 17.0.3](https://github.com/jdecked/twemoji/tree/v17.0.3), © Twitter, Inc. and other contributors, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). PNG artwork is unchanged. Unicode test data is distributed under Unicode License v3. Full attribution and license texts are included in `THIRD-PARTY-NOTICES.md` and `licenses/` (source package: `assets/`).
