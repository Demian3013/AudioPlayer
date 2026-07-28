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

        FolderTreeManager.BuildTree(FoldersTreeView);  
        // drive.Name
        // drive.VolumeLabel
        // drive.RootDirectory.FullName
        // Directory.GetDirectories(drive.RootDirectory.FullName)
    }
}