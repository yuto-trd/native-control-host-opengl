using System;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Platform.MacOS;

/// <summary>
/// macOS プラットフォーム向けの OpenGL レンダラー
/// </summary>
public class MacOSGlRenderer : GlRendererBase
{
    private IntPtr _nsView;
    private IntPtr _nsOpenGLContext;
    private float _rotation = 0f;

    protected override void PlatformInitialize(IntPtr windowHandle)
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

    protected override void PlatformCleanup()
    {
        if (_nsOpenGLContext != IntPtr.Zero)
        {
            CocoaInterop.SendMessage(_nsOpenGLContext, "clearCurrentContext");
            CocoaInterop.SendMessage(_nsOpenGLContext, "release");
            _nsOpenGLContext = IntPtr.Zero;
        }
    }

    protected override void PlatformResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        // コンテキストをアクティブ化
        CocoaInterop.SendMessage(_nsOpenGLContext, "makeCurrentContext");

        // ビューポートを設定
        CocoaInterop.glViewport(0, 0, width, height);

        // コンテキストを更新
        CocoaInterop.SendMessage(_nsOpenGLContext, "update");
    }

    protected override void PlatformRender()
    {
        // コンテキストをアクティブ化
        CocoaInterop.SendMessage(_nsOpenGLContext, "makeCurrentContext");

        // 画面をクリア
        CocoaInterop.glClearColor(0.2f, 0.3f, 0.4f, 1.0f);
        CocoaInterop.glClear(CocoaInterop.GL_COLOR_BUFFER_BIT | CocoaInterop.GL_DEPTH_BUFFER_BIT);

        // 回転する三角形を描画
        _rotation += 1.0f;
        CocoaInterop.glRotatef(_rotation, 0f, 0f, 1f);

        CocoaInterop.glBegin(CocoaInterop.GL_TRIANGLES);

        // 赤
        CocoaInterop.glColor3f(1.0f, 0.0f, 0.0f);
        CocoaInterop.glVertex3f(0.0f, 0.5f, 0.0f);

        // 緑
        CocoaInterop.glColor3f(0.0f, 1.0f, 0.0f);
        CocoaInterop.glVertex3f(-0.5f, -0.5f, 0.0f);

        // 青
        CocoaInterop.glColor3f(0.0f, 0.0f, 1.0f);
        CocoaInterop.glVertex3f(0.5f, -0.5f, 0.0f);

        CocoaInterop.glEnd();

        // バッファをフラッシュ
        CocoaInterop.glFlush();
        CocoaInterop.SendMessage(_nsOpenGLContext, "flushBuffer");
    }
}
