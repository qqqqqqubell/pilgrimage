using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace Pilgrimage.UI
{
    public enum LanFirewallState { Unsupported, Ready, Declined, Failed }

    // Windows exe only: hosts need an inbound UDP allow rule. School/public Wi-Fi is usually the
    // Public profile, and dismissing Windows' first-run prompt creates Block rules for the exe.
    // Checking needs no admin; fixing asks once through UAC (Windows never allows a silent change).
    public static class LanFirewall
    {
        private const string AppRule = "Pilgrimage LAN App";
        public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode;
        private static string PortRule(ushort port) => "Pilgrimage LAN UDP " + port;

        public static string Describe(LanFirewallState state, ushort port)
        {
            switch (state) {
                case LanFirewallState.Ready: return "방화벽: 허용됨 (UDP " + port + ")";
                case LanFirewallState.Declined: return "방화벽 허용이 취소되었습니다. 다른 PC가 접속하지 못할 수 있습니다.";
                case LanFirewallState.Failed: return "방화벽을 자동 설정하지 못했습니다. 다른 PC가 접속하지 못하면 '방화벽 다시 설정'을 누르세요.";
                default: return "";
            }
        }

        // forcePrompt: skip the check and go straight to the UAC fix (retry button).
        public static IEnumerator Ensure(ushort port, bool forcePrompt, Action<LanFirewallState> done)
        {
            if (!Supported) { done(LanFirewallState.Unsupported); yield break; }
            string exe = ExePath();
            if (exe == null) { done(LanFirewallState.Failed); yield break; }
            if (!forcePrompt) {
                int? check = null;
                yield return Run(CheckScript(port, exe), false, c => check = c, null);
                if (check == 0) { done(LanFirewallState.Ready); yield break; }
            }
            bool declined = false;
            int? fix = null;
            yield return Run(FixScript(port, exe), true, c => fix = c, () => declined = true);
            if (declined) { done(LanFirewallState.Declined); yield break; }
            int? verify = null;
            yield return Run(CheckScript(port, exe), false, c => verify = c, null);
            done(verify == 0 ? LanFirewallState.Ready : LanFirewallState.Failed);
        }

        private static IEnumerator Run(string script, bool elevated, Action<int?> exit, Action declined)
        {
            Process process = null;
            bool cancelled = false;
            try {
                var info = new ProcessStartInfo(PowerShell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand "
                    + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
                if (elevated) { info.UseShellExecute = true; info.Verb = "runas"; info.WindowStyle = ProcessWindowStyle.Hidden; }
                else { info.UseShellExecute = false; info.CreateNoWindow = true; }
                process = Process.Start(info);
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1223) { cancelled = true; } // UAC "아니요"
            catch (Exception e) { UnityEngine.Debug.LogWarning("LAN firewall: " + e.Message); }
            if (cancelled) { declined?.Invoke(); yield break; }
            if (process == null) { exit(null); yield break; }
            float until = Time.realtimeSinceStartup + (elevated ? 120 : 30);
            while (!HasExited(process) && Time.realtimeSinceStartup < until) yield return null;
            int? code = null;
            try { if (process.HasExited) code = process.ExitCode; else process.Kill(); }
            catch (Exception) { }
            finally { process.Dispose(); }
            exit(code);
        }

        private static bool HasExited(Process process) { try { return process.HasExited; } catch (Exception) { return true; } }

        private static string PowerShell {
            get {
                string path = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                return File.Exists(path) ? path : "powershell.exe";
            }
        }

        private static string ExePath()
        {
            try { using (var self = Process.GetCurrentProcess()) return self.MainModule.FileName; }
            catch (Exception) { return null; }
        }

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        private static string CheckScript(ushort port, string exe) =>
            "$ErrorActionPreference='Stop'\n" +
            "$exe=" + Quote(exe) + "\n" +
            "$allow=@(Get-NetFirewallRule -DisplayName " + Quote(PortRule(port)) + " -ErrorAction SilentlyContinue | Where-Object { $_.Enabled -eq 'True' -and $_.Action -eq 'Allow' -and $_.Direction -eq 'Inbound' }).Count\n" +
            "$block=@(Get-NetFirewallApplicationFilter | Where-Object { $_.Program -ieq $exe } | Get-NetFirewallRule | Where-Object { $_.Enabled -eq 'True' -and $_.Action -eq 'Block' -and $_.Direction -eq 'Inbound' }).Count\n" +
            "if ($allow -gt 0 -and $block -eq 0) { exit 0 } else { exit 1 }\n";

        private static string FixScript(ushort port, string exe) =>
            "$ErrorActionPreference='Stop'\n" +
            "$exe=" + Quote(exe) + "\n" +
            "Get-NetFirewallApplicationFilter | Where-Object { $_.Program -ieq $exe } | Get-NetFirewallRule | Where-Object { $_.Action -eq 'Block' } | Remove-NetFirewallRule\n" +
            "Get-NetFirewallRule -DisplayName " + Quote(PortRule(port)) + "," + Quote(AppRule) + " -ErrorAction SilentlyContinue | Remove-NetFirewallRule\n" +
            "New-NetFirewallRule -DisplayName " + Quote(PortRule(port)) + " -Direction Inbound -Protocol UDP -LocalPort " + port + " -Action Allow -Profile Any | Out-Null\n" +
            "New-NetFirewallRule -DisplayName " + Quote(AppRule) + " -Direction Inbound -Program $exe -Action Allow -Profile Any | Out-Null\n" +
            "exit 0\n";
    }
}
