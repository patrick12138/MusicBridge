namespace MusicBridge.Controllers
{
    public class NeteaseMusicController : MusicAppControllerBase
    {
        public override string Name => "����������";
        public override string ProcessName => "cloudmusic";
        protected override string DefaultExeName => "cloudmusic.exe";
    }
}