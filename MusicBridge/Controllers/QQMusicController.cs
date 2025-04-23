using System.IO;

namespace MusicBridge.Controllers
{
    public class QQMusicController : MusicAppControllerBase
    {
        public override string Name => "QQ音乐";
        public override string ProcessName => "QQMusic";
        protected override string DefaultExeName => "QQMusic.exe";
        public override string? CheckDefaultInstallLocations()
        {
            string path86 = @"C:\Program Files (x86)\Tencent\QQMusic\QQMusic.exe";
            string path64 = @"C:\Program Files\Tencent\QQMusic\QQMusic.exe"; // 可能的64位路径
            if (File.Exists(path86)) return path86;
            if (File.Exists(path64)) return path64;
            // 还可以检查 AppData 等路径
            return base.CheckDefaultInstallLocations();
        }
    }
}