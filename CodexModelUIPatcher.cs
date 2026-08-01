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
    private const int MoveFileReplaceExisting = 0x1;
    private const int MoveFileDelayUntilReboot = 0x4;
    private const int MoveFileWriteThrough = 0x8;

    // Updated for Codex 26.727.4816.0:
    // The old single pattern (u=s&&t!==`amazonBedrock`) no longer exists.
    // The hidden-model filter is now spread across 4 sites. Each OLD pattern
    // appears once; we always replace with a length-preserving !0 + spaces so
    // the asar size never changes. The ternary conditions (useHiddenModels /
    // availableModels / hidden) are fully neutralized, not just the else branch.
    //
    // The first-run patcher only knew the initial patterns and patched only the
    // `!hidden` else branches -> the live file on this machine is already in the
    // partial state, so each site accepts MULTIPLE old patterns (initial and
    // partial) and a single final new pattern, so we can fix-up in one run.
    private struct Patch
    {
        public byte[][] Olds;
        public byte[] Newv;
    }

    private static byte[] B(params byte[] xs) { return xs; }

    private static Patch[] Patches = new Patch[]
    {
        // Site 1: J$r({...,useHiddenModels:i}) visibility ternary wrapped in parens.
        // Full original: (i&&t!==`amazonBedrock`?n.has(r.model):!r.hidden)
        // Partial (first bad patch only fixed the else branch): (...:!0<spaces>)
        // Final: (!0<spaces>) -> entire expression always true.
        // The outside `(` and `)` are kept, so the OP true-branch is also neutralized.
        new Patch
        {
            Olds = new byte[][]
            {
                B(40, 105, 38, 38, 116, 33, 61, 61, 96, 97, 109, 97, 122, 111, 110, 66, 101, 100, 114, 111, 99, 107, 96, 63, 110, 46, 104, 97, 115, 40, 114, 46, 109, 111, 100, 101, 108, 41, 58, 33, 114, 46, 104, 105, 100, 100, 101, 110, 41),
                B(40, 105, 38, 38, 116, 33, 61, 61, 96, 97, 109, 97, 122, 111, 110, 66, 101, 100, 114, 111, 99, 107, 96, 63, 110, 46, 104, 97, 115, 40, 114, 46, 109, 111, 100, 101, 108, 41, 58, 33, 48, 32, 32, 32, 32, 32, 32, 32, 41)
            },
            Newv = B(40, 33, 48, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 41),
        },
        // Site 2: $Bu non-local host branch.
        // Initial: n.filter(e=>!e.hidden);  Final: n.filter(e=>!0<spaces>); -> keeps all models.
        new Patch
        {
            Olds = new byte[][]
            {
                B(110, 46, 102, 105, 108, 116, 101, 114, 40, 101, 61, 62, 33, 101, 46, 104, 105, 100, 100, 101, 110, 41, 59)
            },
            Newv = B(110, 46, 102, 105, 108, 116, 101, 114, 40, 101, 61, 62, 33, 48, 32, 32, 32, 32, 32, 32, 32, 41, 59)
        },
        // Site 3: $Bu catch branch.
        // Initial: n.filter(e=>!e.hidden)}  Final: n.filter(e=>!0<spaces>)} -> keeps all models.
        new Patch
        {
            Olds = new byte[][]
            {
                B(110, 46, 102, 105, 108, 116, 101, 114, 40, 101, 61, 62, 33, 101, 46, 104, 105, 100, 100, 101, 110, 41, 125)
            },
            Newv = B(110, 46, 102, 105, 108, 116, 101, 114, 40, 101, 61, 62, 33, 48, 32, 32, 32, 32, 32, 32, 32, 41, 125)
        },
        // Site 4: $Bu local-host main filter (before the closing }).
        // Initial ternary+): i.useHiddenModels&&r!==`amazonBedrock`?i.availableModels.has(e.model):!e.hidden)}
        // Partial (bad patch only fixed else):  same but else branch = !0<spaces>) }
        // Final: !0<spaces> -> filter callback always true, all models kept.
        new Patch
        {
            Olds = new byte[][]
            {
                B(105, 46, 117, 115, 101, 72, 105, 100, 100, 101, 110, 77, 111, 100, 101, 108, 115, 38, 38, 114, 33, 61, 61, 96, 97, 109, 97, 122, 111, 110, 66, 101, 100, 114, 111, 99, 107, 96, 63, 105, 46, 97, 118, 97, 105, 108, 97, 98, 108, 101, 77, 111, 100, 101, 108, 115, 46, 104, 97, 115, 40, 101, 46, 109, 111, 100, 101, 108, 41, 58, 33, 101, 46, 104, 105, 100, 100, 101, 110, 41, 125),
                B(105, 46, 117, 115, 101, 72, 105, 100, 100, 101, 110, 77, 111, 100, 101, 108, 115, 38, 38, 114, 33, 61, 61, 96, 97, 109, 97, 122, 111, 110, 66, 101, 100, 114, 111, 99, 107, 96, 63, 105, 46, 97, 118, 97, 105, 108, 97, 98, 108, 101, 77, 111, 100, 101, 108, 115, 46, 104, 97, 115, 40, 101, 46, 109, 111, 100, 101, 108, 41, 58, 33, 48, 32, 32, 32, 32, 32, 32, 32, 41, 125)
            },
            Newv = B(33, 48, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 41, 125)
        },
    };

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

            ValidatePatchLengths();

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
        Console.WriteLine("用途: 移除 Codex 模型下拉菜单的隐藏模型过滤 (useHiddenModels / !hidden)，显示所有可用模型。");
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

            // Per site: which old pattern matched (or -1), how many times it appears.
            int[][] oldIndexPer = new int[Patches.Length][];
            int[] oldCountPer = new int[Patches.Length];
            int[] newCountPer = new int[Patches.Length];

            // For each site, count occurrences of every old alternative and of the final new.
            for (int p = 0; p < Patches.Length; p++)
            {
                oldIndexPer[p] = new int[Patches[p].Olds.Length];
                int totalOld = 0;
                for (int o = 0; o < Patches[p].Olds.Length; o++)
                {
                    int idx = IndexOf(original, Patches[p].Olds[o], 0);
                    int cnt = CountAll(original, Patches[p].Olds[o]);
                    oldIndexPer[p][o] = idx;
                    totalOld += cnt;
                }
                oldCountPer[p] = totalOld;
                newCountPer[p] = CountAll(original, Patches[p].Newv);
            }

            int sitesDone = 0;
            int sitesPending = 0;
            for (int p = 0; p < Patches.Length; p++)
            {
                if (newCountPer[p] > 0 && oldCountPer[p] == 0) sitesDone++;
                else if (oldCountPer[p] > 0) sitesPending++;
            }

            Console.WriteLine("状态: pending=" + sitesPending + "/" + Patches.Length + " done=" + sitesDone + "/" + Patches.Length + " size=" + original.Length);
            Log("state pending=" + sitesPending + "/" + Patches.Length + " done=" + sitesDone + " size=" + original.Length);

            if (sitesPending == 0)
            {
                if (sitesDone == Patches.Length)
                {
                    Console.WriteLine("结果: 已经是补丁版。");
                    Console.WriteLine();
                    return PatchResult.AlreadyPatched;
                }
                Console.WriteLine("结果: 未找到可识别的隐藏模型过滤代码 (useHiddenModels / !hidden)。此 Codex 版本可能使用了不同的实现，需要更新补丁器。");
                Console.WriteLine();
                Log("pattern not found");
                return PatchResult.Failed;
            }

            // sitesPending > 0: proceed to patch the pending sites.

            // It is fine for some sites to already be done (e.g. partial first-bad patch
            // fixed the simple filters). We just patch every still-pending site to its
            // final form. Proceed to patch.
            for (int p = 0; p < Patches.Length; p++)
            {
                if (newCountPer[p] > 0) continue; // already done, skip
                int thisOld = 0;
                for (int o = 0; o < Patches[p].Olds.Length; o++)
                {
                    thisOld += CountAll(original, Patches[p].Olds[o]);
                }
                if (thisOld != 1)
                {
                    Console.WriteLine("结果: 站点 " + p + " 匹配次数异常 (" + thisOld + ")，避免误改，已停止。");
                    Console.WriteLine();
                    Log("ambiguous site " + p + " count=" + thisOld);
                    return PatchResult.Failed;
                }
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

            for (int p = 0; p < Patches.Length; p++)
            {
                if (newCountPer[p] > 0) continue; // already done, no rewrite needed
                int idx = -1;
                for (int o = 0; o < Patches[p].Olds.Length; o++)
                {
                    if (oldIndexPer[p][o] >= 0) { idx = oldIndexPer[p][o]; break; }
                }
                if (idx < 0) continue;
                Buffer.BlockCopy(Patches[p].Newv, 0, original, idx, Patches[p].Newv.Length);
                Log("applied site " + p + " at " + idx);
            }
            File.WriteAllBytes(candidatePath, original);
            ApplyTargetAcl(target.AsarPath, candidatePath);

            byte[] candidate = File.ReadAllBytes(candidatePath);
            bool verifyOk = true;
            for (int p = 0; p < Patches.Length; p++)
            {
                bool oldGone = true;
                for (int o = 0; o < Patches[p].Olds.Length; o++)
                {
                    if (IndexOf(candidate, Patches[p].Olds[o], 0) >= 0) oldGone = false;
                }
                if (!oldGone) verifyOk = false;
                if (IndexOf(candidate, Patches[p].Newv, 0) < 0) verifyOk = false;
            }
            if (!verifyOk)
            {
                Console.WriteLine("结果: 候选补丁文件验证失败。");
                Console.WriteLine();
                Log("candidate verification failed");
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

    private static void ValidatePatchLengths()
    {
        for (int p = 0; p < Patches.Length; p++)
        {
            for (int o = 0; o < Patches[p].Olds.Length; o++)
            {
                if (Patches[p].Olds[o].Length != Patches[p].Newv.Length)
                {
                    throw new InvalidOperationException("Patch length mismatch at site " + p + " old " + o + ".");
                }
            }
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

    private static int CountAll(byte[] haystack, byte[] needle)
    {
        if (needle == null || needle.Length == 0 || haystack == null || haystack.Length < needle.Length) return 0;
        int count = 0;
        int idx = 0;
        while (true)
        {
            int found = IndexOf(haystack, needle, idx);
            if (found < 0) break;
            count++;
            idx = found + needle.Length;
        }
        return count;
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
