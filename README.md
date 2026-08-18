# WindbgMcp — WinDbg GUI 桥接 MCP Server

> 通过 MCP 协议把 AI 助手接入你**正在使用的 WinDbg GUI**，让命令真正在你眼前的窗口里执行、实时可见。

## 这是什么

`WindbgMcp.exe` 是一个 **MCP (Model Context Protocol) 服务器**。它不是自己新开一个调试会话，而是**桥接**到你已经打开的 WinDbg GUI 上：

```
┌────────────┐   MCP stdio   ┌──────────────┐   DebugConnect   ┌─────────────────┐
│ AI 助手/MCP │ ◄───────────► │ WindbgMcp.exe │ ◄──────────────► │ WinDbg GUI       │
│ 客户端      │   JSON-RPC    │ (C# MCP 服务) │   tcp:50000      │ .server tcp:50000│
└────────────┘               └──────────────┘                  └─────────────────┘
```

关键特性：

- **复用你的 GUI 会话**：命令（`!analyze -v`、`kp`、`dt` 等）在 WinDbg 窗口里真实执行，你肉眼可见
- **实时可见**：AI 的操作和输出同步呈现在 GUI，可随时手动中断、介入
- **带完整内存的 dump 分析友好**：配合 `w3wp.dmp` 这类 full-memory dump 使用
- **纯 Windows**：底层走 DbgEng COM（`dbgeng.dll`）

---

## 快速开始

### 前置条件

| 依赖 | 说明 |
|------|------|
| WinDbg (Windows 10/11 SDK Debuggers) | 含 `dbgeng.dll`、`windbg.exe`，位于 `C:\Program Files (x86)\Windows Kits\10\Debuggers\x64` |
| .NET 8 SDK | 构建/发布用（`dotnet`） |
| MCP 客户端 | 支持 stdio MCP 的客户端（如 Claude Desktop、OpenClaw 等） |

### 第 1 步：启动 WinDbg 并开 .server

在 WinDbg 中加载目标 dump（或附加进程）后，在命令窗口执行：

```
.server tcp:port=50000
```

看到类似 `Server started.  Client can connect by any of: tcp:port=50000` 即成功。

### 第 2 步：构建 & 发布

```powershell
# 构建（调试）
dotnet build -c Debug

# 发布单文件 exe（推荐，MCP 配置用这个）
dotnet publish -c Release -r win-x64 -o C:\tools\WindbgMcp
```

### 第 3 步：配置 MCP 客户端

以 `claude_desktop_config.json` 为例：

```json
{
  "mcpServers": {
    "windbg-gui": {
      "command": "C:\\tools\\WindbgMcp\\WindbgMcp.exe",
      "args": ["50000"],
      "env": {
        "PATH": "C:\\Program Files (x86)\\Windows Kits\\10\\Debuggers\\x64;C:\\Windows\\System32"
      },
      "description": "Bridge to human-facing WinDbg GUI via .server tcp:port=50000"
    }
  }
}
```

> **`env.PATH` 必须包含 WinDbg 的 x64 目录**，否则进程加载不了 `dbgeng.dll`，`DebugConnect` 会失败。

### 第 4 步：验证

重启 MCP 客户端，确认 `windbg-gui` 服务器状态为 connected，然后询问助手"在 WinDbg 里执行 `!analyze -v`"。

---

## 提供的工具 (Tools)

| 工具 | 作用 |
|------|------|
| `wd_execute` | 在 WinDbg GUI 中执行任意调试命令，返回输出 |
| `wd_analyze` | 自动跑 `!analyze -v` + 调用栈 + 故障模块 |
| `wd_callstack` | 获取当前线程调用栈 (`kp`) |
| `wd_inspect` | 智能检查：按 hint 自动选 `dt`/`dps`/`da`/`u`/`dd` |
| `wd_status` | 查询调试会话执行状态 |

详细参数见 [docs/tools.md](docs/tools.md)。

---

## 项目结构

```
windbg-mcp-server/
├── Program.cs                  # 入口：MCP stdio JSON-RPC 循环 + 请求分发
├── WindbgMcp.csproj            # 项目文件（net8.0, win-x64, 单文件发布）
├── WindbgMcp.sln               # 解决方案
├── DbgEng/
│   ├── DebugClient.cs          # DbgEng GUID、DebugConnect P/Invoke、接口声明
│   ├── DebuggerSession.cs      # DebugConnect 连接 + vtable 方法调用（核心）
│   └── OutputCallbacks.cs      # IDebugOutputCallbacks COM 回调，捕获输出
├── Tools/
│   └── WindbgTools.cs          # 5 个 MCP 工具的定义与实现
└── docs/
    ├── architecture.md         # 架构与 DbgEng 集成细节
    ├── config.md               # MCP 配置与常见问题
    ├── tools.md                # 工具参数详解
    └── changelog.md            # 变更记录
```

---

## 常见问题 (FAQ)

**Q: 启动报 `DebugConnect failed`**
A: 确认 (1) WinDbg 里执行了 `.server tcp:port=50000`；(2) 端口一致；(3) `PATH` 含 WinDbg x64 目录；(4) 用 **10.0 及以上** SDK（老版本 DbgEng 的 IDebugControl vtable 偏移不同，见 [docs/architecture.md](docs/architecture.md#vtable-偏移说明)）。

**Q: 命令执行了但返回空**
A: 可能是输出回调未注册成功，或命令本身无输出。用 `wd_status` 确认会话状态。

**Q: 单文件 exe 发布产物被 git 忽略**
A: 已修正 `.gitignore`，不再全局忽略 `*.exe`/`*.dll`。如需把 `publish/` 纳入版本管理，取消 `.gitignore` 末尾对应注释。

**Q: 想连的是远程机器上的 WinDbg？**
A: `.server` 支持多传输（`tcp`/`namedpipe`/`com` 等）。改 `DebuggerSession` 里的 `remote` 字符串即可，如 `tcp:server=<host>,port=<port>`。

---

## 许可证 / 说明

个人调试工具项目。依赖 [DbgEng](https://learn.microsoft.com/windows-hardware/drivers/debugger/)（Windows SDK 调试引擎）与 [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol)（MCP SDK v2.2.0）。
