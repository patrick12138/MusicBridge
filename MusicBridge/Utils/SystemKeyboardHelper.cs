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
        public static bool IsRunning()
        {
            try
            {
                if (_oskProcess != null && !_oskProcess.HasExited)
                {
                    return true;
                }

                Process[] procs = Process.GetProcessesByName("osk");
                return procs != null && procs.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardHelper.IsRunning] 错误: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 打开系统虚拟键盘
        /// </summary>
        public static bool Open()
        {
            try
            {
                // 如果已经在运行，就不重复启动
                if (IsRunning())
                    return true;

                // 使用 ProcessStartInfo 启动系统键盘
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "osk.exe",
                    UseShellExecute = true
                };

                _oskProcess = Process.Start(startInfo);
                return _oskProcess != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardHelper.Open] 错误: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 关闭系统虚拟键盘
        /// </summary>
        public static bool Close()
        {
            try
            {
                // 尝试通过进程名关闭所有虚拟键盘实例
                Process[] procs = Process.GetProcessesByName("osk");
                bool closed = false;

                foreach (var proc in procs)
                {
                    try
                    {
                        proc.Kill();
                        closed = true;
                    }
                    catch
                    {
                        // 忽略单个进程关闭失败的错误
                    }
                }

                _oskProcess = null;
                return closed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardHelper.Close] 错误: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 切换系统虚拟键盘的显示状态
        /// </summary>
        public static bool ToggleSystemKeyboard()
        {
            if (IsRunning())
            {
                Close();
                return false; // 返回关闭后的状态
            }
            else
            {
                Open();
                return true; // 返回打开后的状态
            }
        }
    }
}