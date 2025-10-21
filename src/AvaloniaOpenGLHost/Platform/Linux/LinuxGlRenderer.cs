using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.Linux;

/// <summary>
/// Linux (X11) プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class LinuxGlRenderer : GlRendererBase
{
    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _glxContext;
    private float _rotation = 0f;

    protected override void PlatformInitialize(IntPtr windowHandle)
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

    protected override void PlatformCleanup()
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
    }

    protected override void PlatformResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        // このスレッドでコンテキストをアクティブ化
        X11Interop.glXMakeCurrent(_display, _window, _glxContext);

        // ビューポートを設定
        X11Interop.glViewport(0, 0, width, height);
    }

    protected override void PlatformRender()
    {
        // このスレッドでコンテキストをアクティブ化
        X11Interop.glXMakeCurrent(_display, _window, _glxContext);

        // 画面をクリア
        X11Interop.glClearColor(0.2f, 0.3f, 0.4f, 1.0f);
        X11Interop.glClear(X11Interop.GL_COLOR_BUFFER_BIT | X11Interop.GL_DEPTH_BUFFER_BIT);

        // 回転する三角形を描画
        _rotation += 1.0f;
        X11Interop.glRotatef(_rotation, 0f, 0f, 1f);

        X11Interop.glBegin(X11Interop.GL_TRIANGLES);

        // 赤
        X11Interop.glColor3f(1.0f, 0.0f, 0.0f);
        X11Interop.glVertex3f(0.0f, 0.5f, 0.0f);

        // 緑
        X11Interop.glColor3f(0.0f, 1.0f, 0.0f);
        X11Interop.glVertex3f(-0.5f, -0.5f, 0.0f);

        // 青
        X11Interop.glColor3f(0.0f, 0.0f, 1.0f);
        X11Interop.glVertex3f(0.5f, -0.5f, 0.0f);

        X11Interop.glEnd();

        // バッファをスワップ
        X11Interop.glXSwapBuffers(_display, _window);
    }
}
