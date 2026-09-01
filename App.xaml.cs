using System.Windows;
using AudioPlayer.Tools;

namespace AudioPlayer
{
    public partial class App
    {
        protected override async void OnExit(ExitEventArgs e)
        {
            var playlistService = PlaylistService.GetInstance();
            await playlistService.SaveAsync();
            
            base.OnExit(e);
        }
    }
}
