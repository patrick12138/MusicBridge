using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MusicBridge
{
    #region WinAPI 定义 (包含嵌入所需API)
    // Windows API和常量
    public static class WinAPI
    {
        // --- Windows 消息常量 ---
        public const int WM_APPCOMMAND = 0x0319;    // 用于向窗口发送媒体控制命令的Windows消息
        public const int WM_CLOSE = 0x0010;       // 关闭窗口消息
        public const int WM_DESTROY = 0x0002;     // 销毁窗口消息
        public const int WM_SIZE = 0x0005;        // 窗口大小改变消息

        // --- APPCOMMAND 常量 (用于 WM_APPCOMMAND 消息) ---
        public const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14 << 16;
        public const int APPCOMMAND_MEDIA_NEXTTRACK = 11 << 16;
        public const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12 << 16;
        public const int APPCOMMAND_VOLUME_MUTE = 8 << 16;
        public const int APPCOMMAND_VOLUME_DOWN = 9 << 16;
        public const int APPCOMMAND_VOLUME_UP = 10 << 16;

        // --- Window Styles (用于修改窗口样式) ---
        public const uint WS_CHILD = 0x40000000;       // 子窗口样式
        public const uint WS_VISIBLE = 0x10000000;     // 可见样式
        public const uint WS_CLIPSIBLINGS = 0x04000000; // 裁剪兄弟窗口区域
        public const uint WS_CLIPCHILDREN = 0x02000000; // 裁剪子窗口区域
        public const uint WS_POPUP = 0x80000000;       // 弹出窗口样式
        public const uint WS_CAPTION = 0x00C00000;     // 标题栏
        public const uint WS_BORDER = 0x00800000;     // 边框
        public const uint WS_DLGFRAME = 0x00400000;     // 对话框边框
        public const uint WS_THICKFRAME = 0x00040000;     // 可调整大小的边框 (Sizing Border)

        // --- Get/SetWindowLongPtr 索引 ---
        public const int GWL_STYLE = -16;             // 获取/设置窗口样式
        public const int GWL_EXSTYLE = -20;           // 获取/设置扩展窗口样式
        public const int GWLP_HWNDPARENT = -8;        // 获取/设置父窗口句柄 (仅用于 SetWindowLongPtr)

        // --- SetWindowPos Flags (用于调整窗口位置和状态) ---
        public const uint SWP_NOSIZE = 0x0001;         // 忽略 cx, cy 参数，保持大小
        public const uint SWP_NOMOVE = 0x0002;         // 忽略 X, Y 参数，保持位置
        public const uint SWP_NOZORDER = 0x0004;       // 保持 Z 顺序
        public const uint SWP_FRAMECHANGED = 0x0020;   // 应用 SetWindowLongPtr 后的样式更改，强制重绘边框
        public const uint SWP_SHOWWINDOW = 0x0040;     // 显示窗口
        public const uint SWP_NOACTIVATE = 0x0010;     // 不激活窗口

        // --- ShowWindow 命令 ---
        public const int SW_RESTORE = 9;              // 还原窗口

        // --- Windows API 函数导入 ---
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd); // 检查窗口句柄是否有效

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // --- 用于嵌入窗口的 API ---
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent); // 设置父窗口

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint); // 移动并调整窗口大小

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags); // 设置窗口位置、大小和Z顺序

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        // 自动选择32/64位 GetWindowLongPtr
        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 自动选择32/64位 SetWindowLongPtr
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
           uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
           int x, int y, int nWidth, int nHeight,
           IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam); // 创建窗口 (用于 HwndHost)

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hwnd); // 销毁窗口 (用于 HwndHost)

        // 查找主窗口方法 (保持不变，用于初始查找)
        public static IntPtr FindMainWindow(string processName)
        {
            IntPtr foundHwnd = IntPtr.Zero;
            int maxTitleLen = 0;
            List<IntPtr> potentialHwnds = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return true;

                try
                {
                    using (Process proc = Process.GetProcessById((int)pid))
                    {
                        if (proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                        {
                            potentialHwnds.Add(hWnd);
                            int currentTitleLen = GetWindowTextLength(hWnd);
                            if (currentTitleLen > maxTitleLen)
                            {
                                maxTitleLen = currentTitleLen;
                                foundHwnd = hWnd;
                            }
                        }
                    }
                }
                catch { /* 忽略查找过程中的进程退出等错误 */ }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != IntPtr.Zero) return foundHwnd;
            if (potentialHwnds.Count == 1) return potentialHwnds[0];
            return IntPtr.Zero;
        }
    }
    #endregion

    #region 音乐控制器接口和实现 (简化控制逻辑)
    // 媒体命令枚举
    public enum MediaCommand
    {
        PlayPause, NextTrack, PreviousTrack, VolumeMute, VolumeDown, VolumeUp
    }

    // 音乐软件控制器接口
    public interface IMusicAppController
    {
        string Name { get; }          // 应用名称
        string ProcessName { get; }   // 进程名称
        string? ExecutablePath { get; } // 可执行文件路径
        bool IsRunning();             // 检查是否运行
        Task LaunchAsync();           // 启动应用 (异步)
        Task CloseAppAsync();         // 关闭应用 (异步)
        // 控制命令现在直接针对 HWND 发送消息，不再需要复杂模拟
        Task SendCommandAsync(IntPtr targetHwnd, MediaCommand command);
        string GetCurrentSong(IntPtr targetHwnd); // 获取歌曲信息也需要目标 HWND
        Task<string?> FindExecutablePathAsync(); // 查找路径
    }

    // 控制器基类 (保持大部分查找和启停逻辑)
    public abstract class MusicAppControllerBase : IMusicAppController
    {
        public abstract string Name { get; }
        public abstract string ProcessName { get; }
        private string? _executablePath = null;
        private bool _pathSearched = false;
        public string? ExecutablePath => _executablePath;
        protected abstract string DefaultExeName { get; }

        public virtual bool IsRunning() => Process.GetProcessesByName(ProcessName).Length > 0;

        // --- 查找、启动、关闭逻辑基本不变 ---
        public async Task<string?> FindExecutablePathAsync()
        {
            if (_executablePath != null) return _executablePath;
            if (_pathSearched) return null;
            Debug.WriteLine($"[{Name}] 开始查找路径...");
            string? path = await Task.Run(() => FindPathFromRegistry());
            if (path == null) path = CheckDefaultInstallLocations();
            _executablePath = path;
            _pathSearched = true;
            Debug.WriteLine($"[{Name}] 查找路径结束，结果: {_executablePath ?? "未找到"}");
            return _executablePath;
        }

        // 从注册表查找路径的具体实现
        public string? FindPathFromRegistry()
        {
            // 尝试在不同的注册表位置查找应用的卸载信息
            string? path = SearchRegistryUninstallKey(Registry.CurrentUser, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall") ??
                           SearchRegistryUninstallKey(Registry.LocalMachine, $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall") ??
                           SearchRegistryUninstallKey(Registry.LocalMachine, $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"); // 针对64位系统上的32位应用

            // 有些应用可能在 HKCU 的 WOW6432Node 下（不常见，但可以加上）
            if (path == null)
            {
                path = SearchRegistryUninstallKey(Registry.CurrentUser, $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            }

            return path;
        }

        // 搜索单个注册表根键下的卸载项
        public string? SearchRegistryUninstallKey(RegistryKey baseKey, string keyPath)
        {
            try
            {
                using (RegistryKey? key = baseKey.OpenSubKey(keyPath))
                {
                    if (key == null) return null;

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey? subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey == null) continue;

                            object? displayNameObj = subKey.GetValue("DisplayName");
                            object? installLocationObj = subKey.GetValue("InstallLocation");
                            object? displayIconObj = subKey.GetValue("DisplayIcon");

                            // 优先匹配 DisplayName
                            if (displayNameObj != null && displayNameObj.ToString()?.IndexOf(Name, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // 尝试使用 InstallLocation 拼接默认 Exe 名称
                                if (installLocationObj != null && !string.IsNullOrWhiteSpace(installLocationObj.ToString()))
                                {
                                    string potentialPath = Path.Combine(installLocationObj.ToString()!, DefaultExeName);
                                    if (File.Exists(potentialPath)) return potentialPath;
                                    // 有些应用的 InstallLocation 可能不包含子目录，需要额外尝试
                                    // 例如 酷狗的 InstallLocation 可能是 C:\Program Files (x86)\KuGou\, 需要拼接 KGMusic\KuGou.exe
                                    // 这部分逻辑可以在子类中覆盖 FindPathFromRegistry() 来实现
                                }
                                // 尝试从 DisplayIcon 提取路径
                                if (displayIconObj != null && !string.IsNullOrWhiteSpace(displayIconObj.ToString()))
                                {
                                    string iconPath = displayIconObj.ToString()!.Trim('"');
                                    // DisplayIcon 可能包含参数 ",0" 等，需要移除
                                    string potentialPath = iconPath.Split(',')[0];
                                    if (File.Exists(potentialPath) && Path.GetExtension(potentialPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        return potentialPath;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[注册表搜索错误] 搜索 {baseKey.Name}\\{keyPath} 时出错: {ex.Message}");
            }
            return null;
        }

        // 检查默认安装位置 (需要子类根据情况实现)
        public virtual string? CheckDefaultInstallLocations()
        {
            // 子类可以重写此方法来检查 C:\Program Files, AppData 等
            return null;
        }


        public virtual async Task LaunchAsync()
        {
            if (IsRunning()) return;
            if (!_pathSearched) await FindExecutablePathAsync();
            if (string.IsNullOrEmpty(_executablePath) || !File.Exists(_executablePath))
            {
                MessageBox.Show($"无法找到 {Name} 的可执行文件路径。", "启动失败", MessageBoxButton.OK);
                return;
            }
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = _executablePath;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                }
                await Task.Delay(500); // 短暂等待
                Debug.WriteLine($"[{Name}] 启动命令已发送。");
            }
            catch (Exception ex) { /* ... (错误处理) ... */ }
        }

        public virtual async Task CloseAppAsync()
        {
            // --- 保持之前的优雅关闭逻辑 ---
            Process[] processes = Process.GetProcessesByName(ProcessName);
            if (processes.Length == 0) return;
            Debug.WriteLine($"[{Name}] 尝试关闭 {processes.Length} 个进程...");
            foreach (Process process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            if (process.CloseMainWindow())
                            {
                                if (await Task.Run(() => process.WaitForExit(3000))) { /* 成功关闭 */ }
                                else { process.Kill(); /* 超时强制关闭 */ }
                            }
                            else { process.Kill(); /* 无法发送消息则强制关闭 */ }
                        }
                    }
                    catch { /* 忽略关闭过程中的错误 */ }
                }
            }
            await Task.Delay(500);
        }


        // --- 控制和获取信息逻辑改变 ---

        // 发送命令：直接向目标窗口发送 WM_APPCOMMAND 消息
        public virtual async Task SendCommandAsync(IntPtr targetHwnd, MediaCommand command)
        {
            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 失败：目标窗口句柄 ({targetHwnd}) 无效。");
                return;
            }

            int? appCommand = command switch
            {
                MediaCommand.PlayPause => WinAPI.APPCOMMAND_MEDIA_PLAY_PAUSE,
                MediaCommand.NextTrack => WinAPI.APPCOMMAND_MEDIA_NEXTTRACK,
                MediaCommand.PreviousTrack => WinAPI.APPCOMMAND_MEDIA_PREVIOUSTRACK,
                MediaCommand.VolumeMute => WinAPI.APPCOMMAND_VOLUME_MUTE,
                MediaCommand.VolumeDown => WinAPI.APPCOMMAND_VOLUME_DOWN,
                MediaCommand.VolumeUp => WinAPI.APPCOMMAND_VOLUME_UP,
                _ => null
            };

            if (appCommand.HasValue)
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 发送 WM_APPCOMMAND ({command}) 到 HWND: {targetHwnd}");
                WinAPI.SendMessageW(targetHwnd, WinAPI.WM_APPCOMMAND, targetHwnd, (IntPtr)appCommand.Value);
                // SendMessage 是同步的，但命令执行是异步的，稍作等待有助于界面响应
                await Task.Delay(50);
            }
            else
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 收到无效的命令: {command}");
            }
            // **不再需要键盘模拟逻辑**
        }

        // 获取当前歌曲：从目标窗口标题获取
        public virtual string GetCurrentSong(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                return "无"; // 窗口无效
            }

            int length = WinAPI.GetWindowTextLength(targetHwnd);
            if (length > 0)
            {
                StringBuilder sb = new StringBuilder(length + 1);
                WinAPI.GetWindowText(targetHwnd, sb, sb.Capacity);
                string title = sb.ToString();
                return CleanWindowTitle(title); // 使用辅助方法清理标题
            }
            return "无"; // 没有标题或获取失败
        }

        // 清理窗口标题的辅助方法
        private string CleanWindowTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "无";

            // 尝试移除常见的应用名后缀
            string[] suffixes = { $"- {Name}", "- 网易云音乐", "- QQ音乐", "- 酷狗音乐", "酷狗音乐", "QQ音乐", "网易云音乐" };
            foreach (var suffix in suffixes)
            {
                // 使用 LastIndexOf 确保移除的是末尾的后缀
                int index = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                // 检查是否确实是后缀，并且不是标题的主要部分
                if (index > 0 && index == title.Length - suffix.Length)
                {
                    return title.Substring(0, index).Trim();
                }
                // 有些标题可能只有应用名，例如酷狗启动时
                if (title.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return "就绪"; // 或者返回空字符串
                }
            }
            return title.Trim(); // 没有匹配到后缀，返回原始标题
        }
    }

    // --- 具体控制器实现 (基本只需继承基类) ---
    public class QQMusicController : MusicAppControllerBase
    {
        public override string Name => "QQ音乐";
        public override string ProcessName => "QQMusic";
        protected override string DefaultExeName => "QQMusic.exe";
        public override string? CheckDefaultInstallLocations()
        {
            string path86 = @"C:\Program Files (x86)\Tencent\QQMusic\QQMusic.exe";
            string path64 = @"C:\Program Files\Tencent\QQMusic\QQMusic.exe"; // 可能的64位路径
            if (File.Exists(path86)) return path86;
            if (File.Exists(path64)) return path64;
            // 还可以检查 AppData 等路径
            return base.CheckDefaultInstallLocations();
        }
    }

    public class KugouMusicController : MusicAppControllerBase
    {
        public override string Name => "酷狗音乐";
        public override string ProcessName => "KuGou";
        protected override string DefaultExeName => "KuGou.exe";
        // 酷狗的注册表路径可能需要特殊处理
        //protected override string? FindPathFromRegistry()
        //{
        //    // 尝试基类逻辑
        //    string? installLocation = base.FindPathFromRegistry();
        //    if (installLocation != null)
        //    {
        //        // 基类找到的可能是exe文件
        //        if (File.Exists(installLocation) && Path.GetFileName(installLocation).Equals(DefaultExeName, StringComparison.OrdinalIgnoreCase))
        //            return installLocation;
        //        // 基类找到的可能是目录
        //        if (Directory.Exists(installLocation))
        //        {
        //            string potentialPath = Path.Combine(installLocation, "KGMusic", DefaultExeName); // 尝试子目录
        //            if (File.Exists(potentialPath)) return potentialPath;
        //            potentialPath = Path.Combine(installLocation, DefaultExeName); // 尝试根目录
        //            if (File.Exists(potentialPath)) return potentialPath;
        //        }
        //    }
        //    // 添加其他酷狗特定的查找逻辑...
        //    return null;
        //}
    }

    public class NeteaseMusicController : MusicAppControllerBase
    {
        public override string Name => "网易云音乐";
        public override string ProcessName => "cloudmusic";
        protected override string DefaultExeName => "cloudmusic.exe";
        //protected override string? CheckDefaultInstallLocations()
        //{
        //    // 检查 Program Files 和 AppData
        //    string pf86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Netease\CloudMusic", DefaultExeName);
        //    string pf64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Netease\CloudMusic", DefaultExeName);
        //    string appDataLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Netease\CloudMusic", DefaultExeName);
        //    if (File.Exists(pf86)) return pf86;
        //    if (File.Exists(pf64)) return pf64;
        //    if (File.Exists(appDataLocal)) return appDataLocal;
        //    return base.CheckDefaultInstallLocations();
        //}
        //protected override string? FindPathFromRegistry() { /* ... */ return base.FindPathFromRegistry(); /* 或网易云特定逻辑 */ }
    }
    #endregion

    #region MainWindow 主窗口逻辑 (集成嵌入功能)
    public partial class MainWindow : Window
    {
        private IMusicAppController? currentController; // 当前选中的控制器
        private readonly List<IMusicAppController> controllers = new List<IMusicAppController>(); // 所有控制器
        private readonly DispatcherTimer statusTimer = new DispatcherTimer(); // 状态刷新定时器
        private IntPtr _embeddedWindowHandle = IntPtr.Zero; // 当前嵌入的窗口句柄

        // 构造函数
        public MainWindow()
        {
            InitializeComponent(); // 初始化 XAML 组件

            // --- 初始化控制器列表和下拉框 ---
            try
            {
                controllers.Add(new QQMusicController());
                controllers.Add(new NeteaseMusicController());
                controllers.Add(new KugouMusicController());

                foreach (var controller in controllers)
                {
                    MusicAppComboBox.Items.Add(new ComboBoxItem { Content = controller.Name });
                }
                if (MusicAppComboBox.Items.Count > 0)
                {
                    MusicAppComboBox.SelectedIndex = 0; // 默认选中第一个
                }
                else
                {
                    UpdateStatus("错误：未找到任何音乐播放器控制器。");
                }

                // 配置状态刷新定时器
                statusTimer.Interval = TimeSpan.FromSeconds(2); // 缩短刷新间隔以更快响应
                statusTimer.Tick += StatusTimer_Tick;
                statusTimer.Start();

                // 监听 AppHost 大小变化
                AppHostControl.SizeChanged += AppHostControl_SizeChanged;
                // 窗口关闭时尝试恢复嵌入的窗口
                this.Closing += MainWindow_Closing;
                // 窗口加载后执行初始操作
                Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化应用时发生严重错误: {ex.Message}", "初始化失败", MessageBoxButton.OK);
            }
        }

        // 窗口加载完成
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 异步查找所有路径
            await FindAllControllerPathsAsync();
            // 初始刷新状态和控件
            await RefreshMusicAppStatusAsync();
        }

        // 异步查找所有控制器路径
        private async Task FindAllControllerPathsAsync()
        {
            Debug.WriteLine("开始查找所有控制器路径...");
            List<Task> tasks = new List<Task>();
            foreach (var controller in controllers)
            {
                tasks.Add(controller.FindExecutablePathAsync());
            }
            await Task.WhenAll(tasks);
            Debug.WriteLine("所有控制器路径查找完成。");
            // 路径查找完成后，重新刷新一次状态，可能启动按钮现在可用了
            await RefreshMusicAppStatusAsync();
        }


        // 窗口关闭中
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 尝试恢复（分离）当前嵌入的窗口
            AppHostControl?.RestoreHostedWindow();
        }

        // AppHost 控件大小改变
        private void AppHostControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 通知 AppHost 调整内部嵌入窗口的大小
            AppHostControl?.ResizeEmbeddedWindow();
        }

        // 定时器触发
        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            // 定期刷新状态
            await RefreshMusicAppStatusAsync();

            // 额外检查：如果记录了嵌入句柄，但该窗口已失效，则自动分离
            if (_embeddedWindowHandle != IntPtr.Zero && !WinAPI.IsWindow(_embeddedWindowHandle))
            {
                Debug.WriteLine($"[定时器] 检测到嵌入窗口句柄 {_embeddedWindowHandle} 已失效，自动分离。");
                await Dispatcher.InvokeAsync(DetachEmbeddedWindow); // 确保在 UI 线程分离
            }
        }

        // 下拉框选择变化
        private async void MusicAppComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MusicAppComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                string appName = selectedItem.Content.ToString()!;
                var selectedController = controllers.FirstOrDefault(c => c.Name == appName);

                if (selectedController != null)
                {
                    // 如果切换控制器时有窗口嵌入，先分离
                    if (_embeddedWindowHandle != IntPtr.Zero)
                    {
                        DetachEmbeddedWindow();
                    }

                    currentController = selectedController;
                    UpdateStatus($"已切换到: {currentController.Name}");

                    // 确保新控制器的路径已查找 (如果需要)
                    if (currentController.ExecutablePath == null)
                    {
                        await currentController.FindExecutablePathAsync();
                    }
                    await RefreshMusicAppStatusAsync(); // 刷新新控制器的状态
                }
            }
        }

        // --- 按钮事件处理 ---

        // “启动并嵌入”按钮点击
        private async void LaunchAndEmbedButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) { UpdateStatus("请先选择播放器"); return; }

            // 如果已嵌入，不执行任何操作（或提示先分离）
            if (_embeddedWindowHandle != IntPtr.Zero)
            {
                UpdateStatus($"{currentController.Name} 已嵌入，请先分离。");
                return;
            }

            UpdateStatus($"正在启动 {currentController.Name} ...");
            SetInteractionButtonsEnabled(false); // 禁用交互按钮

            // 1. 启动进程 (如果未运行)
            if (!currentController.IsRunning())
            {
                await currentController.LaunchAsync();
                await Task.Delay(5000); // 等待较长时间让窗口创建
            }
            else
            {
                // 如果已运行，确保窗口不是最小化
                IntPtr existingHwnd = WinAPI.FindMainWindow(currentController.ProcessName);
                if (existingHwnd != IntPtr.Zero && WinAPI.IsIconic(existingHwnd))
                {
                    WinAPI.ShowWindow(existingHwnd, WinAPI.SW_RESTORE);
                    await Task.Delay(500); // 等待窗口恢复
                }
                UpdateStatus($"{currentController.Name} 已在运行，尝试查找窗口...");
            }


            // 2. 查找主窗口句柄 (尝试多次)
            IntPtr targetHwnd = IntPtr.Zero;
            for (int i = 0; i < 5; i++) // 尝试 5 次
            {
                targetHwnd = WinAPI.FindMainWindow(currentController.ProcessName);
                if (targetHwnd != IntPtr.Zero) break; // 找到即退出循环
                Debug.WriteLine($"第 {i + 1} 次查找 {currentController.Name} 窗口失败，等待 1 秒后重试...");
                await Task.Delay(1000);
            }

            // 3. 尝试嵌入
            if (targetHwnd != IntPtr.Zero)
            {
                UpdateStatus($"找到窗口 {targetHwnd}，正在嵌入...");
                bool success = false;
                // AppHost 操作需要回到 UI 线程
                await Dispatcher.InvokeAsync(() =>
                {
                    success = AppHostControl.EmbedWindow(targetHwnd);
                });

                if (success)
                {
                    _embeddedWindowHandle = targetHwnd; // 记录嵌入的句柄
                    UpdateStatus($"{currentController.Name} 已嵌入。");
                }
                else
                {
                    _embeddedWindowHandle = IntPtr.Zero;
                    UpdateStatus($"嵌入 {currentController.Name} 失败。");
                    MessageBox.Show($"嵌入 {currentController.Name} 失败。\n可能原因：\n- 权限不足 (尝试以管理员运行本程序)\n- 目标应用窗口结构不兼容\n- 目标应用有反嵌入机制", "嵌入失败", MessageBoxButton.OK);
                }
            }
            else
            {
                UpdateStatus($"未能找到 {currentController.Name} 的主窗口，无法嵌入。");
            }

            // 最终刷新状态并恢复按钮
            await RefreshMusicAppStatusAsync();
        }

        // “分离窗口”按钮点击
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            DetachEmbeddedWindow();
            UpdateStatus("窗口已分离。");
            // 刷新状态以更新按钮可用性
            RefreshMusicAppStatusAsync();
        }

        // 封装的分离逻辑 (可在 UI 线程调用)
        private void DetachEmbeddedWindow()
        {
            AppHostControl?.RestoreHostedWindow(); // AppHost 负责恢复窗口
            _embeddedWindowHandle = IntPtr.Zero; // 清除记录
            Debug.WriteLine("嵌入窗口已分离。");
        }

        // “关闭嵌入的应用”按钮点击
        private async void CloseEmbeddedAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) return;
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            var result = MessageBox.Show($"确定要关闭嵌入的 {currentController.Name} 吗？", "确认关闭", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            UpdateStatus($"正在关闭 {currentController.Name}...");
            SetInteractionButtonsEnabled(false); // 禁用交互按钮

            // 先分离，再关闭
            DetachEmbeddedWindow();
            await Task.Delay(200); // 短暂等待分离完成

            await currentController.CloseAppAsync(); // 调用控制器的关闭逻辑
            await Task.Delay(1000); // 等待进程关闭

            // 最终刷新状态
            await RefreshMusicAppStatusAsync();
        }


        // --- 媒体控制按钮 ---
        // 封装发送命令逻辑
        private async Task SendMediaCommandAsync(MediaCommand command)
        {
            if (currentController == null) return;

            // 确定目标窗口：优先使用嵌入的窗口句柄，否则查找主窗口
            IntPtr targetHwnd = _embeddedWindowHandle != IntPtr.Zero && WinAPI.IsWindow(_embeddedWindowHandle)
                                ? _embeddedWindowHandle
                                : WinAPI.FindMainWindow(currentController.ProcessName);

            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                UpdateStatus($"无法找到 {currentController.Name} 的窗口来发送命令 {command}。");
                // 检查应用是否还在运行
                if (!currentController.IsRunning() && _embeddedWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine($"[SendMediaCommand] 应用已停止，但仍记录为嵌入，执行分离。");
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                }
                return;
            }

            try
            {
                // 直接调用控制器的 SendCommandAsync，传递目标 HWND
                await currentController.SendCommandAsync(targetHwnd, command);
                // 命令发送后，稍作等待并刷新状态（特别是歌名）
                await Task.Delay(150);
                await RefreshMusicAppStatusAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"发送命令 {command} 时出错: {ex.Message}");
                Debug.WriteLine($"[SendMediaCommandAsync] Error: {ex}");
            }
        }

        // 按钮事件绑定到 SendMediaCommandAsync
        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PlayPause);
        private async void NextButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.NextTrack);
        private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PreviousTrack);
        private async void VolumeUpButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeUp);
        private async void VolumeDownButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeDown);
        private async void MuteButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeMute);


        // --- 状态刷新和 UI 更新 ---

        // 刷新音乐应用状态 (异步)
        private async Task RefreshMusicAppStatusAsync()
        {
            if (currentController == null)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentSongTextBlock.Text = "歌曲: N/A";
                    UpdateStatus("请选择播放器");
                    SetInteractionButtonsEnabled(false);
                    SetMediaButtonsEnabled(false);
                });
                return;
            }

            bool isRunning = false;
            bool isEmbedded = _embeddedWindowHandle != IntPtr.Zero && WinAPI.IsWindow(_embeddedWindowHandle);
            string song = "无";
            string status = $"[{currentController.Name}] ";

            try
            {
                // 检查进程是否仍在运行
                isRunning = currentController.IsRunning();

                // 状态同步：如果记录为嵌入但进程已停止，则执行分离
                if (isEmbedded && !isRunning)
                {
                    Debug.WriteLine($"[刷新状态] 检测到嵌入窗口的进程已停止，执行分离。");
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    isEmbedded = false; // 更新本地状态
                    status += "已停止 (自动分离)";
                }
                else if (isRunning)
                {
                    status += isEmbedded ? "已嵌入" : "运行中 (未嵌入)";
                    // 获取歌曲信息，传递当前有效的窗口句柄
                    IntPtr songTargetHwnd = isEmbedded ? _embeddedWindowHandle : WinAPI.FindMainWindow(currentController.ProcessName);
                    song = currentController.GetCurrentSong(songTargetHwnd);
                }
                else // 未运行
                {
                    status += "未运行";
                    // 确保如果未运行，则嵌入句柄也清空
                    if (_embeddedWindowHandle != IntPtr.Zero)
                    {
                        await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    }
                }

                // 在 UI 线程更新界面
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentSongTextBlock.Text = $"歌曲: {song}";
                    UpdateStatus(status);
                    SetInteractionButtonsEnabled(currentController != null); // 基本交互按钮（启动、分离、关闭）根据控制器和状态启用
                    SetMediaButtonsEnabled(isRunning); // 媒体控制按钮根据是否运行启用
                });
            }
            catch (Exception ex)
            {
                UpdateStatus($"刷新 {currentController.Name} 状态时出错: {ex.Message}");
                Debug.WriteLine($"[RefreshMusicAppStatusAsync] Error: {ex}");
                // 出错时保守处理，禁用大部分按钮
                await Dispatcher.InvokeAsync(() =>
                {
                    SetInteractionButtonsEnabled(false);
                    SetMediaButtonsEnabled(false);
                    MusicAppComboBox.IsEnabled = true; // 允许切换
                });
            }
        }

        // 更新状态栏文本 (确保在UI线程)
        private void UpdateStatus(string status)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => UpdateStatus(status));
                return;
            }
            if (CurrentStatusTextBlock != null) CurrentStatusTextBlock.Text = $"状态: {status}";
            Debug.WriteLine($"[状态更新] {status}");
        }

        // 设置交互按钮（启动/嵌入、分离、关闭）的启用状态
        private void SetInteractionButtonsEnabled(bool controllerSelected)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => SetInteractionButtonsEnabled(controllerSelected)); return; }

            bool canLaunchEmbed = controllerSelected && _embeddedWindowHandle == IntPtr.Zero && currentController?.ExecutablePath != null;
            LaunchAndEmbedButton.IsEnabled = canLaunchEmbed;

            bool canDetach = controllerSelected && _embeddedWindowHandle != IntPtr.Zero;
            DetachButton.IsEnabled = canDetach;

            bool canCloseEmbedded = controllerSelected && _embeddedWindowHandle != IntPtr.Zero;
            CloseEmbeddedAppButton.IsEnabled = canCloseEmbedded;

            // 下拉框通常总是可用
            MusicAppComboBox.IsEnabled = true;
        }

        // 设置媒体控制按钮的启用状态
        private void SetMediaButtonsEnabled(bool isRunning)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => SetMediaButtonsEnabled(isRunning)); return; }

            PlayPauseButton.IsEnabled = isRunning;
            NextButton.IsEnabled = isRunning;
            PreviousButton.IsEnabled = isRunning;
            VolumeUpButton.IsEnabled = isRunning;
            VolumeDownButton.IsEnabled = isRunning;
            MuteButton.IsEnabled = isRunning;
        }
    }
    #endregion
}