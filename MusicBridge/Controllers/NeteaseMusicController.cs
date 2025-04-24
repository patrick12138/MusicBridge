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
            
            // 优先尝试基类中的媒体键方法
            await base.SendCommandAsync(hwnd, command);
        }

    }
}
