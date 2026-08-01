using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

internal static class CodexModelUIPatcher
{
    private static readonly byte[] OldBytes = new byte[]
    {
        117, 61, 115, 38, 38, 116, 33, 61, 61, 96, 97, 109,
        97, 122, 111, 110, 66, 101, 100, 114, 111, 99, 107, 96
    };

    private static readonly byte[] NewBytes = Encoding.UTF8.GetBytes("u=false                 ");

    private const int MoveFileReplaceExisting = 0x1;
    private const int MoveFileDelayUntilReboot = 0x4;
    private const int MoveFileWriteThrough = 0x8;

    private static readonly string StateRoot = GetExecutableDirectory();

    private static readonly string LocalStateRoot = StateRoot;

    private static readonly string ProgramDataRoot = StateRoot;

    private static readonly string LogPath = Path.Combine(StateRoot, "patcher.log");

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

    private static string GetExecutableDirectory()
    {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        return Path.GetDirectoryName(exePath);
    }

    private sealed class Target
    {
        public string InstallLocation;
        public string AsarPath;
        public string PackageName;
    }

    private enum PatchResult
    {
        AlreadyPatched,
        PatchedNow,
        ScheduledForReboot,
        Failed
    }

    private static int Main(string[] args)
    {
        Directory.CreateDirectory(LocalStateRoot);
        Directory.CreateDirectory(ProgramDataRoot);

        bool noPause = HasArg(args, "--no-pause");

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
        }

        try
        {
            if (!IsAdministrator())
            {
                RelaunchAsAdmin(args);
                return 0;
            }

            PrintHeader();
            Log("started");

            if (OldBytes.Length != NewBytes.Length)
            {
                throw new InvalidOperationException("Patch length mismatch.");
            }

            List<Target> targets = FindTargets();
            if (targets.Count == 0)
            {
                Console.WriteLine("没有找到 OpenAI.Codex 的 app.asar。");
                Console.WriteLine("确认 Microsoft Store 版 Codex (26.721.3996+) 已安装后，再运行这个程序。");
                return Finish(2, noPause);
            }

            int already = 0;
            int patchedNow = 0;
            int scheduled = 0;
            int failed = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                PatchResult result = PatchTarget(targets[i]);
                if (result == PatchResult.AlreadyPatched) already++;
                else if (result == PatchResult.PatchedNow) patchedNow++;
                else if (result == PatchResult.ScheduledForReboot) scheduled++;
                else failed++;
            }

            Console.WriteLine();
            Console.WriteLine("结果：");
            Console.WriteLine("  已经是补丁版: " + already);
            Console.WriteLine("  已立即完成:   " + patchedNow);
            Console.WriteLine("  已排到重启:   " + scheduled);
            Console.WriteLine("  失败:         " + failed);

            if (scheduled > 0)
            {
                Console.WriteLine();
                Console.WriteLine("切换已安排在下次重启时生效。");
                Console.WriteLine("重启后模型菜单中所有模型都会显示；不用再运行一次。");
            }
            else if (patchedNow > 0)
            {
                Console.WriteLine();
                Console.WriteLine("补丁已生效。请完全退出 Codex 后重新打开，模型菜单将显示所有模型。");
            }
            else if (already > 0 && failed == 0)
            {
                Console.WriteLine();
                Console.WriteLine("当前安装已经是补丁版，不需要操作。");
            }

            Console.WriteLine();
            Console.WriteLine("日志: " + LogPath);
            Console.WriteLine("备份: " + Path.Combine(LocalStateRoot, "Backups"));

            return Finish(failed == 0 ? 0 : 1, noPause);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("出错了: " + ex.Message);
            Log("fatal: " + ex);
            return Finish(1, noPause);
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine("Codex Model UI Patcher");
        Console.WriteLine("----------------------");
        Console.WriteLine("用途: 移除 Codex 模型下拉菜单的隐藏模型过滤 (u=s&&t!==`amazonBedrock`)，显示所有可用模型。");
        Console.WriteLine();
    }

    private static PatchResult PatchTarget(Target target)
    {
        Console.WriteLine("目标: " + target.PackageName);
        Console.WriteLine("路径: " + target.AsarPath);
        Log("target " + target.AsarPath);

        try
        {
            byte[] original = File.ReadAllBytes(target.AsarPath);
            int oldIndex = IndexOf(original, OldBytes, 0);
            int newIndex = IndexOf(original, NewBytes, 0);

            Console.WriteLine("状态: oldIndex=" + oldIndex + " newIndex=" + newIndex + " size=" + original.Length);
            Log("state oldIndex=" + oldIndex + " newIndex=" + newIndex + " size=" + original.Length);

            if (oldIndex < 0)
            {
                if (newIndex >= 0)
                {
                    Console.WriteLine("结果: 已经是补丁版。");
                    Console.WriteLine();
                    return PatchResult.AlreadyPatched;
                }

                Console.WriteLine("结果: 未找到可识别的过滤代码 (u=s&&t!==`amazonBedrock`)。此 Codex 版本可能使用了不同的实现，需要更新补丁器。");
                Console.WriteLine();
                Log("pattern not found");
                return PatchResult.Failed;
            }

            int second = IndexOf(original, OldBytes, oldIndex + OldBytes.Length);
            if (second >= 0)
            {
                Console.WriteLine("结果: 找到多个候选位置，避免误改，已停止。");
                Console.WriteLine();
                Log("ambiguous offsets " + oldIndex + " " + second);
                return PatchResult.Failed;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string safePackageName = SafeFileName(target.PackageName);
            string backupDir = Path.Combine(LocalStateRoot, "Backups");
            string candidateDir = Path.Combine(ProgramDataRoot, "Candidates", safePackageName, stamp);
            Directory.CreateDirectory(backupDir);
            Directory.CreateDirectory(candidateDir);

            string backupPath = Path.Combine(backupDir, "app.asar." + safePackageName + "." + stamp + ".bak");
            string candidatePath = Path.Combine(candidateDir, "app.asar.patched");

            File.Copy(target.AsarPath, backupPath, true);
            Console.WriteLine("备份: " + backupPath);
            Log("backup " + backupPath);

            Buffer.BlockCopy(NewBytes, 0, original, oldIndex, NewBytes.Length);
            File.WriteAllBytes(candidatePath, original);
            ApplyTargetAcl(target.AsarPath, candidatePath);

            byte[] candidate = File.ReadAllBytes(candidatePath);
            int verifyOld = IndexOf(candidate, OldBytes, 0);
            int verifyNew = IndexOf(candidate, NewBytes, 0);
            if (verifyOld >= 0 || verifyNew < 0)
            {
                Console.WriteLine("结果: 候选补丁文件验证失败。");
                Console.WriteLine();
                Log("candidate verification failed old=" + verifyOld + " new=" + verifyNew);
                return PatchResult.Failed;
            }

            Console.WriteLine("补丁: " + candidatePath);
            Log("candidate " + candidatePath);

            bool replacedNow = MoveFileEx(candidatePath, target.AsarPath, MoveFileReplaceExisting | MoveFileWriteThrough);
            if (replacedNow)
            {
                Console.WriteLine("结果: 已立即替换。");
                Console.WriteLine();
                Log("immediate replace ok");
                return PatchResult.PatchedNow;
            }

            int immediateError = Marshal.GetLastWin32Error();
            Console.WriteLine("立即替换被系统拒绝，错误码: " + immediateError);
            Log("immediate replace failed err=" + immediateError);

            if (!File.Exists(candidatePath))
            {
                File.WriteAllBytes(candidatePath, original);
                ApplyTargetAcl(target.AsarPath, candidatePath);
            }

            bool scheduled = MoveFileEx(candidatePath, target.AsarPath, MoveFileReplaceExisting | MoveFileDelayUntilReboot);
            if (!scheduled)
            {
                int scheduleError = Marshal.GetLastWin32Error();
                Console.WriteLine("结果: 安排重启替换也失败，错误码: " + scheduleError);
                Console.WriteLine();
                Log("schedule failed err=" + scheduleError);
                return PatchResult.Failed;
            }

            Console.WriteLine("结果: 已安排到下次重启前替换。");
            Console.WriteLine();
            Log("scheduled for reboot");
            return PatchResult.ScheduledForReboot;
        }
        catch (Exception ex)
        {
            Console.WriteLine("结果: 失败 - " + ex.Message);
            Console.WriteLine();
            Log("target failed: " + ex);
            return PatchResult.Failed;
        }
    }

    private static List<Target> FindTargets()
    {
        Dictionary<string, Target> targets = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);

        AddInstallLocationsFromRunningProcesses(targets);
        if (targets.Count == 0)
        {
            AddInstallLocationsFromAppx(targets);
        }
        if (targets.Count == 0)
        {
            AddInstallLocationsFromWindowsAppsEnumeration(targets);
        }

        List<Target> list = new List<Target>(targets.Values);
        list.Sort(delegate(Target a, Target b)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(b.PackageName, a.PackageName);
        });
        return list;
    }

    private static void AddInstallLocationsFromAppx(Dictionary<string, Target> targets)
    {
        string command = "Get-AppxPackage -Name OpenAI.Codex | Sort-Object Version -Descending | ForEach-Object { $_.InstallLocation }";
        string output = RunPowerShell(command);
        if (output == null) return;

        string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            AddInstallLocation(targets, lines[i].Trim());
        }
    }

    private static void AddInstallLocationsFromRunningProcesses(Dictionary<string, Target> targets)
    {
        AddInstallLocationsFromProcessName(targets, "ChatGPT");
        AddInstallLocationsFromProcessName(targets, "codex");
    }

    private static void AddInstallLocationsFromProcessName(Dictionary<string, Target> targets, string name)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(name);
        }
        catch
        {
            return;
        }

        for (int i = 0; i < processes.Length; i++)
        {
            using (processes[i])
            {
                string path = null;
                try
                {
                    path = processes[i].MainModule.FileName;
                }
                catch
                {
                }

                if (String.IsNullOrEmpty(path)) continue;
                string install = GuessInstallLocationFromProcessPath(path);
                AddInstallLocation(targets, install);
            }
        }
    }

    private static void AddInstallLocationsFromWindowsAppsEnumeration(Dictionary<string, Target> targets)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
        try
        {
            string[] dirs = Directory.GetDirectories(root, "OpenAI.Codex_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < dirs.Length; i++)
            {
                AddInstallLocation(targets, dirs[i]);
            }
        }
        catch
        {
        }
    }

    private static void AddInstallLocation(Dictionary<string, Target> targets, string installLocation)
    {
        if (String.IsNullOrWhiteSpace(installLocation)) return;

        installLocation = installLocation.Trim();
        string asar = Path.Combine(installLocation, "app", "resources", "app.asar");
        if (!File.Exists(asar)) return;

        string key = Path.GetFullPath(asar);
        if (targets.ContainsKey(key)) return;

        targets[key] = new Target
        {
            InstallLocation = installLocation,
            AsarPath = asar,
            PackageName = new DirectoryInfo(installLocation).Name
        };
    }

    private static string GuessInstallLocationFromProcessPath(string path)
    {
        try
        {
            DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(path));
            while (dir != null)
            {
                if (String.Equals(dir.Name, "app", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
                {
                    return dir.Parent.FullName;
                }
                dir = dir.Parent;
            }
        }
        catch
        {
        }
        return null;
    }

    private static string RunPowerShell(string command)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                    Log("powershell timed out");
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    Log("powershell error: " + error);
                }
                return output;
            }
        }
        catch (Exception ex)
        {
            Log("powershell failed: " + ex.Message);
            return null;
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle == null || needle.Length == 0) return -1;
        if (haystack == null || haystack.Length < needle.Length) return -1;
        if (start < 0) start = 0;

        int max = haystack.Length - needle.Length;
        for (int i = start; i <= max; i++)
        {
            bool matched = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }
            if (matched) return i;
        }
        return -1;
    }

    private static void ApplyTargetAcl(string targetPath, string candidatePath)
    {
        try
        {
            FileSecurity security = File.GetAccessControl(targetPath, AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
            File.SetAccessControl(candidatePath, security);
            Log("acl copied");
        }
        catch (Exception ex)
        {
            Console.WriteLine("提示: ACL 复制失败，继续尝试替换: " + ex.Message);
            Log("acl copy failed: " + ex.Message);
        }
    }

    private static string SafeFileName(string value)
    {
        if (String.IsNullOrEmpty(value)) return "unknown";
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            bool bad = false;
            for (int j = 0; j < invalid.Length; j++)
            {
                if (ch == invalid[j])
                {
                    bad = true;
                    break;
                }
            }
            sb.Append(bad ? '_' : ch);
        }
        return sb.ToString();
    }

    private static bool IsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RelaunchAsAdmin(string[] args)
    {
        string exe = Process.GetCurrentProcess().MainModule.FileName;
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = exe;
        psi.UseShellExecute = true;
        psi.Verb = "runas";
        psi.Arguments = JoinArguments(args);
        Process.Start(psi);
    }

    private static string JoinArguments(string[] args)
    {
        if (args == null || args.Length == 0) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(QuoteArgument(args[i]));
        }
        return sb.ToString();
    }

    private static string QuoteArgument(string arg)
    {
        if (arg == null) return "\"\"";
        if (arg.IndexOfAny(new char[] { ' ', '\t', '"' }) < 0) return arg;
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static bool HasArg(string[] args, string expected)
    {
        if (args == null) return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (String.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int Finish(int code, bool noPause)
    {
        Log("finished code=" + code);
        if (!noPause)
        {
            Console.WriteLine();
            Console.Write("按 Enter 关闭...");
            Console.ReadLine();
        }
        return code;
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LocalStateRoot);
            File.AppendAllText(LogPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }
}
