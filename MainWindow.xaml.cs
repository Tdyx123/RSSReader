using System.Windows;

namespace RSSReader;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void WebViewPanel_BackRequested(object? sender, EventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.IsWebViewVisible = false;
            vm.SelectedArticle = null;
        }
    }
}
