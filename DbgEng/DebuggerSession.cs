// DbgEng/DebuggerSession.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WindbgMcp.DbgEng;

/// <summary>
/// 封装 WinDbg DbgEng 会话 —— 通过 DebugConnect 连到 .server 实例
/// </summary>
public unsafe class DebuggerSession : IDisposable
{
    private readonly IntPtr _clientPtr;
    private readonly IntPtr _controlPtr;
    private readonly OutputCallbackImpl _outputCallback;
    private readonly IntPtr _outputCallbackPtr;
    
    // vtable 偏移（IUnknown=3, IDebugControl 方法序号）
    private delegate int ExecuteDelegate(IntPtr thisPtr, int outputControl, [MarshalAs(UnmanagedType.LPStr)] string command, uint flags);
    private delegate int SetOutputCallbacksDelegate(IntPtr thisPtr, IntPtr callbacks);
    private delegate int GetExecutionStatusDelegate(IntPtr thisPtr, out uint status);
    
    private readonly ExecuteDelegate _execute;
    private readonly SetOutputCallbacksDelegate _setOutputCallbacks;
    private readonly GetExecutionStatusDelegate _getExecutionStatus;

    public DebuggerSession(int port = 50000)
    {
        // ── 1. DebugConnect ──────────────────────────────────────
        string remote = $"tcp:server=127.0.0.1,port={port}";
        var iid = Guids.IDebugClient;
        int hr = NativeMethods.DebugConnect(remote, ref iid, out _clientPtr);
        if (hr != 0)
            throw new InvalidOperationException(
                $"DebugConnect failed (0x{hr:X8}). 确认 WinDbg 里执行了 '.server tcp:port={port}' 且 dbgeng.dll 版本匹配。");
        
        Console.Error.WriteLine($"[WindbgMcp] Connected to WinDbg .server on port {port}");
        
        // ── 2. QueryInterface for IDebugControl ─────────────────
        var iidControl = Guids.IDebugControl;
        Marshal.QueryInterface(_clientPtr, ref iidControl, out _controlPtr);
        
        // ── 3. 解析 vtable 中我们需要的方法 ──────────────────────
        // vtable 索引 = 3 个 IUnknown 方法(0/1/2) + 接口内方法序号。
        // IDebugControl（10.0 SDK）实际索引：
        //   Execute            = 29
        //   GetExecutionStatus = 33
        //   SetOutputCallbacks = 60
        // 注意：不能写成接口内方法序号（17/30/46），必须加上 IUnknown 的前 3 个槽位。
        IntPtr* vtable = *(IntPtr**)_controlPtr;
        
        _execute = Marshal.GetDelegateForFunctionPointer<ExecuteDelegate>(vtable[29]);
        _getExecutionStatus = Marshal.GetDelegateForFunctionPointer<GetExecutionStatusDelegate>(vtable[33]);
        _setOutputCallbacks = Marshal.GetDelegateForFunctionPointer<SetOutputCallbacksDelegate>(vtable[60]);
        
        // ── 4. 注册输出回调 ──────────────────────────────────────
        _outputCallback = new OutputCallbackImpl();
        _outputCallbackPtr = Marshal.GetComInterfaceForObject<OutputCallbackImpl, IDebugOutputCallbacks>(_outputCallback);
        _setOutputCallbacks(_controlPtr, _outputCallbackPtr);
    }

    /// <summary>
    /// 在 WinDbg 中执行命令，返回输出文本
    /// </summary>
    public string ExecuteCommand(string command, int timeoutMs = 15000)
    {
        _outputCallback.Clear();
        
        int hr = _execute(_controlPtr, DbgConstants.DEBUG_OUTCTL_ALL_CLIENTS, command, DbgConstants.DEBUG_EXECUTE_DEFAULT);
        if (hr != 0)
            return $"ERROR: Execute returned 0x{hr:X8}";
        
        // 等待输出刷新（简单轮询，实际可以更精细）
        int waited = 0;
        while (waited < timeoutMs)
        {
            Thread.Sleep(100);
            waited += 100;
            if (_outputCallback.HasOutput) break;
        }
        
        return _outputCallback.GetOutput();
    }

    public uint GetExecutionStatus()
    {
        _getExecutionStatus(_controlPtr, out uint status);
        return status;
    }

    public void Dispose()
    {
        if (_controlPtr != IntPtr.Zero)
            Marshal.Release(_controlPtr);
        if (_clientPtr != IntPtr.Zero)
            Marshal.Release(_clientPtr);
    }
}