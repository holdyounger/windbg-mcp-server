// DbgEng/DebugClient.cs
using System;
using System.Runtime.InteropServices;

namespace WindbgMcp.DbgEng;

// ─── GUIDs ───────────────────────────────────────────────────────────

public static class Guids
{
    public static readonly Guid IDebugClient  = new("27fe5639-8407-4f3d-8393-33c5341e374b");
    public static readonly Guid IDebugControl = new("5182e668-105e-416e-ad92-24ef80426568");
    public static readonly Guid IDebugDataSpaces = new("88f7dfab-3ea7-4c3a-aefb-c4e8106173aa");
    public static readonly Guid IDebugRegisters  = new("ce289126-9e84-45a8-96e8-ea3d784b8b58");
    public static readonly Guid IDebugSymbols    = new("8c31e98c-983a-48c3-8c66-184b20117e5e");
}

// ─── DebugConnect P/Invoke ──────────────────────────────────────────

public static class NativeMethods
{
    [DllImport("dbgeng.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    public static extern int DebugConnect(
        string RemoteOptions,
        ref Guid InterfaceId,
        out IntPtr Interface);
}

// ─── IDebugClient ───────────────────────────────────────────────────

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("27fe5639-8407-4f3d-8393-33c5341e374b")]
public interface IDebugClient
{
    // 前 3 个是 IUnknown: QueryInterface, AddRef, Release
    int QueryInterface(ref Guid riid, out IntPtr ppv);
    int AddRef();
    int Release();
    
    // IDebugClient methods (index 3+)
    void AttachKernel(uint Flags, string Options);
    void GetKernelConnectionOptions(IntPtr Buffer, uint BufferSize, out uint OptionsSize);
    void SetKernelConnectionOptions(string Options);
    // ... 我们只用 DebugConnect 拿到的客户端，大部分方法不需要
    // 直接跳到 SetOutputCallbacks (method index ~27)
    void _Skip();
    void _Skip2();
    // 实际用 QueryInterface 拿 IDebugControl 就行，这个接口只用来设回调
}

// ─── IDebugControl ───────────────────────────────────────────────────

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5182e668-105e-416e-ad92-24ef80426568")]
public interface IDebugControl
{
    int GetInterrupt();
    int SetInterrupt(uint Flags);
    int GetInterruptTimeout(out uint Seconds);
    int SetInterruptTimeout(uint Seconds);
    int GetLogFile(out IntPtr Handle, IntPtr Buffer, uint BufferSize, out uint NameSize, out bool Append);
    int OpenLogFile(string File, bool Append);
    int CloseLogFile();
    int GetLogMask(out uint Mask);
    int SetLogMask(uint Mask);
    int GetOutputMask(out uint Mask);
    int SetOutputMask(uint Mask);
    int GetOtherOutputMask(out uint Mask);
    int SetOtherOutputMask(uint Mask);
    int GetOutputWidth(out uint Columns);
    int SetOutputWidth(uint Columns);
    int GetOutputLinePrefix(IntPtr Buffer, uint BufferSize, out uint PrefixSize);
    int SetOutputLinePrefix(string Prefix);
    int GetIdentity(IntPtr Buffer, uint BufferSize, out uint IdentitySize);
    int OutputIdentity(string Format, uint Flags);
    int GetTextReplacement(string Src, uint SrcSize, IntPtr DstBuffer, uint DstSize, out uint DstSizeNeeded);
    int SetTextReplacement(string Src, string Dst);
    int RemoveTextReplacements();
    int Output(int OutputControl, string Format);
    int OutputVaList(int OutputControl, string Format, IntPtr Args);
    
    // ─── 关键方法 ─────────────────────────────────────────────────
    int ControlledOutput(int OutputControl, uint Flags, string Format);
    int ControlledOutputVaList(int OutputControl, uint Flags, string Format, IntPtr Args);
    
    /// <summary>执行调试命令 —— 核心方法</summary>
    int Execute(int OutputControl, string Command, uint Flags);
    
    int ExecuteCommandFile(int OutputControl, string CommandFile, uint Flags);
    int GetNumberPossibleExecutions(out uint Number);
    int GetPossibleExecutionByIndex(uint Index, out IntPtr Buffer, uint BufferSize, out uint DescSize);
    int GetExecutionStatus(out uint Status);
    int SetExecutionStatus(uint Status);
    int GetCodeLevel(out uint Level);
    int SetCodeLevel(uint Level);
    int GetEngineOptions(out uint Options);
    int AddEngineOptions(uint Options);
    int RemoveEngineOptions(uint Options);
    int SetEngineOptions(uint Options);
    int GetSystemErrorControl(out uint OutputLevel, out uint BreakLevel);
    int SetSystemErrorControl(uint OutputLevel, uint BreakLevel);
    int GetTextMacro(uint Slot, IntPtr Buffer, uint BufferSize, out uint MacroSize);
    int SetTextMacro(uint Slot, string Macro);
    int GetRadix(out uint Radix);
    int SetRadix(uint Radix);
    int Evaluate(string Expression, uint DesiredType, out ulong Value, out uint RemainderIndex);
    int CoerceValue(ulong Value, uint InType, out ulong OutValue);
    int CoerceValues(uint Count, ulong[] Values, uint[] InTypes, out ulong OutValues, out uint OutTypes);
    int ExecuteCommandFileEx(int OutputControl, string CommandFile, uint Flags);
    int GetContext(IntPtr Context, uint ContextSize, out uint ContextSizeNeeded);
    int SetContext(IntPtr Context, uint ContextSize);
    int OutputCurrentState(int OutputControl, uint Flags);
    int OutputVersionInformation(int OutputControl);
    int GetNotifyEventCallback();
    int SetNotifyEventCallback();
    int GetInputCallbacks();
    int SetInputCallbacks();
    
    /// <summary>设置输出回调 —— 用来捕获命令结果</summary>
    int SetOutputCallbacks(IntPtr Callbacks);
    
    int GetOutputCallbacks(out IntPtr Callbacks);
    int GetOutputCallbacksWide(out IntPtr Callbacks);
    int SetOutputCallbacksWide(IntPtr Callbacks);
    // ... 后面还有几十个方法，但我们用不到
    // 实际通过 Marshal.GetComInterfaceForObject + vtable 偏移调用 Execute 和 SetOutputCallbacks 更靠谱
}

// ─── 常量 ───────────────────────────────────────────────────────────

public static class DbgConstants
{
    public const int DEBUG_OUTCTL_ALL_CLIENTS = 0;
    public const int DEBUG_OUTCTL_THIS_CLIENT = 1;
    public const uint DEBUG_EXECUTE_DEFAULT = 0;
    public const uint DEBUG_EXECUTE_ECHO = 1;
    public const uint DEBUG_EXECUTE_NOT_LOGGED = 2;
    public const uint DEBUG_EXECUTE_NO_REPEAT = 4;
    
    public const uint DEBUG_OUTPUT_NORMAL = 0x00000001;
    public const uint DEBUG_OUTPUT_ERROR = 0x00000002;
    public const uint DEBUG_OUTPUT_WARNING = 0x00000004;
    public const uint DEBUG_OUTPUT_VERBOSE = 0x00000008;
    public const uint DEBUG_OUTPUT_PROMPT = 0x00000010;
    public const uint DEBUG_OUTPUT_PROMPT_REG = 0x00000020;
    public const uint DEBUG_OUTPUT_EXTENSION = 0x00000040;
    public const uint DEBUG_OUTPUT_DEBUGGEE = 0x00000080;
    public const uint DEBUG_OUTPUT_DEBUGGEE_PROMPT = 0x00000100;
    public const uint DEBUG_OUTPUT_SYMBOLS = 0x00000200;
    
    public const uint DEBUG_STATUS_NO_DEBUGGEE = 0;
    public const uint DEBUG_STATUS_BREAK = 1;
    public const uint DEBUG_STATUS_GO = 2;
    public const uint DEBUG_STATUS_STEP_INTO = 3;
    public const uint DEBUG_STATUS_STEP_OVER = 4;
    public const uint DEBUG_STATUS_STEP_OUT = 5;
    public const uint DEBUG_STATUS_DEAD = 6;
}