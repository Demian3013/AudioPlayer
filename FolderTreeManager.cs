using System.Diagnostics;
using System.IO;
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
                (WindowsFolders.GetMusicFolderPath(), MusicImagePath),
                (WindowsFolders.GetDownloadsFolderPath(), DownloadsImagePath)
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

    public static List<string> GetFilesRecursive(
        string dirPath,
        Regex regex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException(
                $"Указанная директория не существует: {dirPath}");
        }

        var files = new List<string>();

        string[] subDirectories;

        try
        {
            subDirectories = Directory.GetDirectories(dirPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine(
                $"Нет доступа к директории {dirPath}: {ex.Message}");

            return files;
        }

        // Файлы текущей директории
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            files.AddRange(
                GetFilesWithRegex(dirPath, regex));
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine(
                $"Нет доступа к файлам {dirPath}: {ex.Message}");
        }

        // Поддиректории
        foreach (var subDirectory in subDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subDirectoryFiles = GetFilesRecursive(
                subDirectory,
                regex,
                cancellationToken);

            files.AddRange(subDirectoryFiles);
        }

        return files;
    }
    
    private static IEnumerable<string> GetFilesWithRegex(string dirPath, Regex regex)
    {
        IEnumerable<string> directoryEnumerateFiles = [];
        
        try
        {
            directoryEnumerateFiles = Directory.EnumerateFiles(dirPath).Where(file => regex.IsMatch(file));
        }
        catch (UnauthorizedAccessException e)
        {
            Debug.WriteLine("UnauthorizedAccessException: " + e.Message);
        }

        return directoryEnumerateFiles;
    }
}