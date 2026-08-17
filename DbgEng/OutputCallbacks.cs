// DbgEng/OutputCallbacks.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WindbgMcp.DbgEng;

/// <summary>
/// 实现 IDebugOutputCallbacks COM 接口，捕获 WinDbg 命令输出
/// IDebugOutputCallbacks: IUnknown(3) + Output(uint Mask, string Text) = 4 methods
/// </summary>
[ComVisible(true)]
[Guid("4bf58061-3324-469b-8be1-1642acc6e238")]
public class OutputCallbackImpl : IDebugOutputCallbacks
{
    private readonly StringBuilder _sb = new();
    private readonly object _lock = new();
    
    public bool HasOutput => _sb.Length > 0;

    public void Clear() { lock (_lock) _sb.Clear(); }
    
    public string GetOutput()
    {
        lock (_lock) return _sb.ToString();
    }

    // ─── IUnknown ────────────────────────────────────────────────
    public int QueryInterface(ref Guid riid, out IntPtr ppv)
    {
        ppv = Marshal.GetIUnknownForObject(this);
        return 0; // S_OK
    }
    
    public int AddRef() => 1;
    public int Release() => 1;

    // ─── IDebugOutputCallbacks.Output ────────────────────────────
    public int Output(uint Mask, string Text)
    {
        if (!string.IsNullOrEmpty(Text))
        {
            lock (_lock)
            {
                _sb.Append(Text);
            }
        }
        return 0; // S_OK
    }
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4bf58061-3324-469b-8be1-1642acc6e238")]
public interface IDebugOutputCallbacks
{
    int QueryInterface(ref Guid riid, out IntPtr ppv);
    int AddRef();
    int Release();
    int Output(uint Mask, string Text);
}