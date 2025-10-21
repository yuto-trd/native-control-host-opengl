using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.MacOS;

/// <summary>
/// macOS プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class MacOSGlRenderer : SkiaGlRendererBase
{
    private IntPtr _nsView;
    private IntPtr _nsOpenGLContext;

    protected override void CreateContext(IntPtr windowHandle)
    {
        _nsView = windowHandle;

        // NSOpenGLPixelFormat を作成
        IntPtr pixelFormatClass = CocoaInterop.GetClass("NSOpenGLPixelFormat");
        IntPtr pixelFormat = CocoaInterop.SendMessage(
            CocoaInterop.SendMessage(pixelFormatClass, "alloc"),
            "init");

        // NSOpenGLContext を作成
        IntPtr contextClass = CocoaInterop.GetClass("NSOpenGLContext");
        _nsOpenGLContext = CocoaInterop.SendMessage(
            CocoaInterop.SendMessage(contextClass, "alloc"),
            "initWithFormat:shareContext:",
            pixelFormat);

        if (_nsOpenGLContext == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create NSOpenGLContext");

        // ビューに関連付け
        CocoaInterop.SendMessage(_nsOpenGLContext, "setView:", _nsView);
    }

    protected override void DestroyContext()
    {
        if (_nsOpenGLContext != IntPtr.Zero)
        {
            CocoaInterop.SendMessage(_nsOpenGLContext, "clearCurrentContext");
            CocoaInterop.SendMessage(_nsOpenGLContext, "release");
            _nsOpenGLContext = IntPtr.Zero;
        }

        _nsView = IntPtr.Zero;
    }

    protected override void MakeCurrent()
    {
        if (_nsOpenGLContext == IntPtr.Zero)
            throw new InvalidOperationException("NSOpenGLContext is not initialised.");

        CocoaInterop.SendMessage(_nsOpenGLContext, "makeCurrentContext");
    }

    protected override void SwapBuffers()
    {
        if (_nsOpenGLContext != IntPtr.Zero)
        {
            CocoaInterop.glFlush();
            CocoaInterop.SendMessage(_nsOpenGLContext, "flushBuffer");
        }
    }

    protected override void OnAfterResize(int width, int height)
    {
        if (_nsOpenGLContext != IntPtr.Zero)
        {
            CocoaInterop.SendMessage(_nsOpenGLContext, "update");
        }
    }
}
