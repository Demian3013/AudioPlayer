using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace AudioPlayer;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        FolderTreeManager.BuildTree(FoldersTreeView);
    }
    
    private static readonly Regex SoundFileRegex = MyRegex();
    
    [GeneratedRegex(@"^.*\.(mp3|wav|wma|asf|avi)$", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
    
    private void FoldersTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FoldersTreeView.SelectedItem is not TreeViewItem item) return;
        if (item.Tag is not string path) return;
        
        Debug.WriteLine("Вы выбрали папку: " + path);

        // все музыкальные файлы из выбранной папки
        var dirFiles = Directory.GetFiles(path)
                            .Where(file => SoundFileRegex.IsMatch(file))
                            .ToArray();
        
        // TrackDataGrid.ItemsSource = dirFiles;
    }
}