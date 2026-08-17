// Tools/WindbgTools.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using WindbgMcp.DbgEng;

namespace WindbgMcp.Tools;

public static class WindbgTools
{
    public static object[] GetToolDefinitions() => new object[]
    {
        new {
            name = "wd_execute",
            description = "在 WinDbg GUI 中执行调试命令。命令会真正在 GUI 里执行，你可以实时看到输出。",
            inputSchema = new {
                type = "object",
                properties = new {
                    command = new { type = "string", description = "WinDbg 命令，如 '!analyze -v', 'kp', 'lm vm nt'" },
                    timeout = new { type = "number", description = "超时秒数", @default = 15 }
                },
                required = new[] { "command" }
            }
        },
        new {
            name = "wd_analyze",
            description = "自动执行完整 crash 分析：!analyze -v + 调用栈 + 故障模块信息",
            inputSchema = new {
                type = "object",
                properties = new {
                    deep = new { type = "boolean", description = "同时拉取调用栈和模块信息", @default = true }
                }
            }
        },
        new {
            name = "wd_callstack",
            description = "获取当前线程调用栈",
            inputSchema = new {
                type = "object",
                properties = new {
                    thread = new { type = "number", description = "线程索引（可选，默认当前线程）" }
                }
            }
        },
        new {
            name = "wd_inspect",
            description = "智能检查：给定地址或符号，自动选择 dt / dps / da / u 显示内容",
            inputSchema = new {
                type = "object",
                properties = new {
                    target = new { type = "string", description = "地址或符号" },
                    hint = new { type = "string", description = "struct / string / code / raw" }
                },
                required = new[] { "target" }
            }
        },
        new {
            name = "wd_status",
            description = "获取调试会话状态（执行状态、目标类型）",
            inputSchema = new { type = "object", properties = new { } }
        }
    };

    public static async Task<string> CallTool(string name, JsonElement args, DebuggerSession session)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (name)
                {
                    case "wd_execute":
                        var cmd = args.GetProperty("command").GetString()!;
                        var timeout = args.TryGetProperty("timeout", out var t) ? t.GetInt32() : 15;
                        return session.ExecuteCommand(cmd, timeout * 1000);

                    case "wd_analyze":
                        var deep = args.TryGetProperty("deep", out var d) && d.GetBoolean();
                        var result = new System.Text.StringBuilder();
                        result.AppendLine("=== !analyze -v ===");
                        result.AppendLine(session.ExecuteCommand("!analyze -v", 30000));
                        if (deep)
                        {
                            result.AppendLine("\n=== Callstack (kp) ===");
                            result.AppendLine(session.ExecuteCommand("kp", 10000));
                            result.AppendLine("\n=== Faulting module (lm vm) ===");
                            result.AppendLine(session.ExecuteCommand("lm vm *", 10000));
                        }
                        return result.ToString();

                    case "wd_callstack":
                        return session.ExecuteCommand("kp", 10000);

                    case "wd_inspect":
                        var target = args.GetProperty("target").GetString()!;
                        var hint = args.TryGetProperty("hint", out var h) ? h.GetString() ?? "" : "";
                        string inspectCmd = hint switch
                        {
                            "string" => $"da {target}",
                            "code" => $"u {target}",
                            "raw" => $"dd {target} L20",
                            _ when target.Contains("!") || target.StartsWith("_") => $"dt {target}",
                            _ => $"dps {target} L8"
                        };
                        return session.ExecuteCommand(inspectCmd, 10000);

                    case "wd_status":
                        var status = session.GetExecutionStatus();
                        var statusName = status switch
                        {
                            0 => "NO_DEBUGGEE",
                            1 => "BREAK",
                            2 => "GO",
                            3 => "STEP_INTO",
                            4 => "STEP_OVER",
                            5 => "STEP_OUT",
                            6 => "DEAD",
                            _ => $"UNKNOWN({status})"
                        };
                        return $"Execution Status: {statusName}\n\nSession connected to WinDbg GUI.";

                    default:
                        return $"Unknown tool: {name}";
                }
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        });
    }
}