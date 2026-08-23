using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Ceprkac
{
    /// <summary>
    /// Registers Ceprkac as a Windows browser (http/https + HTML files) and
    /// opens the OS default-app picker. Windows 10/11 will not silently steal
    /// UserChoice; the user confirms once in Settings.
    /// </summary>
    internal static class BrowserRegistration
    {
        public const string AppName = "Ceprkac";
        public const string UrlProgId = "CeprkacURL";
        public const string HtmlProgId = "CeprkacHTML";
        public const string CapabilitiesKey = @"Software\Clients\StartMenuInternet\Ceprkac\Capabilities";

        private static readonly string[] UrlProtocols = { "http", "https" };
        private static readonly string[] HtmlExts = { ".htm", ".html", ".shtml", ".xhtml", ".xht", ".svg", ".webp", ".mht", ".mhtml" };

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
        private const int SHCNE_ASSOCCHANGED = 0x08000000;

        public static string ExePath =>
            Process.GetCurrentProcess().MainModule?.FileName
            ?? System.Windows.Forms.Application.ExecutablePath;

        public static void Register()
        {
            var exe = ExePath;
            var cmd = "\"" + exe + "\" \"%1\"";
            var icon = exe + ",0";
            var openCmd = "\"" + exe + "\"";

            TryWrite(RegistryHive.CurrentUser, exe, cmd, icon, openCmd);
            TryWrite(RegistryHive.LocalMachine, exe, cmd, icon, openCmd);
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
        }

        public static bool IsDefault()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                var prog = key?.GetValue("ProgId") as string ?? "";
                if (prog.StartsWith(AppName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
                var prog = key?.GetValue("ProgId") as string ?? "";
                if (prog.StartsWith(AppName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Register, then open the Windows UI so the user can confirm.</summary>
        public static void RegisterAndRequestDefault()
        {
            Register();
            TrySetAppAsDefault();
            if (!IsDefault())
                OpenDefaultAppsSettings();
        }

        public static void OpenDefaultAppsSettings()
        {
            // Win10: classic association UI for this app. Win11: Settings page.
            if (!TryLaunchAssociationUi() && !TryStart("ms-settings:defaultapps?registeredAppUser=" + AppName)
                && !TryStart("ms-settings:defaultapps?registeredAppMachine=" + AppName)
                && !TryStart("ms-settings:defaultapps"))
            {
                TryStart("computerdefaults.exe");
            }
        }

        private static void TryWrite(RegistryHive hive, string exe, string cmd, string icon, string openCmd)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                WriteStartMenuInternet(baseKey, exe, cmd, icon, openCmd);
                WriteProgId(baseKey, @"Software\Classes\" + UrlProgId, "Ceprkac URL", cmd, icon, urlProtocol: true);
                WriteProgId(baseKey, @"Software\Classes\" + HtmlProgId, "Ceprkac HTML Document", cmd, icon, urlProtocol: false);
                using (var apps = baseKey.CreateSubKey(@"Software\RegisteredApplications"))
                    apps?.SetValue(AppName, CapabilitiesKey);
                using (var appPath = baseKey.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\Ceprkac.exe"))
                {
                    appPath?.SetValue("", exe);
                    appPath?.SetValue("Path", System.IO.Path.GetDirectoryName(exe) ?? "");
                }
            }
            catch { }
        }

        private static void WriteStartMenuInternet(RegistryKey baseKey, string exe, string cmd, string icon, string openCmd)
        {
            const string root = @"Software\Clients\StartMenuInternet\Ceprkac";
            using (var k = baseKey.CreateSubKey(root))
            {
                k?.SetValue("", AppName);
                k?.SetValue("LocalizedString", AppName);
            }
            using (var k = baseKey.CreateSubKey(root + @"\DefaultIcon"))
                k?.SetValue("", icon);
            using (var k = baseKey.CreateSubKey(root + @"\shell\open\command"))
                k?.SetValue("", openCmd);
            using (var k = baseKey.CreateSubKey(root + @"\InstallInfo"))
            {
                k?.SetValue("ReinstallCommand", openCmd + " --register-browser");
                k?.SetValue("HideIconsCommand", openCmd + " --register-browser");
                k?.SetValue("ShowIconsCommand", openCmd + " --register-browser");
                k?.SetValue("IconsVisible", 1, RegistryValueKind.DWord);
            }
            using (var k = baseKey.CreateSubKey(root + @"\Capabilities"))
            {
                k?.SetValue("ApplicationName", AppName);
                k?.SetValue("ApplicationIcon", icon);
                k?.SetValue("ApplicationDescription", "Ceprkac web browser");
            }
            using (var k = baseKey.CreateSubKey(root + @"\Capabilities\StartMenu"))
                k?.SetValue("StartMenuInternet", AppName);
            using (var k = baseKey.CreateSubKey(root + @"\Capabilities\URLAssociations"))
            {
                foreach (var p in UrlProtocols)
                    k?.SetValue(p, UrlProgId);
            }
            using (var k = baseKey.CreateSubKey(root + @"\Capabilities\FileAssociations"))
            {
                foreach (var ext in HtmlExts)
                    k?.SetValue(ext, HtmlProgId);
            }
            using (var k = baseKey.CreateSubKey(root + @"\Capabilities\MimeAssociations"))
            {
                k?.SetValue("text/html", HtmlProgId);
                k?.SetValue("application/xhtml+xml", HtmlProgId);
            }
        }

        private static void WriteProgId(RegistryKey baseKey, string path, string friendly, string cmd, string icon, bool urlProtocol)
        {
            using var k = baseKey.CreateSubKey(path);
            if (k == null) return;
            k.SetValue("", friendly);
            if (urlProtocol) k.SetValue("URL Protocol", "");
            using (var d = k.CreateSubKey("DefaultIcon"))
                d?.SetValue("", icon);
            using (var c = k.CreateSubKey(@"shell\open\command"))
                c?.SetValue("", cmd);
        }

        private static bool TryLaunchAssociationUi()
        {
            try
            {
                var ui = (IApplicationAssociationRegistrationUI)new ApplicationAssociationRegistrationUI();
                int hr = ui.LaunchAdvancedAssociationUI(AppName);
                return hr == 0;
            }
            catch { return false; }
        }

        private static void TrySetAppAsDefault()
        {
            try
            {
                var reg = (IApplicationAssociationRegistration)new ApplicationAssociationRegistration();
                foreach (var p in UrlProtocols)
                    try { reg.SetAppAsDefault(AppName, p, AT_URLPROTOCOL); } catch { }
                foreach (var ext in HtmlExts)
                    try { reg.SetAppAsDefault(AppName, ext, AT_FILEEXTENSION); } catch { }
            }
            catch { }
        }

        private static bool TryStart(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                return true;
            }
            catch { return false; }
        }

        private const int AT_FILEEXTENSION = 0;
        private const int AT_URLPROTOCOL = 1;

        [ComImport]
        [Guid("1968106d-f3b5-44cf-890e-116fcb9ecef1")]
        private class ApplicationAssociationRegistrationUI { }

        [ComImport]
        [Guid("1f76a169-f994-40ac-8fc8-0959e8874710")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationAssociationRegistrationUI
        {
            [PreserveSig]
            int LaunchAdvancedAssociationUI([MarshalAs(UnmanagedType.LPWStr)] string pszAppRegName);
        }

        [ComImport]
        [Guid("591209c7-767b-42b2-9fba-44ee4615f2c7")]
        private class ApplicationAssociationRegistration { }

        [ComImport]
        [Guid("4e530b0a-e611-4c77-a3ac-9031d024307c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationAssociationRegistration
        {
            void QueryCurrentDefault();
            void QueryAppIsDefault();
            void QueryAppIsDefaultAll();
            void SetAppAsDefault(
                [MarshalAs(UnmanagedType.LPWStr)] string pszAppRegistryName,
                [MarshalAs(UnmanagedType.LPWStr)] string pszAssociation,
                int atQueryType);
            void SetAppAsDefaultAll([MarshalAs(UnmanagedType.LPWStr)] string pszAppRegistryName);
            void ClearUserAssociations();
        }
    }
}
