using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Ceprkac
{
    /// <summary>
    /// Unload foreign DLLs from Ceprkac and its children the moment they are
    /// mapped — including at process start. Infectors are expected then, so
    /// nothing is grandfathered by a late snapshot.
    ///
    /// Keep: this exe's directory, Edge WebView2, Windows, .NET.
    /// Unload: anything else with a resolvable path (Temp, AppData injectors,
    /// overlays, etc.). Unresolvable paths are skipped so a lookup miss cannot
    /// FreeLibrary a GPU/codec module.
    /// After init settles, any brand-new mapping is also unloaded unless it
    /// belongs to those trees (late WebView2 delay-loads still allowed).
    /// </summary>
    internal sealed class InjectedModuleCleaner
    {
        public static InjectedModuleCleaner? Instance { get; private set; }

        private const string WebView2ClientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        private const int PollMs = 50;
        private const int StablePollsNeeded = 40;

        private readonly HashSet<IntPtr> _ours = new();
        private readonly HashSet<IntPtr> _attempted = new();
        private readonly Dictionary<int, ChildState> _children = new();
        private readonly List<string> _prefixes = new();
        private readonly List<string> _userProfilePrefixes = new();
        private readonly ManualResetEventSlim _stop = new(false);
        private readonly object _startLock = new();
        private Thread? _thread;
        private IntPtr _ldrCookie;
        private LdrDllNotification? _ldrCb;
        private Dictionary<string, string>? _dosDevices;
        private bool _hostFrozen;
        private int _hostLastCount;
        private int _hostStablePolls;

        private sealed class ChildState
        {
            public HashSet<IntPtr> Ours { get; } = new();
            public bool Frozen;
            public int LastCount;
            public int StablePolls;
        }

        public static InjectedModuleCleaner StartGlobal()
        {
            var c = Instance;
            if (c != null) { c.Start(); return c; }
            c = new InjectedModuleCleaner();
            Instance = c;
            c.Start();
            return c;
        }

        public void Start()
        {
            lock (_startLock)
            {
                if (_thread != null) return;
                BuildPrefixes();
                _thread = new Thread(Run) { IsBackground = true, Name = "Ceprkac-ModuleCleaner" };
                _thread.Start();
                RegisterLdr();
            }
        }

        public void Stop()
        {
            _stop.Set();
            UnregisterLdr();
            _thread?.Join(500);
        }

        private void BuildPrefixes()
        {
            void add(string? p)
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                var n = NormalizePath(p);
                if (n.Length == 0) return;
                if (File.Exists(n))
                {
                    try { n = Path.GetDirectoryName(n) ?? n; } catch { }
                    n = NormalizePath(n);
                }
                if (n.Length == 0) return;
                if (!_prefixes.Contains(n)) _prefixes.Add(n);
            }

            add(AppDomain.CurrentDomain.BaseDirectory);
            try { add(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName)); } catch { }
            try { add(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()); } catch { }

            add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Microsoft\EdgeWebView");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft\EdgeWebView");
            add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\EdgeWebView");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Microsoft\Edge");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft\Edge");
            add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Edge");
            add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Microsoft\EdgeUpdate");
            add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\EdgeUpdate");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Microsoft\EdgeUpdate");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft\EdgeUpdate");
            add(Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"));
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Common Files\Microsoft Shared");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Common Files\Microsoft Shared");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\dotnet");
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\dotnet");
            DiscoverEdgeRuntimeFolders(add);

            void addUser(string? p)
            {
                var n = NormalizePath(p);
                if (n.Length == 0) return;
                if (!_userProfilePrefixes.Contains(n)) _userProfilePrefixes.Add(n);
            }
            addUser(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            addUser(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            addUser(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
        }

        private bool IsClearlyForeign(string path)
        {
            var n = NormalizePath(path);
            if (n.Length == 0) return false;
            if (IsTempPath(n)) return true;
            foreach (var pre in _userProfilePrefixes)
            {
                if (n == pre || n.StartsWith(pre + "\\", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void DiscoverEdgeRuntimeFolders(Action<string?> add)
        {
            string[] keys =
            {
                @"SOFTWARE\Microsoft\EdgeUpdate\ClientState\" + WebView2ClientGuid,
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState\" + WebView2ClientGuid,
                @"SOFTWARE\Microsoft\EdgeUpdate\Clients\" + WebView2ClientGuid,
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + WebView2ClientGuid,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView",
            };
            string[] names = { "location", "Location", "InstallLocation", "UninstallString" };
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var key in keys)
                {
                    try
                    {
                        using var k = hive.OpenSubKey(key);
                        if (k == null) continue;
                        foreach (var name in names)
                        {
                            var raw = k.GetValue(name) as string;
                            if (string.IsNullOrWhiteSpace(raw)) continue;
                            var v = raw!;
                            if (name.Equals("UninstallString", StringComparison.OrdinalIgnoreCase))
                            {
                                if (v.StartsWith("\"", StringComparison.Ordinal))
                                {
                                    int end = v.IndexOf('"', 1);
                                    if (end > 1) v = v.Substring(1, end - 1);
                                }
                                else
                                {
                                    int sp = v.IndexOf(" /", StringComparison.Ordinal);
                                    if (sp > 0) v = v.Substring(0, sp);
                                }
                            }
                            add(v);
                        }
                    }
                    catch { }
                }
            }
        }

        private bool BelongsPath(string path)
        {
            var n = NormalizePath(path);
            if (n.Length == 0) return false;
            if (IsTempPath(n)) return false;
            foreach (var pre in _prefixes)
            {
                if (n == pre || n.StartsWith(pre + "\\", StringComparison.Ordinal))
                    return true;
            }
            return IsGpuIcdName(n);
        }

        private static bool IsTempPath(string n) =>
            n.IndexOf("\\temp\\", StringComparison.Ordinal) >= 0
            || n.IndexOf("\\tmp\\", StringComparison.Ordinal) >= 0
            || n.EndsWith("\\temp", StringComparison.Ordinal)
            || n.EndsWith("\\tmp", StringComparison.Ordinal);

        private static readonly string[] GpuIcdPrefixes =
        {
            "nvldumd", "nvwgf2um", "nvd3dum", "nvoglv", "nvapi", "nvopencl", "nvcuda",
            "atidxx", "atio6axx", "amdxc", "amdvlk", "atiadlxx", "atioglxx", "amdocl",
            "igc64", "igc32", "igd10", "igd12", "igdail", "igd9s", "ig4icd", "ig9icd",
            "ig11icd", "ig12icd", "igvk", "intelocl", "igdrcl",
        };

        private static bool IsGpuIcdName(string n)
        {
            string file;
            try { file = Path.GetFileNameWithoutExtension(n); }
            catch { return false; }
            if (string.IsNullOrEmpty(file)) return false;
            file = file.ToLowerInvariant();
            foreach (var p in GpuIcdPrefixes)
                if (file.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        private void Run()
        {
            while (!_stop.Wait(PollMs))
            {
                try
                {
                    SweepSelf();
                    SweepChildren();
                }
                catch { }
            }
        }

        private void SweepSelf()
        {
            var proc = GetCurrentProcess();
            var self = GetModuleHandleW(null);
            var mods = EnumModules(proc);
            foreach (var (h, path) in mods)
            {
                if (h == IntPtr.Zero || h == self) continue;
                if (BelongsPath(path))
                {
                    _ours.Add(h);
                    continue;
                }
                if (NormalizePath(path).Length == 0) continue;
                if (IsClearlyForeign(path) || _hostFrozen)
                    TryUnloadLocal(h);
            }
            UpdateFrozen(ref _hostFrozen, ref _hostLastCount, ref _hostStablePolls, mods.Count);
        }

        private void SweepChildren()
        {
            if (!_hostFrozen) return;
            var live = DescendantPids();
            foreach (var dead in new List<int>(_children.Keys))
                if (!live.Contains(dead)) _children.Remove(dead);

            const uint access = 0x0400 | 0x0010 | 0x0002 | 0x0008 | 0x0020 | 0x1000;
            foreach (var pid in live)
            {
                var h = OpenProcess(access, false, (uint)pid);
                if (h == IntPtr.Zero) continue;
                try
                {
                    if (!_children.TryGetValue(pid, out var st))
                    {
                        st = new ChildState();
                        _children[pid] = st;
                    }
                    var mods = EnumModules(h);
                    foreach (var (mh, path) in mods)
                    {
                        if (mh == IntPtr.Zero) continue;
                        if (BelongsPath(path))
                        {
                            st.Ours.Add(mh);
                            continue;
                        }
                        if (NormalizePath(path).Length == 0) continue;
                        if (IsClearlyForeign(path) || st.Frozen)
                            TryUnloadRemote(h, mh);
                    }
                    UpdateFrozen(ref st.Frozen, ref st.LastCount, ref st.StablePolls, mods.Count);
                }
                catch { }
                finally { CloseHandle(h); }
            }
        }

        private static void UpdateFrozen(ref bool frozen, ref int lastCount, ref int stablePolls, int count)
        {
            if (frozen || count == 0) return;
            if (count != lastCount)
            {
                lastCount = count;
                stablePolls = 0;
                return;
            }
            stablePolls++;
            if (stablePolls >= StablePollsNeeded)
                frozen = true;
        }

        private HashSet<int> DescendantPids()
        {
            int me = Process.GetCurrentProcess().Id;
            var rows = new List<(int pid, int ppid)>();
            var snap = CreateToolhelp32Snapshot(0x00000002, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return new HashSet<int>();
            try
            {
                var pe = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
                if (!Process32FirstW(snap, ref pe)) return new HashSet<int>();
                do { rows.Add(((int)pe.th32ProcessID, (int)pe.th32ParentProcessID)); }
                while (Process32NextW(snap, ref pe));
            }
            finally { CloseHandle(snap); }
            var tree = new HashSet<int> { me };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var (pid, ppid) in rows)
                    if (tree.Contains(ppid) && tree.Add(pid)) changed = true;
            }
            tree.Remove(me);
            return tree;
        }

        private void TryUnloadLocal(IntPtr hmod)
        {
            if (!_attempted.Add(hmod)) return;
            for (int i = 0; i < 32; i++)
                if (!FreeLibrary(hmod)) break;
        }

        private void TryUnloadRemote(IntPtr proc, IntPtr hmod)
        {
            if (!_attempted.Add(hmod)) return;
            var k32 = GetModuleHandleW("kernel32.dll");
            var free = GetProcAddress(k32, "FreeLibrary");
            if (free == IntPtr.Zero) return;
            var t = CreateRemoteThread(proc, IntPtr.Zero, UIntPtr.Zero, free, hmod, 0, out _);
            if (t == IntPtr.Zero) return;
            WaitForSingleObject(t, 100);
            CloseHandle(t);
        }

        private List<(IntPtr, string)> EnumModules(IntPtr proc)
        {
            var result = new List<(IntPtr, string)>();
            var arr = new IntPtr[1024];
            if (!EnumProcessModulesEx(proc, arr, arr.Length * IntPtr.Size, out int needed, 3))
                return result;
            int count = Math.Min(needed / IntPtr.Size, arr.Length);
            var buf = new char[32768];
            for (int i = 0; i < count; i++)
            {
                if (arr[i] == IntPtr.Zero) continue;
                int n = GetModuleFileNameEx(proc, arr[i], buf, buf.Length);
                result.Add((arr[i], n > 0 ? new string(buf, 0, n) : ""));
            }
            return result;
        }

        private string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                var p = path!.Trim().Replace('/', '\\');
                if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p.Substring(4);
                if (p.StartsWith(@"\??\", StringComparison.Ordinal)) p = p.Substring(4);
                p = ToDosPath(p);
                if (p.Length >= 2 && p[1] == ':')
                {
                    var sb = new StringBuilder(32768);
                    int n = GetLongPathName(p, sb, sb.Capacity);
                    if (n > 0 && n < sb.Capacity) p = sb.ToString();
                }
                p = Path.GetFullPath(p).TrimEnd('\\');
                return p.ToLowerInvariant();
            }
            catch { return ""; }
        }

        private string ToDosPath(string path)
        {
            if (!path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
                return path;
            var map = _dosDevices ?? (_dosDevices = BuildDosDeviceMap());
            foreach (var kv in map)
            {
                if (path.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value + path.Substring(kv.Key.Length);
            }
            return path;
        }

        private static Dictionary<string, string> BuildDosDeviceMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var d in Environment.GetLogicalDrives())
                {
                    var letter = d.TrimEnd('\\');
                    var sb = new StringBuilder(260);
                    if (QueryDosDevice(letter, sb, sb.Capacity) != 0)
                    {
                        var device = sb.ToString();
                        if (!string.IsNullOrEmpty(device))
                            map[device] = letter;
                    }
                }
            }
            catch { }
            return map;
        }

        private void RegisterLdr()
        {
            _ldrCb = (reason, data, ctx) =>
            {
                GC.KeepAlive(reason);
                GC.KeepAlive(data);
                GC.KeepAlive(ctx);
            };
            LdrRegisterDllNotification(0, _ldrCb, IntPtr.Zero, out _ldrCookie);
        }

        private void UnregisterLdr()
        {
            if (_ldrCookie != IntPtr.Zero)
                LdrUnregisterDllNotification(_ldrCookie);
            _ldrCookie = IntPtr.Zero;
        }

        private delegate void LdrDllNotification(uint reason, IntPtr data, IntPtr ctx);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint a, bool i, uint pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? n);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr h, string n);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateRemoteThread(IntPtr p, IntPtr a, UIntPtr s, IntPtr start, IntPtr arg, uint f, out uint tid);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint f, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr s, ref PROCESSENTRY32W pe);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr s, ref PROCESSENTRY32W pe);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetLongPathName(string lpszShortPath, StringBuilder lpszLongPath, int cchBuffer);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);
        [DllImport("psapi.dll", SetLastError = true)] private static extern bool EnumProcessModulesEx(IntPtr p, IntPtr[] m, int cb, out int n, uint f);
        [DllImport("psapi.dll", CharSet = CharSet.Unicode)] private static extern int GetModuleFileNameEx(IntPtr p, IntPtr m, [Out] char[] b, int s);
        [DllImport("ntdll.dll")] private static extern uint LdrRegisterDllNotification(uint f, LdrDllNotification cb, IntPtr ctx, out IntPtr cookie);
        [DllImport("ntdll.dll")] private static extern uint LdrUnregisterDllNotification(IntPtr cookie);
    }
}
