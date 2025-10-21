using System;

namespace AvaloniaOpenGLHost.Rendering;

/// <summary>
/// OpenGLレンダラーのインターフェース
/// </summary>
public interface IGlRenderer : IDisposable
{
    /// <summary>
    /// OpenGLコンテキストを初期化
    /// </summary>
    /// <param name="windowHandle">描画先のネイティブウィンドウハンドル</param>
    void Initialize(IntPtr windowHandle);

    /// <summary>
    /// ビューポートのサイズを設定
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    void Resize(int width, int height);

    /// <summary>
    /// 1フレームをレンダリング
    /// </summary>
    void Render();

    /// <summary>
    /// レンダリングループを開始
    /// </summary>
    void Start();

    /// <summary>
    /// レンダリングループを停止
    /// </summary>
    void Stop();

    /// <summary>
    /// レンダラーが実行中かどうか
    /// </summary>
    bool IsRunning { get; }
}
