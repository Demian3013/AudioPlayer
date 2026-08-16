using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TagLib.Riff;

namespace AudioPlayer;

public static class FolderTreeManager
{
    private const string DriveImagePath  = "Image/drive.png";
    private const string FolderImagePath = "Image/folder.png";

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
    }

    private static TreeViewItem CreateTreeItem(string displayName, string fullPath, string imagePath)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
            Height = 14,
            Width  = 14,
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

        var subDirectories = Directory.GetDirectories(dirPath);
        if (subDirectories.Length == 0)
        {
            return Directory.EnumerateFiles(dirPath).Where(file => regex.IsMatch(file));
        }

        IEnumerable<string> subDirsFiles = [];
        
        foreach (var subDirectory in subDirectories)
        {
            foreach (var subDirFile in GetFilesRecursive(subDirectory, regex))
            {
                subDirsFiles = subDirsFiles.Append(subDirFile);
            }
        }

        var currentFolderFiles = Directory.EnumerateFiles(dirPath).Where(file => regex.IsMatch(file));
        
        foreach (var currentFolderFile in currentFolderFiles)
        {
            subDirsFiles = subDirsFiles.Append(currentFolderFile);
        }
        
        return subDirsFiles;
    }
}