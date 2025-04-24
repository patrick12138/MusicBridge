using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBridge.Controllers
{
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
        Task SendCommandAsync(IntPtr targetHwnd, MediaCommand command);
        string GetCurrentSong(IntPtr targetHwnd); // 获取歌曲信息也需要目标 HWND
        Task<string?> FindExecutablePathAsync(); // 查找路径
    }
}
