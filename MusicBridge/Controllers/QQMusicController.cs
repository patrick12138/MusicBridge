namespace MusicBridge.Controllers
{
    public class QQMusicController : MusicAppControllerBase
    {
        public override string Name => "QQ音乐";
        public override string ProcessName => "QQMusic";
        protected override string DefaultExeName => "QQMusic.exe";
    }
}