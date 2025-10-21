using System;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace AvaloniaOpenGLHost.Rendering;

/// <summary>
/// SkiaSharp を使用して OpenGL フレームバッファへ描画するレンダラー基底クラス。
/// プラットフォーム固有の OpenGL コンテキストの管理は派生クラスが実装します。
/// </summary>
public abstract class SkiaGlRendererBase : GlRendererBase
{
    private GRGlInterface? _glInterface;
    private GRDirectContext? _directContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private int _currentWidth;
    private int _currentHeight;
    private float _rotation;

    /// <summary>
    /// OpenGL コンテキストの作成。
    /// </summary>
    protected abstract void CreateContext(IntPtr windowHandle);

    /// <summary>
    /// OpenGL コンテキストを破棄。
    /// </summary>
    protected abstract void DestroyContext();

    /// <summary>
    /// 現在のスレッドで OpenGL コンテキストをアクティブ化。
    /// </summary>
    protected abstract void MakeCurrent();

    /// <summary>
    /// 描画結果を画面へ転送。
    /// </summary>
    protected abstract void SwapBuffers();

    /// <summary>
    /// 対応するフレームバッファ情報。
    /// </summary>
    protected virtual GRGlFramebufferInfo GetFramebufferInfo() => new(0, FramebufferFormat);

    /// <summary>
    /// カラー形式。
    /// </summary>
    protected virtual SKColorType ColorType => SKColorType.Rgba8888;

    /// <summary>
    /// GL フレームバッファのフォーマット。
    /// </summary>
    protected virtual uint FramebufferFormat => ColorType.ToGlSizedFormat();

    /// <summary>
    /// サンプル数。
    /// </summary>
    protected virtual int SampleCount => 0;

    /// <summary>
    /// ステンシルバッファのビット数。
    /// </summary>
    protected virtual int StencilBits => 8;

    /// <summary>
    /// サーフェスの原点。
    /// </summary>
    protected virtual GRSurfaceOrigin SurfaceOrigin => GRSurfaceOrigin.BottomLeft;

    /// <summary>
    /// リサイズ後に追加処理が必要な場合にオーバーライドします。
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    protected virtual void OnAfterResize(int width, int height)
    {
    }

    protected override void PlatformInitialize(IntPtr windowHandle)
    {
        CreateContext(windowHandle);
    }

    protected override void PlatformCleanup()
    {
        try
        {
            MakeCurrent();
        }
        catch
        {
            // コンテキストの再アタッチに失敗しても、破棄処理は継続する。
        }

        DisposeSkiaResources();
        DestroyContext();
    }

    protected override void PlatformResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        if (width == _currentWidth && height == _currentHeight && _surface != null)
            return;

        _currentWidth = width;
        _currentHeight = height;

        MakeCurrent();
        DisposeSurface();

        if (_directContext != null)
        {
            _directContext.ResetContext();
        }

        OnAfterResize(width, height);
    }

    protected override void PlatformRender()
    {
        if (_currentWidth <= 0 || _currentHeight <= 0)
            return;

        MakeCurrent();

        if (!EnsureSurface())
            return;

        DrawFrame(_surface!);

        _surface!.Canvas.Flush();
        _directContext?.Flush();

        SwapBuffers();
    }

    private bool EnsureSurface()
    {
        if (_surface != null)
            return true;

        if (_currentWidth <= 0 || _currentHeight <= 0)
            return false;

        EnsureSkiaContext();

        var framebuffer = GetFramebufferInfo();
        _renderTarget = new GRBackendRenderTarget(
            _currentWidth,
            _currentHeight,
            SampleCount,
            StencilBits,
            framebuffer);

        _surface = SKSurface.Create(_directContext!, _renderTarget!, SurfaceOrigin, ColorType);

        return _surface != null;
    }

    private void EnsureSkiaContext()
    {
        _glInterface ??= GRGlInterface.CreateNativeGlInterface()
            ?? throw new InvalidOperationException("Failed to create SkiaSharp GL interface.");

        _directContext ??= GRDirectContext.CreateGl(_glInterface)
            ?? throw new InvalidOperationException("Failed to create SkiaSharp direct context.");
    }

    private void DrawFrame(SKSurface surface)
    {
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(0x33, 0x55, 0x66));

        canvas.Save();
        canvas.Translate(_currentWidth / 2f, _currentHeight / 2f);
        _rotation = (_rotation + 1f) % 360f;
        canvas.RotateDegrees(_rotation);

        float radius = MathF.Min(_currentWidth, _currentHeight) * 0.35f;

        using var triangle = new SKPath();
        triangle.MoveTo(0, -radius);
        triangle.LineTo(-radius * 0.8660254f, radius * 0.5f);
        triangle.LineTo(radius * 0.8660254f, radius * 0.5f);
        triangle.Close();

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, -radius),
            new SKPoint(0, radius),
            new[]
            {
                new SKColor(0xFF, 0x57, 0x22),
                new SKColor(0x29, 0xB6, 0xF6),
                new SKColor(0x66, 0xBB, 0x6A)
            },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = shader
        };

        canvas.DrawPath(triangle, paint);
        canvas.Restore();

        using var textPaint = new SKPaint
        {
            Color = new SKColor(0xEC, 0xF0, 0xF1),
            IsAntialias = true,
            TextSize = MathF.Max(16f, MathF.Min(_currentWidth, _currentHeight) * 0.06f)
        };

        const string message = "Rendered with SkiaSharp";
        float textWidth = textPaint.MeasureText(message);
        var metrics = textPaint.FontMetrics;
        float baseline = _currentHeight - MathF.Max(24f, _currentHeight * 0.05f) - metrics.Descent;
        float textX = (_currentWidth - textWidth) / 2f;

        canvas.DrawText(message, textX, baseline, textPaint);
    }

    private void DisposeSurface()
    {
        _surface?.Dispose();
        _surface = null;

        _renderTarget?.Dispose();
        _renderTarget = null;
    }

    private void DisposeSkiaResources()
    {
        DisposeSurface();

        if (_directContext != null)
        {
            _directContext.Flush();
            _directContext.Dispose();
            _directContext = null;
        }

        _glInterface?.Dispose();
        _glInterface = null;
    }
}
