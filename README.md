# Avalonia Native Control Host with OpenGL & Vulkan

AvaloniaのNativeControlHostを使用して、別スレッドの独立したOpenGL/Vulkanコンテキストでレンダリングを行うサンプルプロジェクトです。

## 概要

このプロジェクトは、Avalonia内部で使用されているOpenGL/Skiaとは完全に独立した、別スレッドのGPUコンテキストを使用してレンダリングを行う方法を示しています。OpenGL と Vulkan の両バックエンドで SkiaSharp を利用するサンプルを含みます。

### 主な特徴

- **独立したOpenGLコンテキスト**: Avalonia内部の描画パイプラインとは分離
- **別スレッドレンダリング**: 専用のレンダリングスレッドで60FPSのループを実行
- **クロスプラットフォーム対応**: Windows、Linux (X11)、macOS をサポート
- **NativeControlHost使用**: ネイティブウィンドウハンドルを取得して活用
- **SkiaSharp + Vulkan**: Windows/Linux で SkiaSharp Vulkan バックエンドを利用した描画（プレビュー）

## アーキテクチャ

### 実装方針

このプロジェクトは、ChatGPTから提供された方針に基づいて実装されています：

1. **NativeControlHostを継承**: プラットフォーム固有の子ハンドル（Windows: HWND / macOS: NSView / X11: Window）を作成
2. **独立したOpenGLコンテキスト**: 子ハンドルに紐づく自前のOpenGLコンテキストを別スレッドで作成
3. **完全な分離**: Avalonia側のGL/Skiaとは完全に分離され、レイアウトはAvalonia、描画は専用スレッドで処理

### プロジェクト構造

```
src/
├── AvaloniaOpenGLHost/           # メインライブラリ
│   ├── Controls/
│   │   ├── GlHost.cs             # OpenGL 用 NativeControlHost
│   │   └── VulkanHost.cs         # Vulkan 用 NativeControlHost
│   ├── Rendering/
│   │   ├── IGlRenderer.cs        # レンダラーインターフェース
│   │   └── GlRendererBase.cs    # 基底レンダラークラス（スレッド管理）
│   └── Platform/
│       ├── Windows/
│       │   ├── Win32Interop.cs          # Windows P/Invoke定義
│       │   ├── WindowsGlRenderer.cs     # Windows OpenGL 実装
│       │   └── WindowsVulkanRenderer.cs # Windows Vulkan 実装
│       ├── Linux/
│       │   ├── X11Interop.cs            # X11/GLX P/Invoke定義
│       │   ├── LinuxGlRenderer.cs       # Linux OpenGL 実装
│       │   └── LinuxVulkanRenderer.cs   # Linux Vulkan 実装
│       └── MacOS/
│           ├── CocoaInterop.cs      # Cocoa P/Invoke定義
│           └── MacOSGlRenderer.cs   # macOS実装
└── AvaloniaOpenGLHost.Sample/    # サンプルアプリケーション
    ├── Program.cs
    ├── App.axaml
    ├── MainWindow.axaml          # OpenGL/Vulkan ホストを切り替えるメインウィンドウ
    └── MainWindow.axaml.cs
```

## 使用方法

### 必要な環境

- .NET 8.0 SDK以降
- Windows: Visual Studio 2022 または .NET SDK（Vulkan を利用する場合は Vulkan Runtime / SDK が必要）
- Linux: X11 開発ライブラリ (`libX11-dev`, `libGL-dev`, `libvulkan-dev`)
- macOS: Xcode Command Line Tools

### ビルド

```bash
# ソリューション全体をビルド
dotnet build

# サンプルアプリケーションを実行
dotnet run --project src/AvaloniaOpenGLHost.Sample
```

### コードでの使用例

XAML:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:controls="clr-namespace:AvaloniaOpenGLHost.Controls;assembly=AvaloniaOpenGLHost">

    <TabControl>
        <TabItem Header="OpenGL">
            <controls:GlHost Width="800" Height="600" />
        </TabItem>
        <TabItem Header="Vulkan">
            <controls:VulkanHost Width="800" Height="600" />
        </TabItem>
    </TabControl>

</Window>
```

C#:
```csharp
using AvaloniaOpenGLHost.Controls;

var tabControl = new TabControl
{
    Items =
    {
        new TabItem { Header = "OpenGL", Content = new GlHost { Width = 800, Height = 600 } },
        new TabItem { Header = "Vulkan", Content = new VulkanHost { Width = 800, Height = 600 } }
    }
};
```

## 技術的な詳細

### プラットフォーム別の実装

#### Windows
- `CreateWindowEx` で子ウィンドウ（HWND）を作成
- WGL（Windows OpenGL）を使用
- `wglCreateContext` でコンテキストを作成
- `SwapBuffers` でダブルバッファリング
- Vulkan 版では `VK_KHR_win32_surface` を利用し、SkiaSharp の `GRSharpVkBackendContext` を通じて VkQueue に描画

#### Linux (X11)
- X11の既存ウィンドウハンドルを使用
- GLX（OpenGL Extension to X11）を使用
- `glXCreateContext` でコンテキストを作成
- `glXSwapBuffers` でダブルバッファリング
- Vulkan 版では `VK_KHR_xlib_surface` を使用し、SkiaSharp の Vulkan バックエンドで描画

#### macOS
- NSViewハンドルを使用
- NSOpenGLContextを作成
- `flushBuffer` でレンダリング結果を反映

### レンダリングスレッド

- 専用のバックグラウンドスレッドで60FPSのレンダリングループを実行
- `GlRendererBase` クラスがスレッド管理を担当
- 各プラットフォーム実装は `PlatformRender()` メソッドをオーバーライド

### サイズ変更の処理

- Avaloniaの `BoundsProperty` を監視
- サイズ変更時に `IGlRenderer.Resize()` を呼び出し
- レンダリングスレッドで `glViewport` を更新

## 注意事項と制約

### Airspace問題
NativeControlHostは「Airspace問題」を持ちます：
- ネイティブコントロールは常にAvaloniaコントロールの上に描画される
- 半透明でオーバーレイすることはできない
- Z順序の制御が制限される

### プラットフォーム差異
- Windows: 最も安定して動作
- Linux: X11環境で動作、Waylandは未対応
- macOS: OpenGLが非推奨（Metalへの移行が推奨されている）

### パフォーマンス考慮事項
- レンダリングは別スレッドで実行されるため、UIスレッドをブロックしない
- フレームレートは60FPSに制限（`Thread.Sleep(16)`）
- より高度な同期が必要な場合はVSync対応を検討

## カスタマイズ

### レンダリング内容の変更

各プラットフォームの `PlatformRender()` メソッドを変更することで、描画内容をカスタマイズできます：

```csharp
protected override void PlatformRender()
{
    // コンテキストをアクティブ化
    Win32Interop.wglMakeCurrent(_hdc, _hglrc);

    // クリア
    Win32Interop.glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
    Win32Interop.glClear(Win32Interop.GL_COLOR_BUFFER_BIT);

    // カスタム描画処理をここに追加
    // ...

    // バッファをスワップ
    Win32Interop.SwapBuffers(_hdc);
}
```

### フレームレートの変更

`GlRendererBase.RenderLoop()` の `Thread.Sleep(16)` を変更：

```csharp
// 120 FPS の場合
Thread.Sleep(8);

// 30 FPS の場合
Thread.Sleep(33);
```

## 参考資料

- [Avalonia NativeControlHost ドキュメント](https://docs.avaloniaui.net/docs/concepts/nativecontrolhost)
- [OpenGL Programming Guide](https://www.opengl.org/documentation/)
- WGL (Windows): [Microsoft Docs](https://learn.microsoft.com/en-us/windows/win32/opengl/wgl)
- GLX (Linux): [X.Org Documentation](https://www.x.org/releases/current/doc/libGL/glx.html)
- NSOpenGLContext (macOS): [Apple Developer Documentation](https://developer.apple.com/documentation/appkit/nsopenglcontext)

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](LICENSE)ファイルを参照してください。

## 貢献

プルリクエストを歓迎します。大きな変更を行う場合は、まずissueを開いて変更内容を議論してください。

## トラブルシューティング

### Windows
- エラー: "Failed to create child window"
  - アプリケーションマニフェストが正しく設定されているか確認
  - DPI設定を確認

### Linux
- エラー: "Failed to open X11 display"
  - X11サーバーが起動しているか確認
  - `DISPLAY` 環境変数が設定されているか確認
  - 必要なライブラリがインストールされているか確認: `sudo apt-get install libx11-dev libgl-dev`

### macOS
- エラー: "Failed to create NSOpenGLContext"
  - macOS 10.14以降ではOpenGLが非推奨
  - Metalへの移行を検討

## 今後の展開

- [ ] Waylandサポート
- [ ] Metal（macOS）サポート
- [ ] Vulkanサポート
- [ ] より高度なレンダリングサンプル（テクスチャ、シェーダー等）
- [ ] パフォーマンス最適化（VSync、Triple Buffering等）
