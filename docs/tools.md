# 工具参考 (Tools)

`WindbgMcp.exe` 通过 MCP `tools/list` 暴露以下工具。AI 助手可根据场景自动选用。

## wd_execute

在 WinDbg GUI 中执行任意调试命令，返回其输出。

| 参数 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| `command` | string | ✅ | WinDbg 命令，如 `!analyze -v`、`kp`、`lm vm nt`、`dt _EPROCESS` |
| `timeout` | number | ❌ | 等待输出的最大秒数，默认 15 |

**示例**
```json
{ "name": "wd_execute", "arguments": { "command": "!analyze -v", "timeout": 30 } }
{ "name": "wd_execute", "arguments": { "command": "~*kb" } }
{ "name": "wd_execute", "arguments": { "command": "!runaway" } }
```

## wd_analyze

自动执行完整 crash 分析流程，一次性返回多段信息。

| 参数 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| `deep` | boolean | ❌ | 是否同时拉调用栈 + 故障模块，默认 `true` |

**内部执行序列**（deep=true）：
1. `!analyze -v`
2. `kp`（当前线程调用栈）
3. `lm vm *`（模块列表）

## wd_callstack

获取当前线程（或指定线程）调用栈。

| 参数 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| `thread` | number | ❌ | 线程索引（可选，默认当前线程） |

> 当前实现始终执行 `kp`（当前线程）。`thread` 参数预留，未实际切换线程上下文。

## wd_inspect

智能检查指定地址/符号，按 `hint` 自动选择显示命令。

| 参数 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| `target` | string | ✅ | 地址（如 `0xfffff...`）或符号（如 `nt!_EPROCESS`） |
| `hint` | string | ❌ | 提示：`string` / `code` / `raw` / `struct` |

**命令选择逻辑**

| hint | 命令 |
|------|------|
| `string` | `da <target>` |
| `code` | `u <target>` |
| `raw` | `dd <target> L20` |
| 含 `!` 或 `_` 前缀 | `dt <target>` |
| 其他 | `dps <target> L8` |

**示例**
```json
{ "name": "wd_inspect", "arguments": { "target": "nt!_EPROCESS" } }
{ "name": "wd_inspect", "arguments": { "target": "0x7ffc5fb7ece0", "hint": "code" } }
{ "name": "wd_inspect", "arguments": { "target": "0x0000005c2742000", "hint": "raw" } }
```

## wd_status

查询调试会话状态，返回执行状态枚举。

无参数。返回 `Execution Status: <值>`。

**状态值**
| 值 | 含义 |
|----|------|
| `NO_DEBUGGEE` | 无调试目标 |
| `BREAK` | 已中断（适合执行分析命令） |
| `GO` | 正在运行 |
| `STEP_INTO` / `STEP_OVER` / `STEP_OUT` | 单步中 |
| `DEAD` | 会话已结束 |

---

## 建议的 AI 分析流程

分析 crash dump 时，推荐组合：

1. `wd_status` → 确认会话可交互
2. `wd_analyze` (deep) → 拿到 `!analyze -v` + 栈 + 模块
3. `wd_execute "~*kb"` → 看所有线程（查死锁/挂起）
4. `wd_execute "!runaway"` → 找 CPU 热点线程
5. `wd_execute "!threads"` → .NET 进程看托管线程
6. `wd_inspect` → 深入某个地址/结构
