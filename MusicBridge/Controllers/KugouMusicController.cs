using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MusicBridge.Controllers
{
    public class KugouMusicController : MusicAppControllerBase
    {
        public override string Name => "酷狗音乐";
        public override string ProcessName => "KuGou";
        protected override string DefaultExeName => "KuGou.exe";
        
        // 酷狗音乐可能有特殊的安装路径，覆盖基类方法提供更准确的查找
        public override string? CheckDefaultInstallLocations()
        {
            string[] possiblePaths = {
                @"C:\Program Files (x86)\KuGou\KuGou.exe",
                @"C:\Program Files\KuGou\KuGou.exe",
                @"C:\Users\Public\Desktop\酷狗音乐.lnk"
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.WriteLine($"[{Name}] 找到可执行文件: {path}");
                    return path;
                }
            }
            
            // 尝试在Program Files目录下查找
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string potentialPath = Path.Combine(programFiles, "KuGou", "KuGou.exe");
            if (File.Exists(potentialPath))
            {
                Debug.WriteLine($"[{Name}] 找到可执行文件: {potentialPath}");
                return potentialPath;
            }
            
            // 尝试在AppData目录查找
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            potentialPath = Path.Combine(appData, "KuGou", "KuGou.exe");
            if (File.Exists(potentialPath))
            {
                Debug.WriteLine($"[{Name}] 找到可执行文件: {potentialPath}");
                return potentialPath;
            }
            
            return base.CheckDefaultInstallLocations();
        }
        
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