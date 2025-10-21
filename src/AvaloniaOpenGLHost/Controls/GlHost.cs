using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AvaloniaOpenGLHost.Platform.Linux;
using AvaloniaOpenGLHost.Platform.MacOS;
using AvaloniaOpenGLHost.Platform.Windows;
using AvaloniaOpenGLHost.Rendering;

namespace AvaloniaOpenGLHost.Controls;

/// <summary>
/// ネイティブウィンドウハンドルを使用して独立した OpenGL コンテキストでレンダリングするコントロール
/// Avalonia の描画パイプラインとは完全に分離された、別スレッドの OpenGL レンダリングを提供します
/// </summary>
public sealed class GlHost : NativeControlHost
{
    private IGlRenderer? _renderer;
    private IntPtr _nativeHandle = IntPtr.Zero;

    static GlHost()
    {
        // Bounds プロパティが変更された時にリサイズ処理を行う
        BoundsProperty.Changed.AddClassHandler<GlHost>((control, args) =>
        {
            control.OnBoundsChanged(args);
        });
    }

    /// <summary>
    /// ネイティブコントロールを作成
    /// </summary>
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        IPlatformHandle handle;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: 子ウィンドウを作成
            _nativeHandle = Win32Interop.CreateChildWindow(parent.Handle);
            handle = new PlatformHandle(_nativeHandle, "HWND");

            // Windows 用レンダラーを作成
            _renderer = new WindowsGlRenderer();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: X11 Window を使用
            // 注意: Avalonia が提供する既存のハンドルを使用
            // 実際の本番環境では、別途子ウィンドウを作成することを推奨
            _nativeHandle = parent.Handle;
            handle = new PlatformHandle(_nativeHandle, parent.HandleDescriptor);

            // Linux 用レンダラーを作成
            _renderer = new LinuxGlRenderer();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: NSView を使用
            _nativeHandle = parent.Handle;
            handle = new PlatformHandle(_nativeHandle, "NSView");

            // macOS 用レンダラーを作成
            _renderer = new MacOSGlRenderer();
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported");
        }

        // レンダラーを初期化して開始
        try
        {
            _renderer.Initialize(_nativeHandle);

            // 初期サイズを設定
            var bounds = Bounds;
            _renderer.Resize((int)bounds.Width, (int)bounds.Height);

            // レンダリングループを開始
            _renderer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize renderer: {ex}");
            _renderer?.Dispose();
            _renderer = null;
            throw;
        }

        return handle;
    }

    /// <summary>
    /// ネイティブコントロールを破棄
    /// </summary>
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // レンダラーを停止して破棄
        _renderer?.Stop();
        _renderer?.Dispose();
        _renderer = null;

        // Windows の場合は子ウィンドウを破棄
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _nativeHandle != IntPtr.Zero)
        {
            Win32Interop.DestroyWindow(_nativeHandle);
        }

        _nativeHandle = IntPtr.Zero;

        base.DestroyNativeControlCore(control);
    }

    /// <summary>
    /// Bounds が変更された時の処理
    /// </summary>
    private void OnBoundsChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (_renderer == null)
            return;

        var bounds = (Rect)args.NewValue!;
        var width = (int)bounds.Width;
        var height = (int)bounds.Height;

        if (width > 0 && height > 0)
        {
            _renderer.Resize(width, height);
        }
    }

    /// <summary>
    /// デタッチ時の処理
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // レンダラーを停止
        _renderer?.Stop();
    }

    /// <summary>
    /// アタッチ時の処理
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // レンダラーが既に作成されている場合は再開
        if (_renderer != null && !_renderer.IsRunning)
        {
            _renderer.Start();
        }
    }
}
