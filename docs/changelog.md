# 变更记录 (Changelog)

本项目变更日志，遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 风格。

## [0.1.0] - 2026-08-18

### 文档
- 新增 `README.md`：项目概述、快速开始、工具列表、FAQ
- 新增 `docs/architecture.md`：架构与 DbgEng 集成细节、vtable 偏移说明
- 新增 `docs/config.md`：MCP 配置指南与故障排查
- 新增 `docs/tools.md`：5 个 MCP 工具的参数详解
- 修正 `.gitignore`：不再全局忽略 `*.exe`/`*.dll`，避免误伤单文件发布产物

### 修复
- **vtable 偏移错误**：`DebuggerSession.cs` 中 `Execute`/`GetExecutionStatus`/`SetOutputCallbacks` 的 vtable 索引由接口内序号改为真实索引（+3 个 IUnknown 槽位）：
  - `Execute` 17 → 29
  - `GetExecutionStatus` 30 → 33
  - `SetOutputCallbacks` 46 → 60
  - 原因：直接调用 `Execute` 时实际命中 `SetOutputWidth`，导致崩溃/异常
- `Execute` 标志由 `DEBUG_EXECUTE_ECHO` 改为 `DEBUG_EXECUTE_DEFAULT`，避免命令文本回显污染输出解析

### 遗留（未处理）
- `windbg-mcp-server.py`：早期 Python 半成品，COM 调用未实现，与 C# 版功能重叠，建议删除或归档

---

## [未发布]

### 计划
- 并发控制：`ExecuteCommand` 的"清空→执行→读取"加锁/串行化
- 简化 `DebugClient.cs` 冗余的 `IDebugControl` 接口声明（当前实际走 vtable）
- 可选：把发布产物 `publish/` 纳入版本管理
