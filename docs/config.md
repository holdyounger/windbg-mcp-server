# MCP 配置指南

## 标准配置（本机 WinDbg GUI）

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

### 字段说明

| 字段 | 值 | 说明 |
|------|-----|------|
| `command` | `C:\tools\WindbgMcp\WindbgMcp.exe` | 发布后的 exe 绝对路径 |
| `args[0]` | `50000` | 传给 `Main(string[] args)` 的端口号，须与 WinDbg `.server tcp:port=` 一致 |
| `env.PATH` | 含 WinDbg x64 目录 | **必须**，否则加载不了 `dbgeng.dll` |

> 不同 MCP 客户端配置文件位置不同（Claude Desktop 是 `claude_desktop_config.json`，OpenClaw 是 `~/.openclaw/config.json` 等），但 schema 相同。

## OpenClaw 中的配置

如果用的是 OpenClaw，在 `~/.openclaw/config.json` 的 `mcpServers` 段加同样的块即可。OpenClaw 会以 stdio 方式拉起 `WindbgMcp.exe` 并与之通信。

## 前置动作（每次调试前）

**MCP 服务器启动前**，必须先手动：

1. 打开 WinDbg
2. 加载目标（`File → Open Crash Dump...` 或附加进程）
3. 在命令窗口执行 `.server tcp:port=50000`

否则 `WindbgMcp.exe` 启动时 `DebugConnect` 失败并报：

```
FATAL: DebugConnect failed (0x...). 确认 WinDbg 里执行了 '.server tcp:port=50000' 且 dbgeng.dll 版本匹配。
```

## 故障排查

| 症状 | 原因与解决 |
|------|-----------|
| `DebugConnect failed 0x80004005` | WinDbg 没开 `.server`，或端口不一致 |
| `DebugConnect failed 0x8007007E` | 找不到 `dbgeng.dll` → 检查 `env.PATH` |
| 启动即退出 `FATAL: ...` | 连接失败，见上两行 |
| 工具调用返回空输出 | 输出回调未注册 / 命令无输出；先 `wd_status` |
| 命令执行但 GUI 里看不到 | 用了 `DEBUG_EXECUTE_NOT_LOGGED`？应保持 `ALL_CLIENTS` 广播 |
| 换 WinDbg 版本后崩溃 | vtable 偏移变了，见 `architecture.md` |

## 远程连接（可选）

把 `DebuggerSession.cs` 的 `remote` 改为远程地址即可连到别的机器上的 WinDbg：

```csharp
// 本机
string remote = $"tcp:server=127.0.0.1,port={port}";
// 远程
string remote = $"tcp:server=<host>,port={port}";
```

需确保目标机 `.server` 已监听，且防火墙放行端口。
