# Changelog

## 0.9.0 · 2026-10-10

- Hold an app tab or group for 320 ms, drag to the vertical insertion line and release to reorder. Top-level tabs and groups can be interleaved; member tabs reorder within their group.
- Use a group's middle half to add an app tab to it, with the accent border marking a join target. Its outer quarters mark ordering destinations. Groups do not nest.
- Scroll horizontally when dragging near the bar edges. Keep the panel expanded during the gesture; Escape cancels and panel pinning locks sorting.
- Save root mixed order in RootOrder and member order in existing tab slots. Preserve current search text, caret, active tab and shortcut selection when redrawing the bar.
- Migrate older files using their existing groups-first layout, repair stale order keys, and append newly visible root items. Dissolving a group replaces it in place with its ordered members.
- Preserve ordinary button clicks by leaving capture release to WPF's mouse-up handler. Keep double-click group navigation and moving tabs in or out available.
- Pass all 455 isolated checks, including migration, mixed and member ordering, real button geometry, insertion markers, cancellation, locking, saved order and routed Button release / click.

## 0.8.0 · 2026-10-09

- Create named tab groups from the tab bar's + menu. Double-click a group to enter; use the back arrow to return to the top level.
- Drag a software tab onto a group to move it inside, with an accent border indicating a valid target. Drag a member onto the back arrow or use its context menu to move it out.
- Rename groups or dissolve them while keeping every member tab and shortcut. Tabs and new TXT imports created inside a group inherit its membership.
- Save groups, membership and current navigation in local JSON and backups. Older files receive an empty group list and keep all existing tabs at the top level.
- Lock group editing and dragging while pinned; keep group navigation and search available.
- Retain shortcut multi-selection and reordering, visible search input and existing appearance settings.
- Pass all 413 isolated checks, including legacy group migration, navigation, valid drop targets, tab membership, locking and retained content.

## 0.7.0 · 2026-10-09

- Select a shortcut by clicking; Shift-click selects a range and Ctrl-click toggles individual entries.
- Hold a selected row for 320 ms and drag to move the selected entries together, preserving their relative order. Show an insertion line, scroll near list edges and save the new order immediately.
- Keep the dock expanded during the press and drag; Escape cancels. Lock reordering while the panel is pinned, and cancel stale drags after filtering or switching tabs.
- Preserve the pinned group above ordinary entries. Reorder each group separately; filtered sorting changes only matching slots, keeping hidden entries in place.
- Retain the v0.6.1 search rendering fix and existing local data without a schema migration.
- Pass all 376 isolated checks, including routed press / release, range selection, multi-row movement, insertion hit testing, cancellation and stored order.

## 0.6.1 · 2026-10-09

- Fix invisible search input and a clipped caret caused by applying TextBox padding twice in the custom template.
- Keep Chinese and Latin input visible across all themes, supported text sizes and pinned / unlocked panels.
- Add rendered-glyph regression checks; search filtering and existing local data remain compatible.
- Pass all 344 isolated checks, including 48 Chinese / Latin search rendering scenarios.

## 0.6.0 · 2026-10-08

First public release of Keyside.

- Resize all three columns by dragging header boundaries; save width proportions and restore the notes column after expanding it.
- Make transparency affect the background only, keeping text and emoji opaque across all four themes.
- Draw frosted glass using a separately blurred backdrop, refreshed every 600 ms, with automatic text contrast. Keep editors opaque and use a compatibility appearance when capture is unavailable.
- Include Chinese / English documentation, a ready-to-import TXT template and portable Windows x64 download.
- Pass 200 isolated checks, including actual window composition over dedicated test backgrounds.

## 0.5 · Development version

- Soften the three palettes using a lightly tinted neutral panel, gray text and subdued accent controls.
- Refine title / subtitle spacing and icon size.
- Lock editing, imports, settings, movement and resizing while pinned; keep search and navigation available.
- Add Save TXT template to the import menu.

## 0.4 · Development version

- Replace the color wheel with soft blue, pink and green palettes.
- Add 80%–150% text scaling and a collapsible notes column.
- Embed the supplied subtitle icon.

## 0.3 · Development version

- Add entry pinning, editable notes, offline color emoji and TXT batch imports with preview and duplicate skipping.

## 0.2 · Development version

- Fix side docking detection and immediate hiding on outside clicks.
- Add resizing from every edge / corner and a saved transparency setting.

## 0.1 · Development version

- Introduce four-edge docking, hover reveal, software tabs, shortcut editing, search, themes, Chinese / English UI and local JSON persistence.
