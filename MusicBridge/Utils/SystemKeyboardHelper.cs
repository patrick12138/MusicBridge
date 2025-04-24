using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 提供系统虚拟键盘操作的工具类
    /// </summary>
    public static class SystemKeyboardHelper
    {
        // 虚拟键盘程序路径
        private static readonly string OskPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "osk.exe");

        // 进程实例，用于跟踪虚拟键盘进程
        private static Process _oskProcess = null;

        /// <summary>
        /// 检查系统虚拟键盘是否正在运行
        /// </summary>
        public static bool IsKeyboardRunning()
        {
            if (_oskProcess != null && !_oskProcess.HasExited)
            {
                return true;
            }

            // 检查是否有其他实例在运行
            Process[] processes = Process.GetProcessesByName("osk");
            return processes.Length > 0;
        }

        /// <summary>
        /// 打开系统虚拟键盘
        /// </summary>
        /// <returns>是否成功启动键盘</returns>
        public static bool OpenSystemKeyboard()
        {
            try
            {
                // 如果键盘已经在运行，则不重复启动
                if (IsKeyboardRunning())
                {
                    // 将已有的键盘窗口置前
                    Process[] processes = Process.GetProcessesByName("osk");
                    if (processes.Length > 0)
                    {
                        IntPtr hwnd = processes[0].MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            WinAPI.SetForegroundWindow(hwnd);
                            return true;
                        }
                    }
                    return true;
                }

                // 检查文件是否存在
                if (!File.Exists(OskPath))
                {
                    MessageBox.Show("找不到系统虚拟键盘程序 (osk.exe)。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // 启动虚拟键盘进程
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = OskPath,
                    UseShellExecute = true
                };

                _oskProcess = Process.Start(startInfo);
                return _oskProcess != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardHelper.OpenSystemKeyboard] 错误: {ex}");
                MessageBox.Show($"启动系统虚拟键盘时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 关闭系统虚拟键盘
        /// </summary>
        public static void CloseSystemKeyboard()
        {
            try
            {
                if (_oskProcess != null && !_oskProcess.HasExited)
                {
                    _oskProcess.CloseMainWindow();
                    // 给一点时间让进程优雅地退出
                    if (!_oskProcess.WaitForExit(1000))
                    {
                        _oskProcess.Kill();
                    }
                }
                else
                {
                    // 尝试关闭由其他方式启动的键盘实例
                    Process[] processes = Process.GetProcessesByName("osk");
                    foreach (var process in processes)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(1000))
                        {
                            process.Kill();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardHelper.CloseSystemKeyboard] 错误: {ex}");
            }
            finally
            {
                _oskProcess = null;
            }
        }

        /// <summary>
        /// 切换系统虚拟键盘的显示状态（打开/关闭）
        /// </summary>
        /// <returns>键盘最终是否为打开状态</returns>
        public static bool ToggleSystemKeyboard()
        {
            if (IsKeyboardRunning())
            {
                CloseSystemKeyboard();
                return false;
            }
            else
            {
                return OpenSystemKeyboard();
            }
        }
    }
}