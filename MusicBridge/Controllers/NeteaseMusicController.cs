using System.Diagnostics;
using System.Text;

namespace MusicBridge.Controllers
{
    public class NeteaseMusicController : MusicAppControllerBase
    {
        public override string Name => "网易云音乐";
        public override string ProcessName => "cloudmusic";
        protected override string DefaultExeName => "cloudmusic.exe";

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
