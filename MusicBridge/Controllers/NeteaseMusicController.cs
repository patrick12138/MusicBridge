using System.Diagnostics;
using System.Threading.Tasks; // 确保引入 Task

namespace MusicBridge.Controllers
{
    public class NeteaseMusicController : MusicAppControllerBase
    {
        public override string Name => "网易云音乐";
        public override string ProcessName => "cloudmusic";
        protected override string DefaultExeName => "cloudmusic.exe";

        // 重写 SendCommandAsync 以尝试键盘模拟 (因为 WM_APPCOMMAND 对网易云可能无效)
        public override async Task SendCommandAsync(IntPtr hwnd, MediaCommand command)
        {
            // ... (前面的检查和音量控制代码保持不变) ...
            if (hwnd == IntPtr.Zero || !WinAPI.IsWindow(hwnd)) // 增加 IsWindow 检查
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 目标窗口句柄无效或已销毁。");
                return;
            }

            // 首先尝试 WM_APPCOMMAND (对音量控制通常有效)
            if (command == MediaCommand.VolumeUp || command == MediaCommand.VolumeDown || command == MediaCommand.VolumeMute)
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 发送 WM_APPCOMMAND 控制音量: {command}");
                await base.SendCommandAsync(hwnd, command);
                return;
            }


            // 对于播放/暂停/切歌，尝试使用 SendInput 进行键盘模拟
            Debug.WriteLine($"[{Name} SendCommandAsync] 尝试使用 SendInput 模拟发送命令: {command}");
            List<ushort> modifiers = new List<ushort>();
            ushort primaryKey = 0;

            switch (command)
            {
                case MediaCommand.PlayPause:
                    // 尝试 Ctrl+P
                    Debug.WriteLine($"[{Name}] 准备使用 SendInput 模拟 Ctrl+P");
                    modifiers.Add(WinAPI.VK_CONTROL);
                    primaryKey = WinAPI.VK_P;
                    break;
                case MediaCommand.NextTrack:
                    // 尝试 Ctrl+Right
                    Debug.WriteLine($"[{Name}] 准备使用 SendInput 模拟 Ctrl+Right");
                    modifiers.Add(WinAPI.VK_CONTROL);
                    primaryKey = WinAPI.VK_RIGHT;
                    break;
                case MediaCommand.PreviousTrack:
                    // 尝试 Ctrl+Left
                    Debug.WriteLine($"[{Name}] 准备使用 SendInput 模拟 Ctrl+Left");
                    modifiers.Add(WinAPI.VK_CONTROL);
                    primaryKey = WinAPI.VK_LEFT;
                    break;
            }

            if (primaryKey != 0) // 检查是否找到了对应的按键
            {
                try
                {
                    // 在调用前尝试设置焦点到目标窗口
                    WinAPI.SetFocus(hwnd);
                    await Task.Delay(50); // 短暂等待焦点切换

                    // ***修改：调用新的 SendInput 辅助方法***
                    await WinAPI.SimulateKeyPressWithModifiers(modifiers, primaryKey);
                    Debug.WriteLine($"[{Name}] 使用 SendInput 模拟按键 {command} 已发送。");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{Name}] 使用 SendInput 模拟按键 {command} 时发生错误: {ex.Message}");
                    // 仍然可以考虑回退到 WM_APPCOMMAND
                    // await base.SendCommandAsync(hwnd, command);
                }
            }
            else
            {
                 Debug.WriteLine($"[{Name}] 命令 {command} 没有对应的 SendInput 模拟按键操作。");
                 // await base.SendCommandAsync(hwnd, command);
            }
        }

        // ... (确保 WinAPI.cs 中有 VK_CONTROL, VK_P, VK_LEFT, VK_RIGHT 常量) ...
        // ... (以及 SimulateKeyPressWithModifiers, SetFocus, IsWindow 方法) ...
    }
}
