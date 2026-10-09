# Keyside v0.9.0 · Windows x64

完整解压后双击 `Keyside.exe`。需要 Windows 10 / 11 x64 和 .NET Framework 4.8。

v0.9.0 新增标签页与标签组长按排序。更新时先从托盘退出旧版，再完整解压新版运行；原有分组、标签和快捷键会自动保留。

按住标签页或标签组约 0.3 秒后拖动，放在左右插入线处即可调整前后顺序；标签页与组可混合排列，组内标签也可排序。拖到组中间的高亮区域则加入组。顺序自动保存，固定面板时锁定排序，Esc 取消。

标签栏 `+` → 新建标签组；双击组标签进入，左侧箭头返回。将组外软件标签拖到组标签上，边框高亮后松开即可加入；组内新建标签自动归入当前组。右键组可重命名或解散，解散只将标签移回外层；右键成员可移出组，也可拖到返回箭头上移出。分组和当前位置自动保存；固定时锁定编辑，导航和搜索可用。

点击选中一行，按住 Shift 点击另一行可连选；Ctrl 点击可选择分散的行。在选中行上按住左键约 0.3 秒后拖动，按插入提示线松开，顺序自动保存。置顶与普通词条分别排序，固定面板时锁定排序；Esc 取消拖动。

拖标题栏到屏幕边缘吸附；鼠标悬停细边缘条展开，点击外部收起。点击 `+` 新建软件标签或组，双击词条编辑，右键管理与置顶。设置里可调主题、三种配色、字号和背景透明度。

完整操作与 English quick start 见随包的 [USAGE.zh-CN.md](USAGE.zh-CN.md)。TXT 导入示例见 [Template.txt](Template.txt)。源码与最新下载：https://github.com/nengchuanyi-web/Keyside

Extract the entire archive and run `Keyside.exe`. Requires Windows 10 / 11 x64 and .NET Framework 4.8. Drag the title to a screen edge to dock; hover the thin strip to reveal and click outside to hide. Use `+` for app tabs, double-click to edit, and right-click to manage or pin entries. Appearance, text size and background transparency are in Settings.

Use + → New tab group, double-click to enter and the back arrow to return. Drop an app tab onto a highlighted group to add it. Right-click a member to move it out, or drag it onto the back arrow. Rename or ungroup from the group's menu; ungrouping keeps its tabs and shortcuts. Group editing is locked while pinned; navigation and search remain available.

Hold an app tab or group for about 0.3 seconds, then drag to an insertion line to change its order. Tabs and groups can be interleaved; member tabs can be sorted too. A group's highlighted center adds a tab to that group. Order saves automatically; pinning locks sorting and Escape cancels.

Click to select, Shift-click a range or Ctrl-click individual entries. Hold a selected row for about 0.3 seconds and drag; release at the insertion line to save. Escape cancels. Reorder pinned and ordinary entries separately; panel pinning locks reordering.

Application code: [MIT License](LICENSE). Third-party attribution: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`.
