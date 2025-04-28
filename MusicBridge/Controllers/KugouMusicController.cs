using System.Diagnostics;

namespace MusicBridge.Controllers
{
    public class KugouMusicController : MusicAppControllerBase
    {
        public override string Name => "酷狗音乐";
        public override string ProcessName => "KuGou";
        protected override string DefaultExeName => "KuGou.exe";
        
        // 酷狗音乐窗口标题处理可能需要特殊逻辑
        public override string GetCurrentSong(IntPtr targetHwnd)
        {
            string title = base.GetCurrentSong(targetHwnd);
            
            // 如果是酷狗特定的标题格式，进一步清理
            if (title.Contains(" - "))
            {
                // 酷狗通常使用"歌曲名 - 歌手"的格式
                string[] parts = title.Split(new[] { " - " }, StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    // 优先显示"歌曲名 - 歌手"
                    return parts[0].Trim() + " - " + parts[1].Trim();
                }
            }
            
            return title;
        }
        
        public override async Task SendCommandAsync(IntPtr hwnd, MediaCommand command)
        {
            Debug.WriteLine($"[{Name}] 尝试发送命令: {command} 到窗口 {hwnd}");

            // 如果是音量相关的命令，直接调用基类方法
            if (command == MediaCommand.VolumeMute || command == MediaCommand.VolumeDown || command == MediaCommand.VolumeUp)
            {
                Debug.WriteLine($"[{Name}] 音量命令 {command}，调用基类方法处理");
                await base.SendCommandAsync(hwnd, command);
                return;
            }

            // 首先尝试使用媒体键方法（这是从网易云验证有效的方法）
            bool success = await SendMediaKeyCommandAsync(hwnd, command);
            if (success)
            {
                Debug.WriteLine($"[{Name} SendCommandAsync] 使用媒体键成功发送 {command} 到 HWND: {hwnd}");
                return;
            }
        }
    }
}