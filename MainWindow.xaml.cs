namespace AudioPlayer;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        FolderTreeManager.BuildTree(FoldersTreeView);
    }
}