using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AudioPlayer;

public partial class MainWindow
{
    private const string DriveImagePath  = "Image/drive.png";
    private const string FolderImagePath = "Image/folder.png";
    
    public MainWindow()
    {
        InitializeComponent();
            
        var driveInfos = DriveInfo.GetDrives();
        foreach (var driveInfo in driveInfos)
        {
            #region DiskView

                var driveInfoStackPanel = new StackPanel()
                {
                    Orientation = Orientation.Horizontal
                };

                var diskImage = new Image()
                {
                    Source = new BitmapImage(new Uri(DriveImagePath, UriKind.Relative)),
                    Height = 14, Width = 14,
                    Margin = new Thickness(0,0,5,0)
                };

                var diskInfoTextBlock = new TextBlock()
                {
                    Text = $"{driveInfo.Name} {driveInfo.VolumeLabel}",
                };

                driveInfoStackPanel.Children.Add(diskImage);
                driveInfoStackPanel.Children.Add(diskInfoTextBlock);

                var driveInfoTreeViewItem = new TreeViewItem()
                {
                    Header = driveInfoStackPanel
                };

                FoldersTreeView.Items.Add(driveInfoTreeViewItem);
                
            #endregion

            #region DiskFoldersView

                var dirs = Directory.GetDirectories(driveInfo.RootDirectory.FullName);
                foreach (var dir in dirs)
                {
                    var folderInfoStackPanel = new StackPanel()
                    {
                        Orientation = Orientation.Horizontal
                    };

                    var folderImage = new Image()
                    {
                        Source = new BitmapImage(new Uri(FolderImagePath, UriKind.Relative)),
                        Height = 14, Width = 14,
                        Margin = new Thickness(0,0,5,0)
                    };

                    var folderInfoTextBlock = new TextBlock()
                    {
                        Text = Path.GetFileName(dir)
                    };

                    folderInfoStackPanel.Children.Add(folderImage);
                    folderInfoStackPanel.Children.Add(folderInfoTextBlock);

                    var folderInfoTreeViewItem = new TreeViewItem()
                    {
                        Header = folderInfoStackPanel
                    };

                    driveInfoTreeViewItem.Items.Add(folderInfoTreeViewItem);
                }

            #endregion
        }
            
        // drive.Name
        // drive.VolumeLabel
        // drive.RootDirectory.FullName
        // Directory.GetDirectories(drive.RootDirectory.FullName)
    }
}