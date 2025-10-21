using System;
using System.Collections.Generic;
using AvaloniaOpenGLHost.Rendering;
using SharpVk;
using SharpVk.Khronos;

namespace AvaloniaOpenGLHost.Platform.Linux;

/// <summary>
/// Linux (X11) プラットフォーム向けの Vulkan レンダラー実装。
/// </summary>
public sealed class LinuxVulkanRenderer : SkiaVulkanRendererBase
{
    private IntPtr _display;

    protected override Surface CreateSurface(Instance instance, IntPtr windowHandle)
    {
        _display = X11Interop.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("Failed to open X11 display for Vulkan.");

        return instance.CreateXlibSurface(_display, windowHandle);
    }

    protected override IEnumerable<string> GetPlatformInstanceExtensions()
    {
        yield return "VK_KHR_xlib_surface";
    }

    protected override void DestroySurface()
    {
        base.DestroySurface();

        if (_display != IntPtr.Zero)
        {
            X11Interop.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
}
