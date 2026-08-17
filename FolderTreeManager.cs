using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AudioPlayer;

public static class FolderTreeManager
{
    private const string DriveImagePath  = "Image/drive.png";
    private const string FolderImagePath = "Image/folder.png";
    private const string MusicImagePath = "Image/music.png";
    private const string DownloadsImagePath = "Image/downloads.png";
    private static readonly Guid MusicFolderGuid = new Guid("4BD8D571-6D19-48D3-BE97-422220080E43");
    private static readonly Guid DownloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int SHGetKnownFolderPath(
    [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
    uint dwFlags,
    IntPtr hToken,
    out string pszPath);

    public static void BuildTree(TreeView treeView)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            var name = $"{drive.Name} {drive.VolumeLabel}";
            var path = drive.RootDirectory.FullName;

            var driveItem = CreateTreeItem(name, path, DriveImagePath);

            treeView.Items.Add(driveItem);

            LoadSubfolders(driveItem);
        }

        var folderPaths = new (string path, string image)[]
            {
                (GetKnownFolderPath(MusicFolderGuid), MusicImagePath),
                (GetKnownFolderPath(DownloadsFolderGuid), DownloadsImagePath)
            };

        foreach (var (folderPath, image) in folderPaths)
        {
            if (string.IsNullOrEmpty(folderPath))
                continue;

            var folderItem = CreateTreeItem(Path.GetFileName(folderPath), folderPath, image);
            treeView.Items.Add(folderItem);
            LoadSubfolders(folderItem);
        }
    }

    private static TreeViewItem CreateTreeItem(string displayName, string fullPath, string imagePath)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
            Height = 14,
            Width = 14,
            Margin = new Thickness(0, 0, 5, 0)
        });

        panel.Children.Add(new TextBlock { Text = displayName });

        var item = new TreeViewItem
        {
            Header = panel,
            Tag = fullPath
        };

        SubscribeToExpanded(item);
        return item;
    }

    private static void OnTreeItemExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item) return;

        foreach (TreeViewItem child in item.Items)
        {
            if (child.Items.Count == 0)
            {
                LoadSubfolders(child);
            }
        }
    }

    private static void LoadSubfolders(TreeViewItem item)
    {
        if (item.Tag is not string path || string.IsNullOrEmpty(path)) return;

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var child = CreateTreeItem(
                    Path.GetFileName(dir),
                    dir,
                    FolderImagePath
                );
                item.Items.Add(child);
            }
        }
        catch (UnauthorizedAccessException e)
        {
            Debug.WriteLine("UnauthorizedAccessException: " + e.Message);
        }
    }

    private static void SubscribeToExpanded(TreeViewItem item)
    {
        item.Expanded += OnTreeItemExpanded;
    }

    public static IEnumerable<string> GetFilesRecursive(string dirPath, Regex regex)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException($"Указанная директория не существует: {dirPath}");
        }

        var subDirectories = Array.Empty<string>();
        try
        {
            subDirectories = Directory.GetDirectories(dirPath);
        }
        catch (UnauthorizedAccessException e)
        {
            Debug.WriteLine("UnauthorizedAccessException: " + e.Message);
        }

        if (subDirectories.Length == 0)
        {
            return GetFilesWithRegex(dirPath, regex);
        }

        IEnumerable<string> subDirsFiles = [];

        foreach (var subDirectory in subDirectories)
        {
            foreach (var subDirFile in GetFilesRecursive(subDirectory, regex))
            {
                subDirsFiles = subDirsFiles.Append(subDirFile);
            }
        }

        var currentFolderFiles = GetFilesWithRegex(dirPath, regex);

        foreach (var currentFolderFile in currentFolderFiles)
        {
            subDirsFiles = subDirsFiles.Append(currentFolderFile);
        }

        return subDirsFiles;
    }
    private static IEnumerable<string> GetFilesWithRegex(string dirPath, Regex regex)
    {
        var DirectoryEnumerateFiles = Enumerable.Empty<string>();
        try
        {
            DirectoryEnumerateFiles = Directory.EnumerateFiles(dirPath).Where(file => regex.IsMatch(file));
        }
        catch (UnauthorizedAccessException e)
        {
            Debug.WriteLine("UnauthorizedAccessException: " + e.Message);
        }

        return DirectoryEnumerateFiles;
    }

    private static string GetKnownFolderPath(Guid folderId)
    {
        var hr = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out string path);

        if (hr == 0)
            return path;
        else
            return String.Empty;
    }
}