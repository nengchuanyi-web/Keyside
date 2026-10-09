# Keyside 0.9 · Windows 快捷键侧边面板

- 已生成可双击运行的 `dist/Keyside.exe`；Windows 10/11 x64，.NET Framework 4.8，无网络依赖。
- 四边吸附、边缘悬停展开、自动收起、软件标签页、快捷键 / 功能 / 备注三列、四种主题和中英界面。
- 独立 C# / WPF 实现；Afterhours 仓库分析和架构说明见 `docs/`。

## 0.9 更新：标签页与标签组排序

按住软件标签或组标签的鼠标左键约 0.3 秒后拖动。拖到标签左右边缘会出现竖直插入线，松开即可改变前后顺序；外层软件标签与组可混合排列，组内软件标签也可排序。顺序立即保存，重启和 JSON 备份保留。新建标签、组，以及从组内移出的标签会追加到外层已有顺序之后。

将软件标签拖到组标签中间，边框出现主题色高亮后松开，即可加入组；组标签左右各约四分之一区域用于排序。组之间不嵌套。标签栏放不下时，拖到左右边缘可以滚动寻找位置。按 Esc 或放到标签栏外取消，拖动期间保持展开；固定面板时锁定排序。排序保留当前搜索、活动标签和词条选择，单击软件标签、双击组和返回导航照常使用。

解散组时，成员按组内顺序替换该组在外层的位置，保留周围已排好的标签顺序。旧数据自动补齐排序字段：原有组和软件标签沿用 v0.8 的显示顺序，成员顺序不变。

## 0.8 更新：标签组

外层标签栏点击 `+`，选择“新建标签组”，填写名称。组标签显示名称和成员数量；单击保持当前位置，双击进入组内标签页，左侧 `‹` 返回外层。当前支持一层分组，组内的 `+` 直接创建软件标签。

按住组外软件标签拖到组标签上，组边框出现主题色高亮后松开，即可移入。拖动不复制或清空快捷键，组外标签移入后从外层隐藏；双击目标组即可查看。组内可右键成员 → “移出标签组”，也可拖到返回箭头上移出。

右键组标签或组内名称可重命名、解散。解散前有确认提示，只将成员移回外层，保留所有快捷键、备注、置顶与顺序。每组名称最长 60 字符，最多 200 个组；软件标签总上限仍为 200。

分组、成员关系、当前组及活动标签自动保存，JSON 备份包含分组；旧版数据自动作为外层标签保留。组内新建标签或 TXT 导入新标签会加入当前组，追加导入到另一组的已有标签后会导航到其所在位置。固定面板时锁定创建、重命名、解散和拖入 / 移出，双击导航、返回与搜索仍可使用。

## 0.7 更新：多选与拖拽排序

点击一条快捷键选中；按住 Shift 点击另一条，选中两条之间的全部词条。Ctrl 点击可增加或取消单独的词条。在已选中的任意一行按住鼠标左键约 0.3 秒后拖动，出现插入提示线，松开即可整组移动；组内保持原来的相对顺序。按 Esc 或拖到列表之外取消。排序立即保存，重启和 JSON 备份均保留。

置顶词条继续优先显示，置顶与普通词条分别拖动排序；混合选择两组时会提示先分开选择。搜索状态下只调整可见结果，隐藏词条在保存列表中的位置保持不变；清空搜索可查看完整列表。切换搜索、标签或固定面板会取消尚未完成的拖动。

拖动期间面板保持展开。固定面板时可选中词条、搜索和切换标签，但无法改动顺序；取消固定后恢复。多选时右键中的逐条编辑、删除和置顶操作暂停，单击选中一行后即可使用。

## 0.6.1 更新：搜索文字显示

修复搜索框重复应用内边距导致输入文字和光标被裁切的问题；中文和字母在四种主题、80%–150% 字号以及固定状态下均可正常显示。更新程序前先从托盘退出旧版，原有数据会继续沿用。

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

右键快捷键可置顶 / 取消置顶，置顶项在各软件标签页内优先显示，组合前有 `↑` 标记；多个置顶项按保存顺序显示，可在置顶组内拖动排序。

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
| 创建软件标签页 | 外层 `+` → 新建软件标签页；组内 `+` 直接新建 |
| 创建标签组 | 外层 `+` → 新建标签组，填写名称 |
| 进入 / 返回标签组 | 双击组标签进入；点击左侧 `‹` 返回外层 |
| 标签页 / 组排序 | 长按约 0.3 秒，拖到左右插入线后松开；组内也可排序 |
| 标签拖入组 | 将软件标签拖到组标签上，边框高亮后松开 |
| 标签移出组 | 右键成员 → 移出标签组；或拖到返回箭头上 |
| 重命名 / 解散组 | 右键组标签或组内名称；解散将标签移回外层，保留内容 |
| 重命名、删除标签页 | 右键该标签页 |
| 添加快捷键 | 点击底部“添加快捷键”，填写组合和功能 |
| 选择多条快捷键 | 点击一行，再按住 Shift 点击另一行连选；Ctrl 点击可逐条增加 / 取消 |
| 拖拽排序 | 在选中行上按住左键约 0.3 秒后拖动，按插入提示线松开；Esc 取消；置顶与普通词条分别排序 |
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

编辑弹窗、菜单、标题拖动、词条拖拽、缩放，以及搜索框刚发生输入的 1.5 秒内会暂缓自动收起。搜索框单纯保留焦点不会无限阻止收起；点击外部也会解除搜索保护。普通应用上方可以唤出；前台应用占满整块屏幕时停止边缘唤出，托盘仍可主动展开。

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

- `state.json`：标签组、成员关系、当前组、标签栏顺序、标签页、快捷键、备注、置顶、活动标签和设置；修改后原子保存。
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

验证使用独立测试数据，不会写入正式数据。0.9.0 共 455 项通过，记录见 `docs/verification-v0.9.0.txt`。新增混合标签栏与组内排序、旧排序字段迁移、插入线和归组中心命中、当前搜索与选择保留、取消、固定锁定、保存恢复及解散后原位展开检查。原有分组、多选词条排序、中文 / 字母搜索实际像素、四种主题、三档字号、表头拖调、Emoji 和玻璃合成检查继续通过。控制器和布局自动验证没有注入系统鼠标；完整 OLE 拖动手势仍需实际鼠标体验确认。`docs/preview-v0.6.png` 为专用彩色测试背景上的实际系统合成截图；其余控件截图以 WPF 渲染为主。报告不代表已完成多台电脑的兼容性测试。

也可用 Visual Studio 的“.NET 桌面开发”工作负载和 4.8 Developer Pack 打开 `ShortcutDock.csproj`。当前机器采用 `build.ps1` 路径完成编译，未验证 Visual Studio 项目构建路径。

## English quick start

Hold an app tab or group for about 0.3 seconds and drag to a vertical insertion line to reorder it. Top-level tabs and groups can be interleaved; member tabs can be sorted within their group. The center of a highlighted group adds an app tab to it, while its outer quarters are ordering targets. Drag near the bar edges to scroll. Order saves automatically and persists in JSON backups. Escape cancels; panel pinning locks reordering. Ungrouping replaces the group in place with its ordered member tabs.

Unzip the portable package, then double-click `Keyside.exe` (source build: `dist/Keyside.exe`). The app docks to the right of the primary monitor. Hover the thin edge strip to reveal it; leave to hide. Drag its title near any work-area edge to dock, or choose an edge in Settings.

At the top level, + offers New software tab and New tab group. Create a group, double-click to enter and use the back arrow to return. Drop an outside app tab onto a highlighted group to move it inside. Inside a group, + creates a member tab. Right-click a member to move it out, or drag it onto the back arrow. Groups can be renamed or dissolved from their context menu; dissolving keeps all tabs and shortcuts. Groups and current navigation persist in JSON backups. Panel pinning locks group edits while keeping navigation and search available.

Click to select a row, Shift-click to select a range or Ctrl-click to toggle individual entries. Hold a selected row for about 0.3 seconds, drag to the insertion line and release to save the group's order. Escape cancels. Pinned and ordinary entries reorder separately. Filtering only reorders visible matches, preserving hidden entries' saved positions. Panel pinning locks reordering.

Use `+` to create software tabs. Right-click a tab to rename or delete it. The three columns are shortcut combination, action, and notes. Double-click a notes cell to edit, including empty cells. Right-click a shortcut to pin it to the top. Offline color emoji rendering supports all Unicode Emoji 17.0 fully-qualified sequences; the notes editor provides a color preview. Entries are reference text and do not trigger keyboard input in other apps.

Drag a header divider, including the notes column's right boundary, to resize any column. Proportions persist across tabs and restarts and adapt to the panel width. Hiding notes expands the remaining columns; showing it restores its width. Column resizing is locked while the panel is pinned.

Settings → Panel color offers exactly three palettes: soft blue (#E0EAFE), soft pink (#FEE0EA), and soft green (#EAFEE0). Version 0.5 uses a lightly tinted neutral panel, dark gray text and soft accent buttons, closer to version 0.2. Dark / glass / wallpaper modes adapt the selected palette. Save persists changes and Cancel restores them. The color wheel and HEX input have been removed.

Settings → Text size scales panel text and emoji from 80% to 150%. The minimum panel height grows when necessary to keep a complete shortcut row visible. Use the bottom Hide notes / Show notes button to collapse or expand the entire notes column without deleting its contents. Settings also has a Show notes column checkbox. These preferences persist across tabs and restarts. The supplied Xiaohongshu image is embedded beside the subtitle.

Use `···` → Import shortcuts TXT for bulk import. The next menu item, Save TXT template, saves a ready-to-import UTF-8 example. Each line is `shortcut@action`. UTF-8 is recommended; UTF-16 with BOM and GB18030 are also supported. Preview before creating a named tab or appending to an existing one. Identical shortcut/action pairs are skipped without changing existing notes or pins. JSON backups preserve the complete state.

Settings includes frosted glass, light, dark and wallpaper-brightness themes, plus Chinese and English UI. Drag any border or corner to resize. The transparency slider now affects the background only, from 0–65%; text and emoji remain opaque. Save remembers it and Cancel restores it. Frosted glass draws a separately blurred backdrop, refreshed every 600 ms, and adapts text contrast to background brightness. Pictures stay in memory; editors stay opaque. Click outside a docked, unpinned panel to hide immediately. Pin keeps it open and locks editing, imports, settings, movement and resizing; search, tab navigation, notes visibility, backups and template download remain available. Unpin restores editing. The system tray can show the panel or exit. Backups are available from `···`; data lives in `%LOCALAPPDATA%\Keyside`.

Requires Windows 10/11 x64 and .NET Framework 4.8. No SDK is needed to run the included executable. Captured glass requires Windows 10 version 2004 or newer; older systems use the native acrylic compatibility path, subject to OS support and transparency settings. This is an unsigned MVP; Windows 10 and physical mixed-DPI monitors still need manual compatibility testing.

## Third-party attribution

Emoji artwork: [Twemoji 17.0.3](https://github.com/jdecked/twemoji/tree/v17.0.3), © Twitter, Inc. and other contributors, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). PNG artwork is unchanged. Unicode test data is distributed under Unicode License v3. Full attribution and license texts are included in `THIRD-PARTY-NOTICES.md` and `licenses/` (source package: `assets/`).
