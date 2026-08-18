# 架构与 DbgEng 集成

本文档说明 `WindbgMcp.exe` 如何通过 DbgEng 桥接 WinDbg GUI，以及代码实现的关键细节。

## 整体架构

```
                    MCP (stdio, JSON-RPC 2.0)
AI 客户端  ◄──────────────────────────────►  WindbgMcp.exe
                                             │
                                             │  DbgEng COM
                                             │  DebugConnect("tcp:server=127.0.0.1,port=50000")
                                             ▼
                                       WinDbg GUI (.server)
                                             │
                                             │  IDebugControl.Execute(...)
                                             ▼
                                     目标进程 / 内核 dump
```

### 为什么不直接 `DbgCreate` 开新会话？

直接开新会话的缺点是：命令在**后台隐藏会话**里执行，你什么都看不到，也无法手动中断。

本项目选择**桥接已有 GUI**（`DebugConnect` 连到 `.server`），好处：

- 命令在你看得见的窗口执行，可实时干预
- 断点、符号加载、手动中断（Ctrl+Break）都能用
- 与手动调试流程无缝衔接

代价：**必须先手动打开 WinDbg 并启动 `.server`**，且 WinDbg 与 WindbgMcp 的 `dbgeng.dll` 版本应匹配。

## 调用链

### 1. `DebugConnect` 建立远程连接

`DebugClient.cs` 通过 P/Invoke 调 `dbgeng.dll!DebugConnect`：

```csharp
[DllImport("dbgeng.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
public static extern int DebugConnect(
    string RemoteOptions,
    ref Guid InterfaceId,
    out IntPtr Interface);
```

连接串：`tcp:server=127.0.0.1,port=50000`，请求 `IDebugClient`。

### 2. `QueryInterface` 拿 `IDebugControl`

`DebuggerSession.cs` 对拿到的 client 做 `QueryInterface(IDebugControl)`，得到控制接口指针，再**手动解析 vtable** 取得方法委托。

### 3. 注册输出回调

`OutputCallbacks.cs` 实现 `IDebugOutputCallbacks` COM 接口（`Output` 方法把文本累积进 `StringBuilder`），通过 `IDebugControl.SetOutputCallbacks` 注册。这样 `Execute` 的输出会被捕获返回给 MCP。

### 4. `Execute` 执行命令

```csharp
int hr = _execute(_controlPtr, DEBUG_OUTCTL_ALL_CLIENTS, command, DEBUG_EXECUTE_DEFAULT);
```

`DEBUG_OUTCTL_ALL_CLIENTS` 让输出广播到所有客户端（GUI 也能看到）。随后轮询 `HasOutput` 直到有输出或超时。

---

## vtable 偏移说明 ⚠️

`DebuggerSession.cs` 通过**硬编码 vtable 索引**调用 COM 方法，而不是用 .NET COM 互操作（`ComImport`）。**必须**在索引里加上 `IUnknown` 的前 3 个方法槽位。

```
vtable 索引 = 3 (IUnknown: QueryInterface/AddRef/Release) + IDebugControl 接口内方法序号
```

以 **Windows 10 SDK DbgEng (10.0.x)** 的 `IDebugControl` 为例，本项目用到的方法：

| 方法 | 接口内序号 | vtable 索引（代码用） |
|------|:---:|:---:|
| `Execute` | 26 | **29** |
| `GetExecutionStatus` | 30 | **33** |
| `SetOutputCallbacks` | 57 | **60** |

> 接口内序号来自 `IDebugControl` 的定义顺序（`Execute` 是第 26 个接口方法，从 0 计，加上 3 个 IUnknown = 29）。
>
> **⚠️ 不同 DbgEng 版本 vtable 可能不同**：Windows 8/8.1 或旧版 Debugging Tools 的 `IDebugControl` 少几个方法，索引会偏移 1-2。若换了 SDK 版本导致调用异常，需核对对应版本的 `dbgeng.h` 方法顺序重算索引。
>
> 早期版本曾错误地直接用接口内序号（17/30/46），导致 `Execute` 实际调到 `SetOutputWidth` 而崩溃，已修正。

## 输出回调细节

- `OutputCallbackImpl` 实现 `IDebugOutputCallbacks`（`IUnknown` 3 方法 + `Output` 1 方法）
- `Marshal.GetComInterfaceForObject<OutputCallbackImpl, IDebugOutputCallbacks>` 生成 COM 指针
- `Output` 方法按 `Mask` 累积文本；调用前 `Clear()`，调用后 `GetOutput()`
- 并发：`_sb` 用 `lock` 保护，但 `ExecuteCommand` 的整体"清空→执行→读取"流程非原子，**多线程并发调用需外部串行化**

## 线程模型

- `Program.cs` 主循环逐行读 stdio，处理 JSON-RPC 请求
- `WindbgTools.CallTool` 用 `Task.Run` 把命令执行放到线程池（避免阻塞读循环）
- `DebuggerSession` 与 COM 回调跨线程交互——COM 回调可能来自不同线程，`OutputCallbackImpl` 内部加锁是必要的

## 关键风险与注意

1. **vtable 硬编码**：最脆弱的点，换 DbgEng 版本需重算索引
2. **dbgeng.dll 依赖**：运行时必须能从 `PATH` 找到对应版本的 `dbgeng.dll`
3. **单线程限制**：WinDbg 会话一次只能执行一个命令，MCP 端应串行调用工具，避免命令交错
4. **`SelfContained=false`**：发布产物需要目标机有 .NET 8 运行时
