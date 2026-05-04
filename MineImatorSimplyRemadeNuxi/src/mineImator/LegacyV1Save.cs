using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MineImatorSimplyRemadeNuxi.core.dialogBox;

namespace MineImatorSimplyRemadeNuxi.mineImator;

public enum IPCCommand : byte
{
    // File open/close
    OpenFileWrite   = 1,
    OpenFileRead    = 2,
    CloseFile       = 3,

    // Write commands
    WriteByte       = 10,
    WriteShort      = 11,
    WriteInt        = 12,
    WriteDouble     = 13,
    WriteBuffer     = 14,
    WriteString     = 15,

    // Read commands
    ReadByte        = 20,
    ReadShort       = 21,
    ReadShortBE     = 22,
    ReadInt         = 23,
    ReadIntBE       = 24,
    ReadDouble      = 25,
    ReadBuffer      = 26,
    ReadString      = 27,  // reads 4-byte LE length prefix then chars
    ReadStringShort = 28,  // reads 2-byte LE length prefix then chars

    Response        = 100
}

// Fixed-size header: 1 + 8 + 8 + 4 + 8 + 1 = 30 bytes
public struct IPCMessage
{
    public IPCCommand Command;
    public long Handle;          // raw bits of double file handle
    public long DataDouble;      // raw bits of double value for write commands
    public int BufferSize;       // trailing payload length, or ReadBuffer count
    public long ResponseDouble;  // raw bits of double return value for read commands
    public bool Success;

    public static IPCMessage Read(BinaryReader reader)
    {
        var m = new IPCMessage();
        m.Command        = (IPCCommand)reader.ReadByte();
        m.Handle         = reader.ReadInt64();
        m.DataDouble     = reader.ReadInt64();
        m.BufferSize     = reader.ReadInt32();
        m.ResponseDouble = reader.ReadInt64();
        m.Success        = reader.ReadBoolean();
        return m;
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

public class LegacyV1Save : IDisposable
{
    private const string RequestEventName  = "GMBinaryWrapper_RequestReady";
    private const string ResponseEventName = "GMBinaryWrapper_ResponseReady";
    private const string MMFName           = "GMBinaryWrapper_MMF";
    private const int    MMFSize           = 65536;
    private const int    ResponseTimeoutMs = 5000;

    private EventWaitHandle?       _requestReady;
    private EventWaitHandle?       _responseReady;
    private MemoryMappedFile?      _mmf;
    private MemoryMappedViewStream? _stream;
    private BinaryWriter?          _writer;
    private BinaryReader?          _reader;
    private readonly bool          _isWindows;
    private bool                   _disposed;
    private Process?               _wrapperProcess;

    public static bool IsWrapperAvailable { get; private set; }
    public bool IsWindows => _isWindows;

    static LegacyV1Save()
    {
        IsWrapperAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    private bool TryConnect()
    {
        try
        {
            _requestReady  = EventWaitHandle.OpenExisting(RequestEventName);
            _responseReady = EventWaitHandle.OpenExisting(ResponseEventName);
            _mmf    = MemoryMappedFile.OpenExisting(MMFName, MemoryMappedFileRights.ReadWrite);
            _stream = _mmf.CreateViewStream(0, MMFSize, MemoryMappedFileAccess.ReadWrite);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            _reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
            return true;
        }
        catch
        {
            DisposeIPC();
            return false;
        }
    }

    private void DisposeIPC()
    {
        _reader?.Dispose();        _reader        = null;
        _writer?.Dispose();        _writer        = null;
        _stream?.Dispose();        _stream        = null;
        _mmf?.Dispose();           _mmf           = null;
        _requestReady?.Dispose();  _requestReady  = null;
        _responseReady?.Dispose(); _responseReady = null;
    }

    private bool StartWrapper()
    {
        string wrapperPath = Path.Combine(AppContext.BaseDirectory, "GMBinaryWrapper", "GMBinaryWrapper.exe");
        if (!File.Exists(wrapperPath))
            wrapperPath = Path.Combine(AppContext.BaseDirectory, "GMBinaryWrapper.exe");
        if (!File.Exists(wrapperPath))
            wrapperPath = Path.Combine(Directory.GetCurrentDirectory(), "GMBinaryWrapper", "GMBinaryWrapper.exe");
        if (!File.Exists(wrapperPath))
            wrapperPath = Path.Combine(Directory.GetCurrentDirectory(), "GMBinaryWrapper.exe");

        if (!File.Exists(wrapperPath))
            return false;

        try
        {
            var psi = new ProcessStartInfo(wrapperPath)
            {
                UseShellExecute  = false,
                CreateNoWindow   = true
            };
            _wrapperProcess = Process.Start(psi);
            if (_wrapperProcess == null) return false;

            for (int i = 0; i < 50; i++)
            {
                Thread.Sleep(50);
                if (TryConnect()) return true;
            }
        }
        catch
        {
            return false;
        }

        _wrapperProcess?.Kill();
        _wrapperProcess?.Dispose();
        _wrapperProcess = null;
        return false;
    }

    public LegacyV1Save()
    {
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (!_isWindows)
        {
            Program.App.MessageBox =
                new DialogBox("OS Invalid for Operation", "The Legacy Save format only works on Windows");
            return;
        }
        
        if (!TryConnect())
            StartWrapper();
    }

    private void EnsureConnected()
    {
        if (_writer == null || _reader == null || _stream == null)
            throw new InvalidOperationException("GMBinaryWrapper is not running.");
    }

    /// <summary>
    /// Sends a request and returns the response header.
    /// For commands that return a payload (ReadBuffer, ReadString, ReadStringShort)
    /// use SendRequestWithPayload instead.
    /// </summary>
    private IPCMessage SendRequest(IPCMessage request, byte[]? extraData = null)
    {
        EnsureConnected();

        _stream!.Seek(0, SeekOrigin.Begin);
        request.Write(_writer!);
        if (extraData != null)
        {
            _writer!.Write(extraData, 0, extraData.Length);
            _writer!.Flush();
        }

        _requestReady!.Set();

        if (!_responseReady!.WaitOne(ResponseTimeoutMs))
            throw new TimeoutException("GMBinaryWrapper did not respond in time.");

        _stream.Seek(0, SeekOrigin.Begin);
        return IPCMessage.Read(_reader!);
    }

    /// <summary>
    /// Sends a request and reads back both the response header and a trailing byte payload.
    /// Used for ReadBuffer, ReadString, ReadStringShort.
    /// </summary>
    private (IPCMessage response, byte[] payload) SendRequestWithPayload(IPCMessage request)
    {
        EnsureConnected();

        _stream!.Seek(0, SeekOrigin.Begin);
        request.Write(_writer!);
        _requestReady!.Set();

        if (!_responseReady!.WaitOne(ResponseTimeoutMs))
            throw new TimeoutException("GMBinaryWrapper did not respond in time.");

        _stream.Seek(0, SeekOrigin.Begin);
        var response = IPCMessage.Read(_reader!);
        byte[] payload = response.BufferSize > 0
            ? _reader!.ReadBytes(response.BufferSize)
            : Array.Empty<byte>();
        return (response, payload);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    // GM DLL functions take all value arguments as doubles and return doubles via st0.
    // File handles are returned as plain integers (via fistp), stored as long, and passed
    // back to DLL functions as (double)handle. Value arguments like byte/int are passed
    // as their double representation: (double)value. ResponseDouble is also a double via st0.

    // Encode a numeric value for the DataDouble wire field (client → server → DLL arg)
    private static long D(double v) => BitConverter.DoubleToInt64Bits(v);
    // Decode a numeric value from the ResponseDouble wire field (DLL return → server → client)
    private static double F(long  v) => BitConverter.Int64BitsToDouble(v);

    // ── File open / close ────────────────────────────────────────────────────────

    /// <summary>File handle is the raw bit-pattern of a GM double. Use with all other methods.</summary>
    public long OpenFileWrite(string filename)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        EnsureConnected();

        byte[] fnBytes = Encoding.UTF8.GetBytes(filename);
        var response = SendRequest(new IPCMessage
        {
            Command    = IPCCommand.OpenFileWrite,
            BufferSize = fnBytes.Length
        }, fnBytes);

        if (!response.Success)
            throw new IOException($"GMBinaryWrapper: OpenFileWrite failed for '{filename}'.");
        return response.Handle;
    }

    public long OpenFileRead(string filename)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        EnsureConnected();

        byte[] fnBytes = Encoding.UTF8.GetBytes(filename);
        var response = SendRequest(new IPCMessage
        {
            Command    = IPCCommand.OpenFileRead,
            BufferSize = fnBytes.Length
        }, fnBytes);

        if (!response.Success)
            throw new IOException($"GMBinaryWrapper: OpenFileRead failed for '{filename}'.");
        return response.Handle;
    }

    public void CloseFile(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        SendRequest(new IPCMessage { Command = IPCCommand.CloseFile, Handle = file });
    }

    // ── Write ─────────────────────────────────────────────────────────────────────

    public void WriteByte(long file, byte value)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.WriteByte, Handle = file, DataDouble = D(value) });
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteByte failed.");
    }

    public void WriteShort(long file, short value)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.WriteShort, Handle = file, DataDouble = D(value) });
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteShort failed.");
    }

    public void WriteInt(long file, int value)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.WriteInt, Handle = file, DataDouble = D(value) });
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteInt failed.");
    }

    public void WriteDouble(long file, double value)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.WriteDouble, Handle = file, DataDouble = D(value) });
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteDouble failed.");
    }

    public void WriteBuffer(long file, byte[] buffer, int count)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage
        {
            Command    = IPCCommand.WriteBuffer,
            Handle     = file,
            BufferSize = count
        }, buffer[..count]);
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteBuffer failed.");
    }

    /// <summary>
    /// Writes a GM-style string: 4-byte LE int length prefix followed by the UTF-8 bytes.
    /// </summary>
    public void WriteString(long file, string value)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        byte[] strBytes = Encoding.UTF8.GetBytes(value);
        var r = SendRequest(new IPCMessage
        {
            Command    = IPCCommand.WriteString,
            Handle     = file,
            BufferSize = strBytes.Length
        }, strBytes);
        if (!r.Success) throw new IOException("GMBinaryWrapper: WriteString failed.");
    }

    // ── Read ──────────────────────────────────────────────────────────────────────

    public byte ReadByte(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadByte, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadByte failed.");
        return (byte)(int)F(r.ResponseDouble);
    }

    public short ReadShort(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadShort, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadShort failed.");
        return (short)(int)F(r.ResponseDouble);
    }

    public short ReadShortBE(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadShortBE, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadShortBE failed.");
        return (short)(int)F(r.ResponseDouble);
    }

    public int ReadInt(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadInt, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadInt failed.");
        return (int)F(r.ResponseDouble);
    }

    public int ReadIntBE(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadIntBE, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadIntBE failed.");
        return (int)F(r.ResponseDouble);
    }

    public double ReadDouble(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var r = SendRequest(new IPCMessage { Command = IPCCommand.ReadDouble, Handle = file });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadDouble failed.");
        return F(r.ResponseDouble);
    }

    public byte[] ReadBuffer(long file, int count)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var (r, payload) = SendRequestWithPayload(new IPCMessage
        {
            Command    = IPCCommand.ReadBuffer,
            Handle     = file,
            BufferSize = count
        });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadBuffer failed.");
        return payload;
    }

    /// <summary>
    /// Reads a GM-style string written by GMBINWriteString:
    /// 4-byte LE int length prefix followed by that many bytes (no null terminator).
    /// </summary>
    public string ReadString(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var (r, payload) = SendRequestWithPayload(new IPCMessage
        {
            Command = IPCCommand.ReadString,
            Handle  = file
        });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadString failed.");
        return Encoding.UTF8.GetString(payload);
    }

    /// <summary>
    /// Reads a short string: 2-byte LE short length prefix followed by that many bytes.
    /// </summary>
    public string ReadStringShort(long file)
    {
        if (!_isWindows) throw new PlatformNotSupportedException();
        var (r, payload) = SendRequestWithPayload(new IPCMessage
        {
            Command = IPCCommand.ReadStringShort,
            Handle  = file
        });
        if (!r.Success) throw new IOException("GMBinaryWrapper: ReadStringShort failed.");
        return Encoding.UTF8.GetString(payload);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeIPC();
        _wrapperProcess?.Kill();
        _wrapperProcess?.Dispose();
        _wrapperProcess = null;
    }
}
