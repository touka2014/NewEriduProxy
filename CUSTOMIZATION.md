# 新艾利都代理定制说明

本分支在 v2rayN master 基础上增加多节点并行代理能力，并移除 Promotion 推广入口。

## 多节点并行使用

1. 在节点列表中用 `Ctrl` / `Shift` 选择一个或多个节点。
2. 在每行的 **Mixed 端口** 中填写该节点要监听的本地端口。
3. 点击 **启动选中**。每个节点会启动独立核心进程并监听 `mixed://127.0.0.1:<端口>`。
4. 列表会持续显示运行状态、实时上传、实时下载、每日与累计流量。
5. 使用 **停止选中** 或 **全部停止** 回收对应进程。

传统主代理的默认 mixed 入站已调整为 20808，并在状态栏提供独立开关，默认关闭；旧配置中仍为上游默认值 10808 的会自动迁移。并行节点不会修改系统代理。Xray、v2fly、v2fly v5 与 sing-box 节点可以同时运行。自定义完整配置由于无法可靠重写其入站与统计端点，不开放并行启动。

软件界面固定为英文。新导入节点的独立 mixed 端口从 40000–48999 范围分配，会排除系统 TCP/UDP 监听端口、主入站端口和已分配端口。`Organize selected` 会为所选节点分配一段连续的端口。每个节点的 `LAN access` 独立保存，默认仅监听 127.0.0.1，开启后监听 0.0.0.0。

节点列表前五列固定为 `LAN access`、`Status`、`Mixed port`、`Live upload`、`Live download`，旧的列布局缓存不会覆盖该顺序。并行进程停止或退出后会删除它的临时核心配置，启动时也会清理上次异常退出留下的文件。

## 数据与进程

- 每个节点的 mixed 端口保存在 `ProfileExItem`。
- 每日与累计流量继续保存在 `ServerStatItem`，日期变化时每日统计自动清零。
- 应用退出时会停止全部并行核心并保存流量数据。
- 启动前会检查 TCP/UDP 端口占用；冲突时不会静默改用其他端口。

## 构建验证

首次构建 Windows WPF 版本前，先下载并校验固定版本的 Xray、sing-box 与路由数据：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare-cores.ps1
```

这些运行文件会放入已被 Git 忽略的 `v2rayN\runtime-assets`，不会提交到仓库。随后执行：

```powershell
dotnet build v2rayN\v2rayN.sln -c Debug
```

Windows WPF 输出程序名为 `NewEriduProxy.exe`。
