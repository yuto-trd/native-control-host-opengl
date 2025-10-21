using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AvaloniaOpenGLHost.Platform.Linux;
using AvaloniaOpenGLHost.Platform.Windows;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Controls;

/// <summary>
/// Vulkan バックエンドを使用して SkiaSharp 描画を行う NativeControlHost。
/// </summary>
public sealed class VulkanHost : NativeControlHost
{
    private IGlRenderer? _renderer;
    private IntPtr _nativeHandle = IntPtr.Zero;

    static VulkanHost()
    {
        BoundsProperty.Changed.AddClassHandler<VulkanHost>((control, args) => control.OnBoundsChanged(args));
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        IPlatformHandle handle;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _nativeHandle = Win32Interop.CreateChildWindow(parent.Handle);
            handle = new PlatformHandle(_nativeHandle, "HWND");
            _renderer = new WindowsVulkanRenderer();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _nativeHandle = parent.Handle;
            handle = new PlatformHandle(_nativeHandle, parent.HandleDescriptor);
            _renderer = new LinuxVulkanRenderer();
        }
        else
        {
            throw new PlatformNotSupportedException("Vulkan rendering is not supported on this platform in the sample.");
        }

        try
        {
            _renderer.Initialize(_nativeHandle);
            var bounds = Bounds;
            _renderer.Resize((int)bounds.Width, (int)bounds.Height);
            _renderer.Start();
        }
        catch
        {
            _renderer?.Dispose();
            _renderer = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _nativeHandle != IntPtr.Zero)
            {
                Win32Interop.DestroyWindow(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
            }

            throw;
        }

        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _renderer?.Stop();
        _renderer?.Dispose();
        _renderer = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _nativeHandle != IntPtr.Zero)
        {
            Win32Interop.DestroyWindow(_nativeHandle);
        }

        _nativeHandle = IntPtr.Zero;

        base.DestroyNativeControlCore(control);
    }

    private void OnBoundsChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_renderer == null)
            return;

        var bounds = (Rect)args.NewValue!;
        int width = (int)bounds.Width;
        int height = (int)bounds.Height;

        if (width > 0 && height > 0)
        {
            _renderer.Resize(width, height);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderer?.Stop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_renderer != null && !_renderer.IsRunning)
        {
            _renderer.Start();
        }
    }
}
