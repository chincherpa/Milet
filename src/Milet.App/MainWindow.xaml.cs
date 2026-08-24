using Microsoft.UI.Xaml;
using Milet.App.Shell;

namespace Milet.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(ShellPage));
    }
}
