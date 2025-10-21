using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.Windows;

/// <summary>
/// Windows プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class WindowsGlRenderer : GlRendererBase
{
    private IntPtr _hwnd;
    private IntPtr _hdc;
    private IntPtr _hglrc;
    private float _rotation = 0f;

    protected override void PlatformInitialize(IntPtr windowHandle)
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

    protected override void PlatformCleanup()
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
    }

    protected override void PlatformResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        // このスレッドでコンテキストをアクティブ化
        Win32Interop.wglMakeCurrent(_hdc, _hglrc);

        // ビューポートを設定
        Win32Interop.glViewport(0, 0, width, height);
    }

    protected override void PlatformRender()
    {
        // このスレッドでコンテキストをアクティブ化
        Win32Interop.wglMakeCurrent(_hdc, _hglrc);

        // 画面をクリア（背景色を設定）
        Win32Interop.glClearColor(0.2f, 0.3f, 0.4f, 1.0f);
        Win32Interop.glClear(Win32Interop.GL_COLOR_BUFFER_BIT | Win32Interop.GL_DEPTH_BUFFER_BIT);

        // 回転する三角形を描画
        _rotation += 1.0f;
        Win32Interop.glRotatef(_rotation, 0f, 0f, 1f);

        Win32Interop.glBegin(Win32Interop.GL_TRIANGLES);

        // 赤
        Win32Interop.glColor3f(1.0f, 0.0f, 0.0f);
        Win32Interop.glVertex3f(0.0f, 0.5f, 0.0f);

        // 緑
        Win32Interop.glColor3f(0.0f, 1.0f, 0.0f);
        Win32Interop.glVertex3f(-0.5f, -0.5f, 0.0f);

        // 青
        Win32Interop.glColor3f(0.0f, 0.0f, 1.0f);
        Win32Interop.glVertex3f(0.5f, -0.5f, 0.0f);

        Win32Interop.glEnd();

        // バッファをスワップ
        Win32Interop.SwapBuffers(_hdc);
    }
}
