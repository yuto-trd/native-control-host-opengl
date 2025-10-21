using System;
using System.Threading;

namespace AvaloniaOpenGLHost.Rendering;

/// <summary>
/// OpenGLレンダラーの基底クラス
/// 別スレッドでのレンダリングループを管理
/// </summary>
public abstract class GlRendererBase : IGlRenderer
{
    private Thread? _renderThread;
    private volatile bool _running;
    private volatile bool _initialized;
    private int _width;
    private int _height;
    private readonly object _sizeLock = new();

    public bool IsRunning => _running;

    /// <summary>
    /// プラットフォーム固有の初期化処理
    /// </summary>
    protected abstract void PlatformInitialize(IntPtr windowHandle);

    /// <summary>
    /// プラットフォーム固有のクリーンアップ処理
    /// </summary>
    protected abstract void PlatformCleanup();

    /// <summary>
    /// プラットフォーム固有のレンダリング処理
    /// </summary>
    protected abstract void PlatformRender();

    /// <summary>
    /// プラットフォーム固有のリサイズ処理
    /// </summary>
    protected abstract void PlatformResize(int width, int height);

    public void Initialize(IntPtr windowHandle)
    {
        if (_initialized)
            throw new InvalidOperationException("Already initialized");

        PlatformInitialize(windowHandle);
        _initialized = true;
    }

    public void Resize(int width, int height)
    {
        lock (_sizeLock)
        {
            _width = width;
            _height = height;
        }
    }

    public void Start()
    {
        if (!_initialized)
            throw new InvalidOperationException("Not initialized");

        if (_running)
            return;

        _running = true;
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "OpenGL Render Thread"
        };
        _renderThread.Start();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        _renderThread?.Join();
        _renderThread = null;
    }

    public void Render()
    {
        // レンダリングスレッドから呼ばれる
        int width, height;
        lock (_sizeLock)
        {
            width = _width;
            height = _height;
        }

        if (width > 0 && height > 0)
        {
            PlatformResize(width, height);
            PlatformRender();
        }
    }

    private void RenderLoop()
    {
        try
        {
            while (_running)
            {
                Render();

                // フレームレートを制限 (60 FPS)
                Thread.Sleep(16);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Render loop error: {ex}");
        }
    }

    public void Dispose()
    {
        Stop();

        if (_initialized)
        {
            PlatformCleanup();
            _initialized = false;
        }

        GC.SuppressFinalize(this);
    }
}
