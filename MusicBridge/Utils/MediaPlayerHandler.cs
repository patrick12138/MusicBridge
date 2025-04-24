using MusicBridge.Controllers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MusicBridge.Utils
{
    /// <summary>
    /// Handles media control operations for music applications
    /// </summary>
    public class MediaPlayerHandler
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;
        
        /// <summary>
        /// Creates a new instance of MediaPlayerHandler
        /// </summary>
        /// <param name="dispatcher">UI dispatcher for threading</param>
        /// <param name="updateStatus">Action to update status messages</param>
        public MediaPlayerHandler(Dispatcher dispatcher, Action<string> updateStatus)
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
        }
        
        /// <summary>
        /// Sends a media command to the specified window
        /// </summary>
        public async Task SendMediaCommandAsync(IMusicAppController controller, IntPtr targetHwnd, MediaCommand command)
        {
            if (controller == null) return;

            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                _updateStatus($"无法找到 {controller.Name} 的窗口来发送命令 {command}。");
                return;
            }

            try
            {
                // 调用控制器的 SendCommandAsync，传递目标 HWND
                await controller.SendCommandAsync(targetHwnd, command);
                // 命令发送后，稍作等待
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                _updateStatus($"发送命令 {command} 时出错: {ex.Message}");
                Debug.WriteLine($"[SendMediaCommandAsync] Error: {ex}");
            }
        }
        
        /// <summary>
        /// Closes the currently embedded application
        /// </summary>
        public void CloseEmbeddedApp(IntPtr windowHandle)
        {
            if (windowHandle != IntPtr.Zero && WinAPI.IsWindow(windowHandle))
            {
                try
                {
                    // 发送关闭消息
                    WinAPI.SendMessage(windowHandle, WinAPI.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"关闭嵌入窗口时发生错误: {ex.Message}");
                }
            }
        }
    }
}