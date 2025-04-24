using System.Diagnostics;
using System.IO;
using System.Windows;

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

            // 如果所有方法都失败，回退到基类方法
            await base.SendCommandAsync(hwnd, command);
        }
        
        // 添加强化的嵌入提示
        public static string GetEmbedTips()
        {
            return "酷狗音乐嵌入提示:\n\n" +
                   "1. 确保酷狗音乐未在播放视频内容\n" +
                   "2. 将酷狗音乐切换到'精简模式'（在酷狗菜单中选择）\n" +
                   "3. 嵌入后如看不到完整内容，请调整窗口大小\n" +
                   "4. 如果看不到播放控制区域，可以在嵌入前手动拉伸酷狗音乐窗口\n" +
                   "5. 如果播放控制无效，尝试点击嵌入区域以确保焦点正确\n\n" +
                   "注意: 如果使用新的鼠标点击控制方式，按钮需要在可见区域内才能正常控制。";
        }
    }
}