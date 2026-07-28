using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AudioPlayer;

public static class FolderTreeManager
{
    private const string DriveImagePath = "Image/drive.png";
    private const string FolderImagePath = "Image/folder.png";

    public static void BuildTree(TreeView treeView)
    {
        var drives = DriveInfo.GetDrives();
        foreach (var drive in drives)
        {
            var name = $"{drive.Name} {drive.VolumeLabel}";
            var path = drive.RootDirectory.FullName;

            var driveItem = CreateTreeItem(name, path, DriveImagePath);

            treeView.Items.Add(driveItem);

            try
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    var folderItem = CreateTreeItem(
                        Path.GetFileName(dir),
                        dir,
                        FolderImagePath
                    );
                    driveItem.Items.Add(folderItem);
                }
            }
            catch { }
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
        var item = sender as TreeViewItem;
        if (item == null) return;

        if (item.Items.Count == 0)
        {
            LoadSubfolders(item);
            return;
        }

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
        string path = item.Tag as string;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                var child = CreateTreeItem(
                    Path.GetFileName(dir),
                    dir,
                    FolderImagePath
                );
                item.Items.Add(child);
            }
        }
        catch { }
    }

    public static void SubscribeToExpanded(TreeViewItem item)
    {
        item.Expanded += OnTreeItemExpanded;
    }
}