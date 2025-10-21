using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.Linux;

/// <summary>
/// Linux (X11) プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class LinuxGlRenderer : SkiaGlRendererBase
{
    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _glxContext;

    protected override void CreateContext(IntPtr windowHandle)
    {
        _window = windowHandle;

        // X11 ディスプレイを開く
        _display = X11Interop.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("Failed to open X11 display");

        int screen = X11Interop.XDefaultScreen(_display);

        // GLX Visual を選択
        int[] attribs = new[]
        {
            X11Interop.GLX_RGBA,
            X11Interop.GLX_DOUBLEBUFFER,
            X11Interop.GLX_RED_SIZE, 8,
            X11Interop.GLX_GREEN_SIZE, 8,
            X11Interop.GLX_BLUE_SIZE, 8,
            X11Interop.GLX_DEPTH_SIZE, 24,
            0
        };

        IntPtr visualInfo = X11Interop.glXChooseVisual(_display, screen, attribs);
        if (visualInfo == IntPtr.Zero)
            throw new InvalidOperationException("Failed to choose GLX visual");

        // GLX コンテキストを作成
        _glxContext = X11Interop.glXCreateContext(_display, visualInfo, IntPtr.Zero, true);
        if (_glxContext == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create GLX context");
    }

    protected override void DestroyContext()
    {
        if (_glxContext != IntPtr.Zero && _display != IntPtr.Zero)
        {
            X11Interop.glXMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero);
            X11Interop.glXDestroyContext(_display, _glxContext);
            _glxContext = IntPtr.Zero;
        }

        if (_display != IntPtr.Zero)
        {
            X11Interop.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }

        _window = IntPtr.Zero;
    }

    protected override void MakeCurrent()
    {
        if (_display == IntPtr.Zero || _window == IntPtr.Zero || _glxContext == IntPtr.Zero)
            throw new InvalidOperationException("GLX context is not initialised.");

        if (!X11Interop.glXMakeCurrent(_display, _window, _glxContext))
            throw new InvalidOperationException("Failed to make GLX context current.");
    }

    protected override void SwapBuffers()
    {
        if (_display != IntPtr.Zero && _window != IntPtr.Zero)
        {
            X11Interop.glXSwapBuffers(_display, _window);
        }
    }
}
