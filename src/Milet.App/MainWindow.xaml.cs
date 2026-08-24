using Microsoft.UI.Xaml;
using Nexus.App.Shell;

namespace Nexus.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(ShellPage));
    }
}
