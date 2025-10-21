using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.Windows;

/// <summary>
/// Windows プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class WindowsGlRenderer : SkiaGlRendererBase
{
    private IntPtr _hwnd;
    private IntPtr _hdc;
    private IntPtr _hglrc;

    protected override void CreateContext(IntPtr windowHandle)
    {
        _hwnd = windowHandle;

        // デバイスコンテキストを取得
        _hdc = Win32Interop.GetDC(_hwnd);
        if (_hdc == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get device context");

        // ピクセルフォーマットを設定
        var pfd = new Win32Interop.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<Win32Interop.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Win32Interop.PFD_DRAW_TO_WINDOW | Win32Interop.PFD_SUPPORT_OPENGL | Win32Interop.PFD_DOUBLEBUFFER,
            iPixelType = Win32Interop.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = Win32Interop.PFD_MAIN_PLANE
        };

        int pixelFormat = Win32Interop.ChoosePixelFormat(_hdc, ref pfd);
        if (pixelFormat == 0)
            throw new InvalidOperationException("Failed to choose pixel format");

        if (!Win32Interop.SetPixelFormat(_hdc, pixelFormat, ref pfd))
            throw new InvalidOperationException("Failed to set pixel format");

        // OpenGL コンテキストを作成
        _hglrc = Win32Interop.wglCreateContext(_hdc);
        if (_hglrc == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create OpenGL context");
    }

    protected override void DestroyContext()
    {
        // コンテキストを解放
        if (_hglrc != IntPtr.Zero)
        {
            Win32Interop.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Win32Interop.wglDeleteContext(_hglrc);
            _hglrc = IntPtr.Zero;
        }

        if (_hdc != IntPtr.Zero && _hwnd != IntPtr.Zero)
        {
            Win32Interop.ReleaseDC(_hwnd, _hdc);
            _hdc = IntPtr.Zero;
        }

        _hwnd = IntPtr.Zero;
    }

    protected override void MakeCurrent()
    {
        if (_hdc == IntPtr.Zero || _hglrc == IntPtr.Zero)
            throw new InvalidOperationException("OpenGL context is not initialised.");

        if (!Win32Interop.wglMakeCurrent(_hdc, _hglrc))
            throw new InvalidOperationException("Failed to make OpenGL context current.");
    }

    protected override void SwapBuffers()
    {
        if (_hdc != IntPtr.Zero)
        {
            Win32Interop.SwapBuffers(_hdc);
        }
    }
}
