# OBS 捕获与准心指针

## 显示器捕获

1. 固定需要录制的 Reticle 控件，按 Win+G 收起 Game Bar。
2. 在 OBS 的显示器捕获属性中确认选择的是显示控件的屏幕。
3. 如果当前 Windows Graphics Capture 方法漏掉控件，改为 **DXGI 桌面复制**进行对比，录制几秒确认成片；这一步是兼容性排查，不保证所有 Windows / Game Bar 版本都能捕获。
4. 确认显示器捕获上方没有不透明的游戏或窗口来源遮住画面。

Reticle 0.2.11 显式将自身视图的 `ApplicationView.IsScreenCaptureEnabled` 设为 true；这不能覆盖 Game Bar 宿主或系统的捕获限制。游戏捕获直接采集游戏渲染内容，无法据此保证包含独立的 Game Bar 窗口。

## 屏幕中央的鼠标指针

0.2.11 在准心固定显示时将自身 CoreWindow 的 PointerCursor 设为空，打开 Game Bar 或关闭控件时恢复原值。只影响准心窗口，不修改系统鼠标设置或游戏输入。

若游戏点击被控件挡住，在 Game Bar 顶栏启用鼠标穿透。该开关由 Game Bar 提供，应用只能读取其状态。

参考：[Microsoft 点击穿透](https://learn.microsoft.com/en-us/xbox/game-bar/guide/click-through)、[应用视图屏幕捕获属性](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.applicationview.isscreencaptureenabled)、[OBS 显示器捕获](https://obsproject.com/kb/display-capture-sources)、[OBS 捕获方法源码](https://github.com/obsproject/obs-studio/blob/master/plugins/win-capture/duplicator-monitor-capture.c)。
