using Milet.Application.Abstractions;

namespace Milet.Infrastructure.Email;

/// <summary>Default-Fallback, solange keine WinUI-Fensterimplementierung registriert ist (s. IWindowHandleProvider).</summary>
public sealed class NullWindowHandleProvider : IWindowHandleProvider
{
    public IntPtr GetHandle() => IntPtr.Zero;
}
