# TaijiBall · 流光版（实验）

这是 `..\TaijiBall` 的**实验分支**，唯一区别是把"八卦化作流光汇聚成页面"的效果重做了一遍。
稳定版（无动画）仍然在 `..\TaijiBall`，两边可以并存、各自独立运行。

## 这次的流光怎么做的

上一版翻车的原因：为了画从八卦飞向页面的光，临时创建了一个**置顶精灵窗口**，
它一出现就把刚弹出的右键菜单顶掉激活 —— 菜单"闪一下就消失"。

这一版换了三条原则：

1. **不创建任何额外窗口**。流光直接画在球自己的分层窗口里，终点按球的边界做钳制，
   表现为"八卦化作八道金光往外流走"，随后页面出现。
2. **先播动画，再开页面**。右键 / 长按 / 设置都是：开始流光 → 260ms 后动画结束回调里才打开菜单/黄历/设置。
   这样动画永远不可能和"菜单正在弹出"打架。
3. **不动窗口尺寸**。流光期间球的窗口保持原大小，不调用任何改变尺寸的 `UpdateLayeredWindow`。

## 效果参数

- 总时长 0.26s（`OnFrame` 里 `dt / 0.26f`）
- 八道流光错峰 3.5%，单道飞行 75%，金色拖尾 + 发光头
- 八卦在前 40% 的时间内淡出（"化光"），流光在最后 28% 淡出

![流光](preview-flow.png)

## 文件与用法

和稳定版完全一致：`TaijiBall.exe` 双击运行（一次 UAC），`Install-TaijiBallTask.ps1` 注册开机静默启动，
`启动太极球.vbs` 静默拉起。命令行调试参数也保留：

- `TaijiBall.exe --fx 0.5 out.png`   渲染流光中间帧（t=0~1）
- `TaijiBall.exe --render 0 out.png 58 1 0 1`
- `TaijiBall.exe --settings` / `--shot` / `--opt-test`

## 已知问题修复

**打开设置崩溃**（`ObjectDisposedException: SettingsForm`）：原来 `OpenSettings` 用
`using (var dlg = new SettingsForm(...))` 创建窗体，但流光要 0.26 秒后才回调 `ShowDialog`，
那时 `using` 作用域早就结束、窗体已释放。改成不用 `using`，在回调的 `finally` 里 `dlg.Dispose()`。

另外给整个程序加了兜底：`Application.ThreadException` 会把未处理异常写到
`%TEMP%\TaijiBall-error.log` 而不是弹"程序已停止工作"，球不会因为单次异常直接退出。

## 说明

效果是"先流光、后弹页"，所以点右键会有约 0.26 秒的延迟感 —— 这是刻意的，
换来的是动画绝不会干扰弹窗。如果嫌慢，把 `OnFrame` 里的 `0.26f` 调小即可。