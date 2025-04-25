using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using MusicBridge.Controllers;
using MusicBridge.Utils.Window;

namespace MusicBridge
{
    // 控制器基类
    public abstract class MusicAppControllerBase : IMusicApp
    {
        public abstract string Name { get; }
        public abstract string ProcessName { get; }
        private string? _executablePath = null;
        private bool _pathSearched = false;
        public string? ExecutablePath => _executablePath;
        protected abstract string DefaultExeName { get; }

        public virtual bool IsRunning() => Process.GetProcessesByName(ProcessName).Length > 0;

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
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] 启动失败: {ex.Message}");
                MessageBox.Show($"启动 {Name} 时发生错误: {ex.Message}", "启动失败", MessageBoxButton.OK);
            }
        }

        public virtual async Task CloseAppAsync()
        {
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
                await Task.Delay(50);
            }
            else
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 收到无效的命令: {command}");
            }
        }

        // 使用媒体控制键发送命令
        protected virtual async Task<bool> SendMediaKeyCommandAsync(IntPtr hwnd, MediaCommand command)
        {
            try
            {
                // 映射媒体控制命令到对应的虚拟键
                byte virtualKey = 0;
                const uint MAPVK_VK_TO_VSC = 0;
                
                switch (command)
                {
                    case MediaCommand.PlayPause:
                        virtualKey = WinAPI.VK_MEDIA_PLAY_PAUSE;
                        break;
                    case MediaCommand.NextTrack:
                        virtualKey = WinAPI.VK_MEDIA_NEXT_TRACK;
                        break;
                    case MediaCommand.PreviousTrack:
                        virtualKey = WinAPI.VK_MEDIA_PREV_TRACK;
                        break;
                    default:
                        return false; // 不支持其他命令
                }
                
                if (virtualKey != 0)
                {
                    // 获取按键的扫描码
                    uint scanCode = WinAPI.MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);
                    Debug.WriteLine($"[{Name}] 使用媒体控制键 - VK: 0x{virtualKey:X}, ScanCode: {scanCode}");
                    
                    // 构造LPARAM（按照Windows键盘消息格式）
                    uint repeatCount = 1;
                    uint downLParam = (repeatCount & 0xFFFF) | (scanCode << 16) | (0 << 24) | (0 << 29) | (0 << 30) | (0 << 31);
                    uint upLParam = (repeatCount & 0xFFFF) | (scanCode << 16) | (0 << 24) | (1 << 29) | (1 << 30) | (0 << 31);
                    
                    // 发送键盘消息（使用PostMessage可能更适合网易云这类应用）
                    WinAPI.PostMessage(hwnd, WinAPI.WM_KEYDOWN, (IntPtr)virtualKey, (IntPtr)downLParam);
                    await Task.Delay(50);
                    WinAPI.PostMessage(hwnd, WinAPI.WM_KEYUP, (IntPtr)virtualKey, (IntPtr)upLParam);
                    
                    // 同时尝试使用SendInput发送系统级别的按键
                    WinAPI.INPUT[] inputs = new WinAPI.INPUT[2];
                    
                    // 设置按键按下
                    inputs[0].type = WinAPI.INPUT_KEYBOARD;
                    inputs[0].u.ki.wVk = virtualKey;
                    inputs[0].u.ki.wScan = (ushort)scanCode;
                    inputs[0].u.ki.dwFlags = WinAPI.KEYEVENTF_EXTENDEDKEY;
                    inputs[0].u.ki.time = 0;
                    inputs[0].u.ki.dwExtraInfo = WinAPI.GetMessageExtraInfo();
                    
                    // 设置按键抬起
                    inputs[1].type = WinAPI.INPUT_KEYBOARD;
                    inputs[1].u.ki.wVk = virtualKey;
                    inputs[1].u.ki.wScan = (ushort)scanCode;
                    inputs[1].u.ki.dwFlags = WinAPI.KEYEVENTF_EXTENDEDKEY | WinAPI.KEYEVENTF_KEYUP;
                    inputs[1].u.ki.time = 0;
                    inputs[1].u.ki.dwExtraInfo = WinAPI.GetMessageExtraInfo();
                    
                    // 发送按键
                    uint result = WinAPI.SendInput(2, inputs, Marshal.SizeOf(typeof(WinAPI.INPUT)));
                    Debug.WriteLine($"[{Name}] SendInput result: {result}");
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Name}] 发送媒体键命令失败: {ex.Message}");
            }
            
            return false;
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

}