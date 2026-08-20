using System.Runtime.InteropServices;

namespace AudioPlayer;

public static class WindowsFolders
{
    private static readonly Guid MusicFolderGuid = new("4BD8D571-6D19-48D3-BE97-422220080E43");
    private static readonly Guid DownloadsFolderGuid = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out string pszPath);
    
    private static string GetKnownFolderPath(Guid folderId)
    {
        var hr = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out var path);
        return hr == 0 ? path : string.Empty;
    }

    public static string GetMusicFolderPath()     => GetKnownFolderPath(MusicFolderGuid);
    public static string GetDownloadsFolderPath() => GetKnownFolderPath(DownloadsFolderGuid);
}