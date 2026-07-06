using System.Windows;
using System.Windows.Input;
using DrawingUploader.ViewModels;

namespace DrawingUploader;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Handle drag-over to show visual feedback.
    /// </summary>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            _viewModel.IsDragOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// Handle file drop from Windows Explorer.
    /// </summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        _viewModel.IsDragOver = false;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            _viewModel.HandleFileDrop(files);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Reset drag-over visual when leaving.
    /// </summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        _viewModel.IsDragOver = false;
        e.Handled = true;
    }
}
