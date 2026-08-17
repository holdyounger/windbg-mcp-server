// Program.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WindbgMcp.DbgEng;
using WindbgMcp.Tools;

class Program
{
    static async Task Main(string[] args)
    {
        int port = args.Length > 0 ? int.Parse(args[0]) : 50000;
        
        DebuggerSession? session = null;
        try
        {
            session = new DebuggerSession(port);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            Console.Error.WriteLine("Usage: WindbgMcp.exe [port]");
            Console.Error.WriteLine("Make sure WinDbg is running with: .server tcp:port=<port>");
            Environment.Exit(1);
        }

        // ─── MCP stdio JSON-RPC 循环 ──────────────────────────────
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var reader = new StreamReader(stdin);
        var writer = new StreamWriter(stdout) { AutoFlush = true };

        Console.Error.WriteLine("[WindbgMcp] MCP server ready, waiting for requests on stdio...");

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(line);
                var response = await HandleRequest(request, session);
                if (response != null)
                    await writer.WriteLineAsync(response);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WindbgMcp] Error: {ex}");
            }
        }
    }

    static async Task<string?> HandleRequest(JsonElement request, DebuggerSession session)
    {
        JsonElement? id = request.TryGetProperty("id", out var idProp) ? idProp : (JsonElement?)null;
        var method = request.GetProperty("method").GetString();
        var @params = request.TryGetProperty("params", out var p) ? p : default;

        switch (method)
        {
            case "initialize":
                return JsonSerializer.Serialize(new {
                    jsonrpc = "2.0", id = id,
                    result = new {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "windbg-gui-bridge", version = "0.1.0" }
                    }
                });

            case "tools/list":
                return JsonSerializer.Serialize(new {
                    jsonrpc = "2.0", id = id,
                    result = new { tools = WindbgTools.GetToolDefinitions() }
                });

            case "tools/call":
                var toolName = @params.GetProperty("name").GetString()!;
                var arguments = @params.TryGetProperty("arguments", out var a) ? a : default;
                var result = await WindbgTools.CallTool(toolName, arguments, session);
                return JsonSerializer.Serialize(new {
                    jsonrpc = "2.0", id = id,
                    result = new { content = new[] { new { type = "text", text = result } } }
                });

            default:
                return JsonSerializer.Serialize(new {
                    jsonrpc = "2.0", id = id,
                    error = new { code = -32601, message = $"Method not found: {method}" }
                });
        }
    }
}