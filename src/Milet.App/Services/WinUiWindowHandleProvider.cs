using Milet.Application.Abstractions;

namespace Milet.App.Services;

/// <summary>Echte WinUI-Implementierung für den WAM-Broker-Fensterhandle (s. IWindowHandleProvider) —
/// überschreibt den NullWindowHandleProvider-Fallback aus Infrastructure (Registrierung nach
/// AddInfrastructure in App.xaml.cs gewinnt).</summary>
public sealed class WinUiWindowHandleProvider : IWindowHandleProvider
{
    public IntPtr GetHandle() => WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
}
