using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using MediaPlayer = System.Windows.Media.MediaPlayer;

[assembly: AssemblyTitle("Big Dog, Bark Bark Bark")]
[assembly: AssemblyProduct("Big Dog, Bark Bark Bark")]
[assembly: AssemblyDescription("Portable Codex completion sound tray app")]
[assembly: AssemblyCompany("fangfren")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace BigDogBark
{
    internal static class Program
    {
        private const string ProductName = "大狗大狗叫叫叫";
        private const string EnglishName = "Big Dog, Bark Bark Bark";
        private const string RepositoryUrl = "https://github.com/fangfren/BigDog-Bark-Bark-Bark";
        private const string MutexName = "BigDogBark.Tray.SingleInstance";
        private const string StartupRegistryName = "BigDogBark";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private static string AppDirectory
        {
            get
            {
                string overridePath = GetEnvironment("BIGDOGBARK_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(overridePath))
                {
                    return overridePath;
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BigDogBark");
            }
        }

        private static string CodexHome
        {
            get
            {
                string overridePath = GetEnvironment("BIGDOGBARK_CODEX_HOME");
                if (!string.IsNullOrWhiteSpace(overridePath))
                {
                    return overridePath;
                }

                string codexHome = GetEnvironment("CODEX_HOME");
                if (!string.IsNullOrWhiteSpace(codexHome))
                {
                    return codexHome;
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex");
            }
        }

        private static string InstalledExePath
        {
            get { return Path.Combine(AppDirectory, "BigDogBark.exe"); }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(AppDirectory, "settings.json"); }
        }

        private static string DefaultSoundPath
        {
            get { return Path.Combine(AppDirectory, "default.wav"); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                string argumentLog = Environment.GetEnvironmentVariable("BIGDOGBARK_ARG_LOG");
                if (!string.IsNullOrWhiteSpace(argumentLog))
                {
                    File.WriteAllText(argumentLog, string.Join("|", args), Encoding.UTF8);
                }

                if (HasArgument(args, "--hook"))
                {
                    RunHook();
                    return;
                }

                if (HasArgument(args, "--play"))
                {
                    RunPlayback();
                    Environment.Exit(0);
                    return;
                }

                if (HasArgument(args, "--status"))
                {
                    EnableConsoleOutput();
                    PrintStatus();
                    return;
                }

                if (HasArgument(args, "--self-test"))
                {
                    EnableConsoleOutput();
                    RunSelfTest();
                    return;
                }

                if (HasArgument(args, "--install-hook"))
                {
                    EnableConsoleOutput();
                    EnsureDefaultSound();
                    HookManager.Install(InstalledExePath);
                    Console.WriteLine("Hook installed.");
                    return;
                }

                if (HasArgument(args, "--uninstall-hook"))
                {
                    EnableConsoleOutput();
                    HookManager.Uninstall();
                    Console.WriteLine("Hook removed.");
                    return;
                }

                string enabledValue;
                if (TryGetArgumentValue(args, "--set-enabled=", out enabledValue))
                {
                    EnableConsoleOutput();
                    bool enabled = enabledValue == "1" || enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    Settings settings = SettingsStore.Load();
                    settings.Enabled = enabled;
                    SettingsStore.Save(settings);
                    Console.WriteLine(enabled ? "ENABLED" : "DISABLED");
                    return;
                }

                bool relaunched = DeploymentManager.EnsureInstalledAndRelaunch(args);
                if (relaunched)
                {
                    return;
                }

                bool firstRun = FirstRunManager.Initialize();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool createdNew;
                using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        MessageBox.Show(
                            ProductName + " 已经在运行。\r\n请查看 Windows 右下角系统托盘。",
                            ProductName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    using (TrayApplicationContext context = new TrayApplicationContext(firstRun))
                    {
                        Application.Run(context);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorLog = Environment.GetEnvironmentVariable("BIGDOGBARK_ERROR_LOG");
                if (!string.IsNullOrWhiteSpace(errorLog))
                {
                    try
                    {
                        File.WriteAllText(errorLog, ex.ToString(), Encoding.UTF8);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    if (string.IsNullOrWhiteSpace(errorLog))
                    {
                        MessageBox.Show(
                            ProductName + " 启动失败：\r\n" + ex.Message,
                            ProductName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                catch
                {
                }
            }
        }

        private static void RunHook()
        {
            Settings settings = SettingsStore.Load();
            if (!settings.Enabled)
            {
                return;
            }

            string executablePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executablePath;
            startInfo.Arguments = "--play";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);
        }

        private static void RunPlayback()
        {
            Settings settings = SettingsStore.Load();
            string soundPath = settings.SoundPath;

            if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
            {
                EnsureDefaultSound();
                soundPath = DefaultSoundPath;
            }

            PlaySound(soundPath);
        }

        private static void PlaySound(string soundPath)
        {
            MediaPlayer player = null;
            try
            {
                player = new MediaPlayer();
                player.Volume = 1.0;
                player.Open(new Uri(soundPath, UriKind.Absolute));

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (!player.NaturalDuration.HasTimeSpan && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(50);
                }

                player.Play();

                int waitMilliseconds = 3000;
                if (player.NaturalDuration.HasTimeSpan)
                {
                    waitMilliseconds = (int)Math.Ceiling(
                        player.NaturalDuration.TimeSpan.TotalMilliseconds) + 250;
                }

                Thread.Sleep(waitMilliseconds);
            }
            catch
            {
                try
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    Thread.Sleep(700);
                }
                catch
                {
                }
            }
            finally
            {
                if (player != null)
                {
                    try
                    {
                        player.Stop();
                        player.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void EnsureDefaultSound()
        {
            Directory.CreateDirectory(AppDirectory);

            if (File.Exists(DefaultSoundPath))
            {
                return;
            }

            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("BarkSound"))
            {
                if (resource == null)
                {
                    throw new InvalidOperationException("Embedded default sound was not found.");
                }

                using (FileStream output = File.Create(DefaultSoundPath))
                {
                    resource.CopyTo(output);
                }
            }
        }

        private static void PrintStatus()
        {
            Settings settings = SettingsStore.Load();
            Console.WriteLine("ENABLED=" + (settings.Enabled ? "1" : "0"));
            Console.WriteLine("SOUND=" + settings.SoundPath);
            Console.WriteLine("EXE=" + InstalledExePath);
            Console.WriteLine("HOOK=" + (HookManager.IsInstalled(InstalledExePath) ? "1" : "0"));
        }

        private static void RunSelfTest()
        {
            Directory.CreateDirectory(AppDirectory);
            EnsureDefaultSound();

            Settings settings = SettingsStore.Load();
            settings.SoundPath = DefaultSoundPath;
            settings.Enabled = true;
            SettingsStore.Save(settings);

            string executablePath = Assembly.GetExecutingAssembly().Location;
            HookManager.Install(executablePath);

            if (!File.Exists(DefaultSoundPath))
            {
                throw new InvalidOperationException("Default sound extraction failed.");
            }

            if (!HookManager.IsInstalled(executablePath))
            {
                throw new InvalidOperationException("Hook installation failed.");
            }

            Console.WriteLine("SELF_TEST_OK");
            Console.WriteLine("SOUND_BYTES=" + new FileInfo(DefaultSoundPath).Length);
            Console.WriteLine("HOOK_PATH=" + HookManager.HookPath);
        }

        private static void EnableConsoleOutput()
        {
            try
            {
                AttachConsole(-1);
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8);
                writer.AutoFlush = true;
                Console.SetOut(writer);
            }
            catch
            {
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetArgumentValue(string[] args, string prefix, out string value)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string GetEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return value == null ? string.Empty : value.Trim();
        }

        internal static string GetAppDirectory()
        {
            return AppDirectory;
        }

        internal static string GetInstalledExePath()
        {
            return InstalledExePath;
        }

        internal static string GetDefaultSoundPath()
        {
            return DefaultSoundPath;
        }

        internal static string GetRepositoryUrl()
        {
            return RepositoryUrl;
        }

        internal static string GetProductName()
        {
            return ProductName;
        }

        internal static string GetEnglishName()
        {
            return EnglishName;
        }

        internal static string QuoteExecutable(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        internal static void ExtractDefaultSoundIfNeeded()
        {
            EnsureDefaultSound();
        }

        internal static void PreviewSound()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Assembly.GetExecutingAssembly().Location;
            startInfo.Arguments = "--play";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);
        }

        internal static bool GetEnabled()
        {
            return SettingsStore.Load().Enabled;
        }

        internal static void SetEnabled(bool enabled)
        {
            Settings settings = SettingsStore.Load();
            settings.Enabled = enabled;
            SettingsStore.Save(settings);
        }

        internal static string GetSoundPath()
        {
            return SettingsStore.Load().SoundPath;
        }

        internal static void SetSoundPath(string soundPath)
        {
            Settings settings = SettingsStore.Load();
            settings.SoundPath = soundPath;
            SettingsStore.Save(settings);
        }

        internal static string GetStartupRegistryName()
        {
            return StartupRegistryName;
        }
    }

    public sealed class Settings
    {
        public bool Enabled { get; set; }

        public string SoundPath { get; set; }
    }

    internal static class SettingsStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        internal static Settings Load()
        {
            try
            {
                if (File.Exists(Program.GetAppDirectory() + "\\settings.json"))
                {
                    string json = File.ReadAllText(Path.Combine(Program.GetAppDirectory(), "settings.json"), Encoding.UTF8);
                    Settings settings = Serializer.Deserialize<Settings>(json);
                    if (settings != null)
                    {
                        Normalize(settings);
                        return settings;
                    }
                }
            }
            catch
            {
            }

            Settings fallback = new Settings();
            fallback.Enabled = true;
            fallback.SoundPath = null;
            Normalize(fallback);
            return fallback;
        }

        internal static void Save(Settings settings)
        {
            Normalize(settings);
            Directory.CreateDirectory(Program.GetAppDirectory());
            string json = Serializer.Serialize(settings);
            File.WriteAllText(Path.Combine(Program.GetAppDirectory(), "settings.json"), json, new UTF8Encoding(false));
        }

        private static void Normalize(Settings settings)
        {
            if (settings.SoundPath == null)
            {
                settings.SoundPath = string.Empty;
            }

            Program.ExtractDefaultSoundIfNeeded();

            if (string.IsNullOrWhiteSpace(settings.SoundPath) || !File.Exists(settings.SoundPath))
            {
                settings.SoundPath = Program.GetDefaultSoundPath();
            }
        }
    }

    internal static class DeploymentManager
    {
        internal static bool EnsureInstalledAndRelaunch(string[] originalArgs)
        {
            if (GetEnvironment("BIGDOGBARK_NO_DEPLOY") == "1")
            {
                return false;
            }

            string currentPath = Assembly.GetExecutingAssembly().Location;
            string installedPath = Program.GetInstalledExePath();

            if (PathsEqual(currentPath, installedPath))
            {
                return false;
            }

            Directory.CreateDirectory(Program.GetAppDirectory());

            string temporaryPath = installedPath + ".new";
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            File.Copy(currentPath, temporaryPath, true);

            try
            {
                if (File.Exists(installedPath))
                {
                    File.Delete(installedPath);
                }

                File.Move(temporaryPath, installedPath);
            }
            catch
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                File.Copy(currentPath, installedPath, true);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = installedPath;
            startInfo.Arguments = BuildArguments(originalArgs, "--deployed");
            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = Program.GetAppDirectory();
            Process.Start(startInfo);
            return true;
        }

        private static string BuildArguments(string[] originalArgs, string extraArgument)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(extraArgument);

            foreach (string arg in originalArgs)
            {
                if (string.Equals(arg, "--deployed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                builder.Append(' ');
                builder.Append(arg);
            }

            return builder.ToString();
        }

        private static bool PathsEqual(string first, string second)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(first).TrimEnd('\\'),
                    Path.GetFullPath(second).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return value == null ? string.Empty : value.Trim();
        }
    }

    internal static class FirstRunManager
    {
        internal static bool Initialize()
        {
            bool firstRun = !File.Exists(Path.Combine(Program.GetAppDirectory(), "settings.json"));

            Settings settings = SettingsStore.Load();
            settings.Enabled = firstRun ? true : settings.Enabled;
            SettingsStore.Save(settings);

            if (firstRun)
            {
                try
                {
                    HookManager.Install(Program.GetInstalledExePath());
                }
                catch
                {
                }
            }

            return firstRun;
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip menu;
        private readonly Icon iconOn;
        private readonly Icon iconOff;
        private readonly ToolStripMenuItem customSoundItem;
        private readonly ToolStripMenuItem defaultNotificationItem;
        private readonly ToolStripMenuItem startupItem;

        internal TrayApplicationContext(bool firstRun)
        {
            iconOn = LoadIcon("IconOn");
            iconOff = LoadIcon("IconOff");

            customSoundItem = new ToolStripMenuItem("使用自定义提示音");
            defaultNotificationItem = new ToolStripMenuItem("使用 Codex 默认通知");
            startupItem = new ToolStripMenuItem("开机自动运行");

            customSoundItem.Click += delegate { SetEnabled(true); };
            defaultNotificationItem.Click += delegate { SetEnabled(false); };

            ToolStripMenuItem previewItem = new ToolStripMenuItem("试听提示音");
            previewItem.Click += delegate { Program.PreviewSound(); };

            ToolStripMenuItem changeSoundItem = new ToolStripMenuItem("更换提示音...");
            changeSoundItem.Click += delegate { ChangeSound(); };

            ToolStripMenuItem resetSoundItem = new ToolStripMenuItem("恢复内置提示音");
            resetSoundItem.Click += delegate { ResetSound(); };

            ToolStripMenuItem installHookItem = new ToolStripMenuItem("安装/修复 Codex Hook");
            installHookItem.Click += delegate { InstallHook(); };

            ToolStripMenuItem removeHookItem = new ToolStripMenuItem("移除 Codex Hook");
            removeHookItem.Click += delegate { RemoveHook(); };

            startupItem.Click += delegate
            {
                StartupManager.SetEnabled(!StartupManager.IsEnabled());
                UpdateUi();
            };

            ToolStripMenuItem openFolderItem = new ToolStripMenuItem("打开程序目录");
            openFolderItem.Click += delegate
            {
                Process.Start("explorer.exe", Program.GetAppDirectory());
            };

            ToolStripMenuItem aboutItem = new ToolStripMenuItem("关于");
            aboutItem.Click += delegate
            {
                MessageBox.Show(
                    Program.GetProductName() + "\r\n" + Program.GetEnglishName() +
                    "\r\n\r\n" + Program.GetRepositoryUrl(),
                    "关于",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };

            menu = new ContextMenuStrip();
            menu.Items.Add(customSoundItem);
            menu.Items.Add(defaultNotificationItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(previewItem);
            menu.Items.Add(changeSoundItem);
            menu.Items.Add(resetSoundItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(installHookItem);
            menu.Items.Add(removeHookItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add(openFolderItem);
            menu.Items.Add(aboutItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    SetEnabled(!Program.GetEnabled());
                }
            };

            UpdateUi();

            if (firstRun)
            {
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 1200;
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();
                    notifyIcon.BalloonTipTitle = Program.GetProductName();
                    notifyIcon.BalloonTipText = "已开启。重启 Codex 后，如提示 Hook 未信任，请运行 /hooks 信任一次。";
                    notifyIcon.ShowBalloonTip(5000);
                };
                timer.Start();
            }
        }

        private void SetEnabled(bool enabled)
        {
            Program.SetEnabled(enabled);
            UpdateUi();
        }

        private void ChangeSound()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 Codex 完成提示音";
                dialog.Filter = "音频文件|*.wav;*.mp3;*.m4a;*.aac;*.wma|所有文件|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    string extension = Path.GetExtension(dialog.FileName);
                    if (string.IsNullOrWhiteSpace(extension))
                    {
                        extension = ".wav";
                    }

                    string targetPath = Path.Combine(Program.GetAppDirectory(), "custom" + extension.ToLowerInvariant());
                    File.Copy(dialog.FileName, targetPath, true);
                    Program.SetSoundPath(targetPath);
                    Program.PreviewSound();
                    UpdateUi();

                    notifyIcon.BalloonTipTitle = Program.GetProductName();
                    notifyIcon.BalloonTipText = "提示音已更换。";
                    notifyIcon.ShowBalloonTip(3000);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "无法导入该音频：\r\n" + ex.Message,
                        Program.GetProductName(),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ResetSound()
        {
            Program.ExtractDefaultSoundIfNeeded();
            Program.SetSoundPath(Program.GetDefaultSoundPath());
            UpdateUi();

            notifyIcon.BalloonTipTitle = Program.GetProductName();
            notifyIcon.BalloonTipText = "已恢复内置提示音。";
            notifyIcon.ShowBalloonTip(3000);
        }

        private void InstallHook()
        {
            try
            {
                HookManager.Install(Program.GetInstalledExePath());
                MessageBox.Show(
                    "Hook 已安装。\r\n\r\n请重启 Codex，并在 /hooks 中信任一次。",
                    Program.GetProductName(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Hook 安装失败：\r\n" + ex.Message,
                    Program.GetProductName(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RemoveHook()
        {
            try
            {
                HookManager.Uninstall();
                MessageBox.Show(
                    "本项目的 Hook 已移除。",
                    Program.GetProductName(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Hook 移除失败：\r\n" + ex.Message,
                    Program.GetProductName(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateUi()
        {
            bool enabled = Program.GetEnabled();
            notifyIcon.Icon = enabled ? iconOn : iconOff;
            notifyIcon.Text = enabled
                ? Program.GetProductName() + " - 自定义提示音已开启"
                : Program.GetProductName() + " - 使用默认通知";

            customSoundItem.Checked = enabled;
            defaultNotificationItem.Checked = !enabled;
            startupItem.Checked = StartupManager.IsEnabled();
        }

        private void ExitApplication()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            menu.Dispose();
            iconOn.Dispose();
            iconOff.Dispose();
            ExitThread();
        }

        private static Icon LoadIcon(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return (Icon)SystemIcons.Application.Clone();
                }

                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    memory.Position = 0;
                    return new Icon(memory);
                }
            }
        }
    }

    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal static bool IsEnabled()
        {
            try
            {
                object value = Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey(RunKeyPath)
                    .GetValue(Program.GetStartupRegistryName());
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                {
                    key.SetValue(
                        Program.GetStartupRegistryName(),
                        Program.QuoteExecutable(Program.GetInstalledExePath()) + " --tray");
                }
                else
                {
                    key.DeleteValue(Program.GetStartupRegistryName(), false);
                }
            }
        }
    }

    internal static class HookManager
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        internal static string HookPath
        {
            get
            {
                string codexHome = Environment.GetEnvironmentVariable("BIGDOGBARK_CODEX_HOME");
                if (string.IsNullOrWhiteSpace(codexHome))
                {
                    codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
                }

                if (string.IsNullOrWhiteSpace(codexHome))
                {
                    codexHome = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".codex");
                }

                return Path.Combine(codexHome, "hooks.json");
            }
        }

        internal static void Install(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("Executable not found.", executablePath);
            }

            Dictionary<string, object> root = ReadRoot();
            Dictionary<string, object> hooks = GetObject(root, "hooks");
            List<object> stopGroups = GetArrayAsList(hooks, "Stop");
            stopGroups = RemoveOurHandlers(stopGroups);
            stopGroups.Add(CreateHookGroup(executablePath));
            hooks["Stop"] = stopGroups.ToArray();
            root["hooks"] = hooks;

            WriteRoot(root);
        }

        internal static void Uninstall()
        {
            Dictionary<string, object> root = ReadRoot();
            Dictionary<string, object> hooks = GetObject(root, "hooks");
            List<object> stopGroups = RemoveOurHandlers(GetArrayAsList(hooks, "Stop"));
            hooks["Stop"] = stopGroups.ToArray();
            root["hooks"] = hooks;
            WriteRoot(root);
        }

        internal static bool IsInstalled(string executablePath)
        {
            try
            {
                Dictionary<string, object> root = ReadRoot();
                Dictionary<string, object> hooks = GetObject(root, "hooks");
                foreach (object group in GetArrayAsList(hooks, "Stop"))
                {
                    if (ContainsOurHook(group))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static Dictionary<string, object> CreateHookGroup(string executablePath)
        {
            Dictionary<string, object> handler = new Dictionary<string, object>();
            handler["type"] = "command";
            handler["command"] = Program.QuoteExecutable(executablePath) + " --hook";
            handler["timeout"] = 5;

            Dictionary<string, object> group = new Dictionary<string, object>();
            group["hooks"] = new object[] { handler };
            return group;
        }

        private static List<object> RemoveOurHandlers(List<object> groups)
        {
            List<object> keptGroups = new List<object>();

            foreach (object groupValue in groups)
            {
                Dictionary<string, object> group = groupValue as Dictionary<string, object>;
                if (group == null)
                {
                    if (!ContainsOurHook(groupValue))
                    {
                        keptGroups.Add(groupValue);
                    }
                    continue;
                }

                List<object> handlers = GetArrayAsList(group, "hooks");
                List<object> keptHandlers = new List<object>();
                foreach (object handler in handlers)
                {
                    if (!ContainsOurHook(handler))
                    {
                        keptHandlers.Add(handler);
                    }
                }

                if (keptHandlers.Count > 0)
                {
                    group["hooks"] = keptHandlers.ToArray();
                    keptGroups.Add(group);
                }
            }

            return keptGroups;
        }

        private static bool ContainsOurHook(object value)
        {
            string text = value as string;
            if (text != null)
            {
                return text.IndexOf("BigDogBark.exe", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("--hook", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                foreach (KeyValuePair<string, object> pair in dictionary)
                {
                    if (ContainsOurHook(pair.Value))
                    {
                        return true;
                    }
                }
                return false;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object item in enumerable)
                {
                    if (ContainsOurHook(item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<string, object> ReadRoot()
        {
            string path = HookPath;
            if (!File.Exists(path))
            {
                return new Dictionary<string, object>();
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, object>();
            }

            Dictionary<string, object> root = Serializer.Deserialize<Dictionary<string, object>>(json);
            if (root == null)
            {
                throw new InvalidOperationException("hooks.json is not a JSON object.");
            }

            return root;
        }

        private static void WriteRoot(Dictionary<string, object> root)
        {
            string path = HookPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (File.Exists(path))
            {
                string backupPath = path + ".bak.BigDogBark." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, backupPath, true);
            }

            string json = JsonPrettyWriter.Write(root);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> parent, string key)
        {
            Dictionary<string, object> value = parent.ContainsKey(key)
                ? parent[key] as Dictionary<string, object>
                : null;
            return value ?? new Dictionary<string, object>();
        }

        private static List<object> GetArrayAsList(Dictionary<string, object> parent, string key)
        {
            if (!parent.ContainsKey(key))
            {
                return new List<object>();
            }

            IEnumerable enumerable = parent[key] as IEnumerable;
            if (enumerable == null || parent[key] is string)
            {
                return new List<object>();
            }

            List<object> result = new List<object>();
            foreach (object item in enumerable)
            {
                result.Add(item);
            }

            return result;
        }
    }

    internal static class JsonPrettyWriter
    {
        internal static string Write(object value)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value, 0);
            builder.AppendLine();
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value, int indent)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string text = value as string;
            if (text != null)
            {
                builder.Append(ToJsonString(text));
                return;
            }

            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                WriteDictionary(builder, dictionary, indent);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                WriteEnumerable(builder, enumerable, indent);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(ToJsonString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static void WriteDictionary(StringBuilder builder, Dictionary<string, object> dictionary, int indent)
        {
            builder.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                builder.AppendLine();
                AppendIndent(builder, indent + 1);
                builder.Append(ToJsonString(pair.Key));
                builder.Append(": ");
                WriteValue(builder, pair.Value, indent + 1);
            }

            if (!first)
            {
                builder.AppendLine();
                AppendIndent(builder, indent);
            }
            builder.Append('}');
        }

        private static void WriteEnumerable(StringBuilder builder, IEnumerable values, int indent)
        {
            List<object> items = new List<object>();
            foreach (object item in values)
            {
                items.Add(item);
            }

            builder.Append('[');
            for (int index = 0; index < items.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.AppendLine();
                AppendIndent(builder, indent + 1);
                WriteValue(builder, items[index], indent + 1);
            }

            if (items.Count > 0)
            {
                builder.AppendLine();
                AppendIndent(builder, indent);
            }
            builder.Append(']');
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            builder.Append(' ', indent * 2);
        }

        private static string ToJsonString(string value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(value);
        }
    }
}
