using System.Diagnostics;
using System.IO;

namespace MusicBridge.Controllers
{
    public class QQMusicController : MusicAppControllerBase
    {
        public override string Name => "QQ音乐";
        public override string ProcessName => "QQMusic";
        protected override string DefaultExeName => "QQMusic.exe";
        
        public override async Task SendCommandAsync(IntPtr hwnd, MediaCommand command)
        {
            Debug.WriteLine($"[{Name}] 尝试发送命令: {command} 到窗口 {hwnd}");
            
            // 优先尝试基类中的媒体键方法
            await base.SendCommandAsync(hwnd, command);
        }
    }
}