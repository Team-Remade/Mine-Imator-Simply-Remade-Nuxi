using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GMBinaryWrapper;

public enum IPCCommand : byte
{
    // File open/close
    OpenFileWrite   = 1,
    OpenFileRead    = 2,
    CloseFile       = 3,

    // Write commands (value in DataDouble field, except WriteBuffer/WriteString which use extra payload)
    WriteByte       = 10,
    WriteShort      = 11,
    WriteInt        = 12,
    WriteDouble     = 13,
    WriteBuffer     = 14,
    WriteString     = 15,

    // Read commands (return value in ResponseDouble field)
    ReadByte        = 20,
    ReadShort       = 21,
    ReadShortBE     = 22,
    ReadInt         = 23,
    ReadIntBE       = 24,
    ReadDouble      = 25,
    ReadBuffer      = 26,  // count in BufferSize; returns bytes as extra payload
    ReadString      = 27,  // reads a GM-style string (4-byte length prefix + bytes)
    ReadStringShort = 28,  // reads a GM-style short string (2-byte length prefix + bytes)

    Response        = 100
}

// Fixed-size header written to the MMF for every message.
// Total size: 1 + 8 + 8 + 4 + 8 + 1 = 30 bytes
public struct IPCMessage
{
    public IPCCommand Command;   // 1 byte
    public long Handle;          // 8 bytes  — raw bits of the double file handle
    public long DataDouble;      // 8 bytes  — raw bits of a double value (write commands)
    public int BufferSize;       // 4 bytes  — length of trailing payload / ReadBuffer count
    public long ResponseDouble;  // 8 bytes  — raw bits of the double return value (read commands)
    public bool Success;         // 1 byte

    public void Read(BinaryReader reader)
    {
        Command        = (IPCCommand)reader.ReadByte();
        Handle         = reader.ReadInt64();
        DataDouble     = reader.ReadInt64();
        BufferSize     = reader.ReadInt32();
        ResponseDouble = reader.ReadInt64();
        Success        = reader.ReadBoolean();
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Command);
        writer.Write(Handle);
        writer.Write(DataDouble);
        writer.Write(BufferSize);
        writer.Write(ResponseDouble);
        writer.Write(Success);
        writer.Flush();
    }
}

public class IPCServer : IDisposable
{
    private const string RequestEventName  = "GMBinaryWrapper_RequestReady";
    private const string ResponseEventName = "GMBinaryWrapper_ResponseReady";
    private const string MMFName           = "GMBinaryWrapper_MMF";
    private const int    MMFSize           = 65536;

    private EventWaitHandle      _requestReady;
    private EventWaitHandle      _responseReady;
    private MemoryMappedFile     _mmf;
    private MemoryMappedViewStream _stream;
    private BinaryWriter         _writer;
    private BinaryReader         _reader;
    private bool                 _disposed;

    // All parameters and return values are 64-bit doubles (GameMaker extension convention).
    //
    // GMBINOpenFileWrite/Read epilogue: 'mov eax,[ebp-4]; mov edx,0; push edx; push eax;
    // fistp qword [esp]; lea esp,[esp+8]; leave; ret'. The handle is in eax:edx at ret time.
    // Declaring as 'long' correctly captures eax:edx. The fistp side-effect pops ST0 — we
    // must NOT declare as 'double' or P/Invoke reads the emptied x87 register (QNaN corruption).
    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern long GMBINOpenFileWrite(string filename);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern long GMBINOpenFileRead(string filename);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINCloseFile(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINWriteByte(double file, double value);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINWriteShort(double file, double value);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINWriteInt(double file, double value);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINWriteDouble(double file, double value);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadByte(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadShort(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadShortBE(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadInt(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadIntBE(double file);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern double GMBINReadDouble(double file);

    // GMBINWriteBuffer and GMBINReadBuffer: arg1=double file at [ebp+8], arg2=struct* buf at [ebp+0x10].
    // [ebp+0xC] is the gap between the double (8 bytes) and the next arg, which is the buffer pointer.
    // Declare with (double file, IntPtr bufPtr) — bufPtr lands at [ebp+0x10] correctly.
    // *(bufPtr-4) = int32 length; bufPtr[0..len-1] = data bytes.
    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINWriteBuffer(double file, IntPtr bufPtr);

    [DllImport("gmbinaryfile.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GMBINReadBuffer(double file, IntPtr bufPtr);

    // GMBINWriteString takes (double file, char* str, struct* str) — same struct layout.
    // Implemented using GMBINWriteBuffer (length prefix + data) for simplicity.

    /// <summary>
    /// Calls GMBINWriteBuffer with a stack-allocated GM buffer struct.
    /// Layout: [int32 length][data bytes]. The DLL receives a pointer to the data portion.
    /// Using stackalloc avoids heap interaction with the DLL's own allocator.
    /// </summary>
    private static unsafe void CallGMBINWriteBuffer(double file, byte[] data)
    {
        int len = data.Length;
        byte* block = stackalloc byte[4 + len];
        // Write length prefix at block[0..3]
        *(int*)block = len;
        // Write data at block[4..]
        for (int i = 0; i < len; i++) block[4 + i] = data[i];
        // Pass block+4 to DLL: *(ptr-4) == len, ptr[i] == data[i]
        GMBINWriteBuffer(file, new IntPtr(block + 4));
    }

    /// <summary>
    /// Calls GMBINReadBuffer with a stack-allocated GM buffer struct and copies result out.
    /// </summary>
    private static unsafe void CallGMBINReadBuffer(double file, byte[] dest)
    {
        int len = dest.Length;
        byte* block = stackalloc byte[4 + len];
        *(int*)block = len;
        GMBINReadBuffer(file, new IntPtr(block + 4));
        for (int i = 0; i < len; i++) dest[i] = block[4 + i];
    }

    public void Start()
    {
        _requestReady  = new EventWaitHandle(false, EventResetMode.AutoReset, RequestEventName);
        _responseReady = new EventWaitHandle(false, EventResetMode.AutoReset, ResponseEventName);

        _mmf    = MemoryMappedFile.CreateNew(MMFName, MMFSize, MemoryMappedFileAccess.ReadWrite);
        _stream = _mmf.CreateViewStream(0, MMFSize, MemoryMappedFileAccess.ReadWrite);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
        _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

        Console.WriteLine("GMBinaryWrapper: IPC server started");
    }

    public void Run()
    {
        while (!_disposed)
        {
            if (!_requestReady.WaitOne(500)) continue;

            _stream.Seek(0, SeekOrigin.Begin);
            var request = new IPCMessage();
            request.Read(_reader);

            // Read trailing payload for commands that carry extra data
            byte[]? extraBuffer = null;
            bool hasPayload = request.BufferSize > 0 && (
                request.Command == IPCCommand.OpenFileWrite  ||
                request.Command == IPCCommand.OpenFileRead   ||
                request.Command == IPCCommand.WriteBuffer    ||
                request.Command == IPCCommand.WriteString);
            if (hasPayload)
                extraBuffer = _reader.ReadBytes(request.BufferSize);

            var response = new IPCMessage { Command = IPCCommand.Response };

            // For non-open commands the client echoes back the handle it received from Open.
            // Cast request.Handle (a plain integer) to double — the GM DLL expects double args.
            double fh = (double)request.Handle;

            try
            {
                switch (request.Command)
                {
                    // ── Open / Close ────────────────────────────────────────────────
                    case IPCCommand.OpenFileWrite:
                    {
                        if (extraBuffer == null || extraBuffer.Length == 0) { response.Success = false; break; }
                        string fn = Encoding.UTF8.GetString(extraBuffer);
                        long handleInt = GMBINOpenFileWrite(fn);
                        response.Handle  = handleInt; // plain integer, client echoes back as-is
                        response.Success = handleInt != 0;
                        break;
                    }
                    case IPCCommand.OpenFileRead:
                    {
                        if (extraBuffer == null || extraBuffer.Length == 0) { response.Success = false; break; }
                        string fn = Encoding.UTF8.GetString(extraBuffer);
                        long handleInt = GMBINOpenFileRead(fn);
                        response.Handle  = handleInt;
                        response.Success = handleInt != 0;
                        break;
                    }
                    case IPCCommand.CloseFile:
                        if (fh != 0.0) GMBINCloseFile(fh);
                        response.Success = true;
                        break;

                    // ── Write ────────────────────────────────────────────────────────
                    case IPCCommand.WriteByte:
                        if (fh == 0.0) { response.Success = false; break; }
                        GMBINWriteByte(fh, BitConverter.Int64BitsToDouble(request.DataDouble));
                        response.Success = true;
                        break;

                    case IPCCommand.WriteShort:
                        if (fh == 0.0) { response.Success = false; break; }
                        GMBINWriteShort(fh, BitConverter.Int64BitsToDouble(request.DataDouble));
                        response.Success = true;
                        break;

                    case IPCCommand.WriteInt:
                        if (fh == 0.0) { response.Success = false; break; }
                        GMBINWriteInt(fh, BitConverter.Int64BitsToDouble(request.DataDouble));
                        response.Success = true;
                        break;

                    case IPCCommand.WriteDouble:
                        if (fh == 0.0) { response.Success = false; break; }
                        GMBINWriteDouble(fh, BitConverter.Int64BitsToDouble(request.DataDouble));
                        response.Success = true;
                        break;

                    case IPCCommand.WriteBuffer:
                    {
                        if (fh == 0.0 || extraBuffer == null) { response.Success = false; break; }
                        CallGMBINWriteBuffer(fh, extraBuffer);
                        response.Success = true;
                        break;
                    }

                    case IPCCommand.WriteString:
                    {
                        // GM string format: 4-byte LE int length prefix then the string bytes.
                        if (fh == 0.0 || extraBuffer == null) { response.Success = false; break; }
                        CallGMBINWriteBuffer(fh, BitConverter.GetBytes(extraBuffer.Length));
                        CallGMBINWriteBuffer(fh, extraBuffer);
                        response.Success = true;
                        break;
                    }

                    // ── Read ─────────────────────────────────────────────────────────
                    case IPCCommand.ReadByte:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadByte(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadShort:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadShort(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadShortBE:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadShortBE(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadInt:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadInt(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadIntBE:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadIntBE(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadDouble:
                        if (fh == 0.0) { response.Success = false; break; }
                        response.ResponseDouble = BitConverter.DoubleToInt64Bits(GMBINReadDouble(fh));
                        response.Success = true;
                        break;

                    case IPCCommand.ReadBuffer:
                    {
                        if (fh == 0.0 || request.BufferSize <= 0) { response.Success = false; break; }
                        byte[] buf = new byte[request.BufferSize];
                        CallGMBINReadBuffer(fh, buf);
                        response.BufferSize = request.BufferSize;
                        response.Success    = true;
                        _stream.Seek(0, SeekOrigin.Begin);
                        response.Write(_writer);
                        _writer.Write(buf, 0, buf.Length);
                        _writer.Flush();
                        _responseReady.Set();
                        continue;
                    }

                    case IPCCommand.ReadString:
                    {
                        // GM string format: 4-byte LE int length then that many chars.
                        if (fh == 0.0) { response.Success = false; break; }
                        byte[] lenBuf = new byte[4];
                        CallGMBINReadBuffer(fh, lenBuf);
                        int len = BitConverter.ToInt32(lenBuf, 0);
                        byte[] strBytes = new byte[len];
                        if (len > 0) CallGMBINReadBuffer(fh, strBytes);
                        response.BufferSize = len;
                        response.Success    = true;
                        _stream.Seek(0, SeekOrigin.Begin);
                        response.Write(_writer);
                        _writer.Write(strBytes, 0, strBytes.Length);
                        _writer.Flush();
                        _responseReady.Set();
                        continue;
                    }

                    case IPCCommand.ReadStringShort:
                    {
                        // Short string format: 2-byte LE short length then that many chars
                        if (fh == 0.0) { response.Success = false; break; }
                        byte[] lenBuf = new byte[2];
                        CallGMBINReadBuffer(fh, lenBuf);
                        short len = BitConverter.ToInt16(lenBuf, 0);
                        byte[] strBytes = new byte[len];
                        if (len > 0) CallGMBINReadBuffer(fh, strBytes);
                        response.BufferSize = len;
                        response.Success    = true;
                        _stream.Seek(0, SeekOrigin.Begin);
                        response.Write(_writer);
                        _writer.Write(strBytes, 0, strBytes.Length);
                        _writer.Flush();
                        _responseReady.Set();
                        continue;
                    }

                    default:
                        response.Success = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                Console.WriteLine($"GMBinaryWrapper Error: {ex.Message}");
            }

            _stream.Seek(0, SeekOrigin.Begin);
            response.Write(_writer);
            _responseReady.Set();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _mmf?.Dispose();
        _requestReady?.Dispose();
        _responseReady?.Dispose();
    }
}

public class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("GMBinaryWrapper: Loading gmbinaryfile.dll...");
        using var server = new IPCServer();
        server.Start();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Dispose();
        };

        Console.WriteLine("GMBinaryWrapper: Running. Press Ctrl+C to stop.");
        server.Run();
    }
}
