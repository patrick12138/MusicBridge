using System.IO;

namespace MusicBridge.Controllers
{
    public class QQMusicController : MusicAppControllerBase
    {
        public override string Name => "QQ����";
        public override string ProcessName => "QQMusic";
        protected override string DefaultExeName => "QQMusic.exe";
        public override string? CheckDefaultInstallLocations()
        {
            string path86 = @"C:\Program Files (x86)\Tencent\QQMusic\QQMusic.exe";
            string path64 = @"C:\Program Files\Tencent\QQMusic\QQMusic.exe"; // ���ܵ�64λ·��
            if (File.Exists(path86)) return path86;
            if (File.Exists(path64)) return path64;
            // �����Լ�� AppData ��·��
            return base.CheckDefaultInstallLocations();
        }
    }
}