using System;
using System.Collections.Generic;
using AvaloniaOpenGLHost.Rendering;
using SharpVk;
using SharpVk.Khronos;

namespace AvaloniaOpenGLHost.Platform.Windows;

/// <summary>
/// Windows プラットフォーム向けの Vulkan レンダラー実装。
/// </summary>
public sealed class WindowsVulkanRenderer : SkiaVulkanRendererBase
{
    protected override Surface CreateSurface(Instance instance, IntPtr windowHandle)
    {
        var hInstance = Win32Interop.GetModuleHandle(null!);
        return instance.CreateWin32Surface(hInstance, windowHandle);
    }

    protected override IEnumerable<string> GetPlatformInstanceExtensions()
    {
        yield return "VK_KHR_win32_surface";
    }
}
