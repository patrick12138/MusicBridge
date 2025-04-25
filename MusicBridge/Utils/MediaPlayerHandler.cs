using MusicBridge.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 处理音乐播放器媒体控制操作
    /// </summary>
    public class MediaPlayerHandler
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;

        /// <summary>
        /// 创建 MediaPlayerHandler 实例
        /// </summary>
        public MediaPlayerHandler(Dispatcher dispatcher, Action<string> updateStatus)
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
        }

        /// <summary>
        /// 向指定窗口发送媒体控制命令
        /// </summary>
        public async Task<bool> SendMediaCommandAsync(IMusicApp controller, IntPtr hwnd, MediaCommand command)
        {
            if (controller == null || hwnd == IntPtr.Zero)
            {
                _updateStatus("错误：无效的控制器或窗口句柄");
                return false;
            }

            try
            {
                // 使用正确的接口方法 SendCommandAsync 而不是 SendMediaCommand
                await controller.SendCommandAsync(hwnd, command);
                
                // 根据命令类型更新状态消息
                string actionText = GetCommandActionText(command);
                _updateStatus($"已向 {controller.Name} 发送{actionText}命令");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaPlayerHandler.SendMediaCommandAsync] 错误: {ex}");
                _updateStatus($"发送媒体命令时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据命令类型获取对应的操作文本
        /// </summary>
        private string GetCommandActionText(MediaCommand command)
        {
            switch (command)
            {
                case MediaCommand.PlayPause: return "播放/暂停";
                case MediaCommand.NextTrack: return "下一曲";
                case MediaCommand.PreviousTrack: return "上一曲";
                case MediaCommand.VolumeUp: return "音量增加";
                case MediaCommand.VolumeDown: return "音量减少";
                case MediaCommand.VolumeMute: return "静音";
                default: return "未知";
            }
        }
        
        /// <summary>
        /// 关闭音乐应用
        /// </summary>
        public bool CloseApp(IMusicApp controller)
        {
            if (controller == null)
            {
                _updateStatus("错误：无效的控制器");
                return false;
            }
            
            try
            {
                // 查找所有匹配的进程
                var processes = Process.GetProcessesByName(controller.ProcessName);
                if (processes.Length == 0)
                {
                    _updateStatus($"{controller.Name} 未运行");
                    return false;
                }
                
                // 尝试关闭所有匹配的进程
                foreach (var process in processes)
                {
                    try
                    {
                        process.CloseMainWindow();
                        // 如果进程没有在合理时间内退出，则强制终止
                        if (!process.WaitForExit(3000))
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MediaPlayerHandler.CloseApp] 关闭进程错误: {ex}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                _updateStatus($"已关闭 {controller.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaPlayerHandler.CloseApp] 错误: {ex}");
                _updateStatus($"关闭 {controller.Name} 时出错: {ex.Message}");
                return false;
            }
        }
    }
}