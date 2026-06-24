using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using SonyXm5.Core;

// Resident helper: keeps one Bluetooth control connection warm and switches the headphones'
// Ambient Sound Control instantly on a configurable hotkey (toggle or hold). Also supports a
// one-shot mode:  sony-ambient-daemon --once [toggle|amb|nc|off|wind]
static class Program
{
    static readonly string Dir = AppContext.BaseDirectory;
    static readonly string CfgFile = Path.Combine(Dir, "config.json");
    static readonly string StateFile = Path.Combine(Dir, "state.txt");
    static readonly string LogFile = Path.Combine(Dir, "daemon.log");

    static AppConfig _cfg = new();
    static readonly object _io = new();
    static readonly System.Threading.SemaphoreSlim _writeLock = new(1, 1);
    static StreamSocket _sock; static DataWriter _w;
    static int _txSeq;               // sequence bit for our next command (kept in sync with received ACKs)
    static int _lastRx = -2;         // last ambient state the headphones reported (for logging)
    static string _curMode;          // last mode set (for toggle alternation)
    static bool _triggerActive;      // hotkey key currently held
    static bool _needCtrl, _needAlt, _needShift, _needWin; static uint _triggerVk;
    static int _busy;

    static void Log(string s) { try { File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss.fff}  {s}{Environment.NewLine}"); } catch { } }

    // ---- Bluetooth ----
    static async Task<bool> ConnectAsync()
    {
        try
        {
            var s = await SonyDevice.ConnectAsync();
            if (s == null) { Log("no headphones found (paired & connected?)"); return false; }
            lock (_io) { _sock = s; _w = new DataWriter(s.OutputStream); _txSeq = 0; _lastRx = -2; }
            _ = ReadLoopAsync(s);
            Log("connected (warm)");
            return true;
        }
        catch (Exception ex) { Log("connect failed: " + ex.Message); return false; }
    }

    static async Task ReadLoopAsync(StreamSocket s)
    {
        var r = new DataReader(s.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        var fr = new FrameReader();
        try
        {
            while (true)
            {
                uint got = await r.LoadAsync(256);
                if (got == 0) break;
                var buf = new byte[got]; r.ReadBytes(buf);
                foreach (var pkt in fr.Feed(buf))
                {
                    if (pkt.Type == Protocol.Ack) { Interlocked.Exchange(ref _txSeq, pkt.Seq); continue; }

                    // ACK every message the headphones send, or the link stalls after a few exchanges.
                    await WriteFrameAsync(Protocol.Frame(Protocol.Ack, (byte)(pkt.Seq ^ 1), System.Array.Empty<byte>()));

                    int flag = Protocol.AmbientFlag(pkt.Payload);
                    if (flag >= 0)
                    {
                        if (_curMode == null) _curMode = flag == 1 ? "amb" : "nc"; // initialise toggle direction once
                        if (flag != _lastRx) { _lastRx = flag; Log($"rx state: {(flag == 1 ? "amb" : "nc")}"); }
                    }
                }
            }
        }
        catch { }
        lock (_io) { if (_sock == s) { _sock = null; _w = null; } }
        Log("read loop ended (disconnected)");
    }

    // Serialize all writes (commands AND the ACKs sent from the read loop) on one socket.
    static async Task<bool> WriteFrameAsync(byte[] frame)
    {
        DataWriter w; lock (_io) w = _w;
        if (w == null) return false;
        await _writeLock.WaitAsync();
        try { w.WriteBytes(frame); await w.StoreAsync(); return true; }
        catch (Exception ex) { Log("write failed: " + ex.Message); lock (_io) { _sock = null; _w = null; } return false; }
        finally { _writeLock.Release(); }
    }

    static async Task<bool> SendAsync(string mode)
    {
        int seq; lock (_io) seq = _txSeq;
        var frame = Protocol.Frame(Protocol.DataMdr, (byte)seq, Protocol.AmbientSoundControl(mode, _cfg.ambientLevel));
        bool ok = await WriteFrameAsync(frame);
        if (ok) Interlocked.Exchange(ref _txSeq, seq ^ 1);   // flip optimistically; corrected by received ACKs
        return ok;
    }

    static async Task ApplyAsync(string mode)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool warm; lock (_io) warm = _sock != null;
            if (!warm) await ConnectAsync();
            if (!await SendAsync(mode)) { if (await ConnectAsync()) await SendAsync(mode); }
            _curMode = mode;
            try { File.WriteAllText(StateFile, mode); } catch { }
            Log($"-> {mode} in {sw.ElapsedMilliseconds} ms (warm={warm})");
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    // ---- hotkey ----
    static void ParseHotkey(string spec)
    {
        _needCtrl = _needAlt = _needShift = _needWin = false; _triggerVk = 0;
        foreach (var raw in (spec ?? "").Split('+'))
        {
            var t = raw.Trim().ToUpperInvariant();
            if (t.Length == 0) continue;
            if (t is "CTRL" or "CONTROL") _needCtrl = true;
            else if (t == "ALT") _needAlt = true;
            else if (t == "SHIFT") _needShift = true;
            else if (t is "WIN" or "WINDOWS" or "META") _needWin = true;
            else _triggerVk = KeyToVk(t);
        }
    }
    static uint KeyToVk(string k)
    {
        if (k.Length == 1) { char c = k[0]; if (c >= 'A' && c <= 'Z') return c; if (c >= '0' && c <= '9') return c; }
        if (k.Length >= 2 && k[0] == 'F' && int.TryParse(k.Substring(1), out int fn) && fn is >= 1 and <= 24) return (uint)(0x70 + fn - 1);
        return k switch
        {
            "SPACE" => 0x20, "ENTER" or "RETURN" => 0x0D, "TAB" => 0x09, "ESC" or "ESCAPE" => 0x1B,
            "INSERT" or "INS" => 0x2D, "DELETE" or "DEL" => 0x2E, "HOME" => 0x24, "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21, "PAGEDOWN" or "PGDN" => 0x22,
            "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
            _ => 0
        };
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    static bool ModsHeld()
    {
        bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        bool win = ((GetAsyncKeyState(0x5B) | GetAsyncKeyState(0x5C)) & 0x8000) != 0;
        return ctrl == _needCtrl && alt == _needAlt && shift == _needShift && win == _needWin;
    }

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr w; public IntPtr l; public uint time; public int x; public int y; }
    const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    static HookProc _hookProc;

    static IntPtr HookCb(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _triggerVk != 0)
        {
            int vk = Marshal.ReadInt32(lParam);
            int msg = (int)wParam;
            if (vk == _triggerVk)
            {
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = msg is WM_KEYUP or WM_SYSKEYUP;
                if (down && _triggerActive) return (IntPtr)1;                 // swallow auto-repeat
                if (down && ModsHeld()) { _triggerActive = true; OnPress(); return (IntPtr)1; }
                if (up && _triggerActive) { _triggerActive = false; OnRelease(); return (IntPtr)1; }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static void OnPress()
    {
        if (_cfg.behavior == "hold") _ = ApplyAsync(_cfg.modeA);
        else { string target = _curMode == _cfg.modeA ? _cfg.modeB : _cfg.modeA; _ = ApplyAsync(target); }
    }
    static void OnRelease()
    {
        if (_cfg.behavior == "hold") _ = ApplyAsync(_cfg.modeB);
    }

    static int Main(string[] args)
    {
        _cfg = AppConfig.Load(CfgFile);
        try { if (File.Exists(StateFile)) _curMode = File.ReadAllText(StateFile).Trim(); } catch { }

        // One-shot mode (scripting / fallback): apply a mode and exit.
        if (args.Length >= 1 && args[0].Equals("--once", StringComparison.OrdinalIgnoreCase))
        {
            string mode = args.Length > 1 ? args[1].ToLowerInvariant() : "toggle";
            if (mode == "toggle") mode = _curMode == "amb" ? "nc" : "amb";
            ApplyAsync(mode).GetAwaiter().GetResult();
            return 0;
        }

        // Resident mode (single instance).
        var mtx = new Mutex(true, "SonyAmbientDaemon_XM5", out bool fresh);
        if (!fresh) return 0;
        ParseHotkey(_cfg.hotkey);
        Log($"daemon start  hotkey='{_cfg.hotkey}' behavior={_cfg.behavior} A={_cfg.modeA} B={_cfg.modeB} level={_cfg.ambientLevel}");

        _ = ConnectAsync();

        if (_triggerVk == 0) { Log("invalid hotkey; nothing to bind"); return 1; }
        _hookProc = HookCb;
        var hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) { Log("SetWindowsHookEx FAILED"); return 1; }
        Log("keyboard hook installed");

        while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        GC.KeepAlive(mtx);
        return 0;
    }
}
