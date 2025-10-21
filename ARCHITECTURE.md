# アーキテクチャドキュメント

## 概要

このプロジェクトは、AvaloniaのNativeControlHostを活用して、Avalonia内部のOpenGL/Skia描画パイプラインとは完全に独立した、別スレッドのGPUコンテキストでレンダリングを実現します。OpenGL に加え、SkiaSharp の Vulkan バックエンドを利用した描画サンプルも提供します。

## 設計原則

### 1. 分離の原則

Avalonia側の描画システムと、このカスタムOpenGLレンダリングは完全に分離されています：

- **Avaloniaの責務**: レイアウト、イベント処理、UI構造の管理
- **カスタムOpenGLの責務**: 独立したレンダリング処理

### 2. プラットフォーム抽象化

各プラットフォーム（Windows/Linux/macOS）の違いを抽象化し、共通のインターフェース（`IGlRenderer`）で統一しています。

```
┌─────────────────────────────────────────────┐
│            GlHost (NativeControlHost)        │
│  - プラットフォーム検出                        │
│  - ネイティブハンドル管理                      │
│  - レンダラー生成                             │
└─────────────────┬───────────────────────────┘
                  │
                  │ 使用
                  │
┌─────────────────▼───────────────────────────┐
│           IGlRenderer Interface              │
│  - Initialize(IntPtr handle)                 │
│  - Resize(int width, int height)             │
│  - Render()                                  │
│  - Start() / Stop()                          │
└─────────────────┬───────────────────────────┘
                  │
                  │ 実装
                  │
      ┌───────────┴───────────┐
      │                       │
      ▼                       ▼
┌─────────────┐         ┌─────────────┐
│GlRendererBase│◄────────│Platform Impl│
│ - スレッド管理│         │ - Windows   │
│ - FPS制御   │         │ - Linux     │
│ - 基本制御  │         │ - macOS     │
└─────────────┘         └─────────────┘
```

## コンポーネント詳細

### GlHost

`NativeControlHost`を継承したメインコントロール。

**責務**:
- プラットフォームの検出（Windows/Linux/macOS）
- 適切なレンダラーの生成
- ネイティブウィンドウハンドルの生成と管理
- Avaloniaのレイアウトシステムとの統合（サイズ変更等）

**ライフサイクル**:
```
CreateNativeControlCore()
  ↓
プラットフォーム判定
  ↓
ネイティブハンドル作成
  ↓
レンダラー初期化
  ↓
レンダリング開始
  ↓
[実行中...]
  ↓
DestroyNativeControlCore()
  ↓
レンダラー停止
  ↓
リソース解放
```

### VulkanHost

`VulkanHost` も `NativeControlHost` を継承し、Windows では `WindowsVulkanRenderer`、Linux では `LinuxVulkanRenderer` を生成します。SkiaSharp の Vulkan バックエンドを扱う `SkiaVulkanRendererBase` を基底にしており、OpenGL 版と同様に別スレッドでレンダリングを実行します。

### IGlRenderer / GlRendererBase

レンダラーのインターフェースと基底実装。Vulkan 版では `SkiaVulkanRendererBase` が Vulkan デバイスとスワップチェーンの構築、SkiaSharp との橋渡しを行います。

**GlRendererBaseの責務**:
- レンダリングスレッドの管理
- レンダリングループの実行（60 FPS）
- サイズ変更の同期処理
- プラットフォーム実装への委譲

**スレッドモデル**:
```
Main Thread (Avalonia UI)          Render Thread
─────────────────────────          ─────────────
Initialize()
  ├─> PlatformInitialize()
  └─> Start() ───────────────────> RenderLoop()
                                        │
Resize(w, h) ──┐                        │
               │                        ▼
               └───> _width, _height    while(_running)
                     (lock)                 │
                                            ├─> Render()
                                            │   └─> PlatformRender()
                                            │
                                            └─> Sleep(16ms)
Stop() ────────────────────────────> _running = false
                                            │
                                            ▼
                                        [終了]
```

### プラットフォーム実装

各プラットフォームで`GlRendererBase`を継承し、以下のメソッドを実装：

#### WindowsGlRenderer

**使用技術**: WGL (Windows OpenGL)

```csharp
PlatformInitialize()
  - GetDC(): デバイスコンテキスト取得
  - ChoosePixelFormat(): ピクセルフォーマット選択
  - SetPixelFormat(): ピクセルフォーマット設定
  - wglCreateContext(): OpenGLコンテキスト作成

PlatformRender()
  - wglMakeCurrent(): コンテキストアクティブ化
  - glClear(), glBegin(), glEnd()等: OpenGL描画
  - SwapBuffers(): ダブルバッファスワップ
```

#### WindowsVulkanRenderer (SkiaSharp)

- `VK_KHR_win32_surface` を用いて `VkSurfaceKHR` を生成
- SkiaSharp の `GRSharpVkBackendContext` から Vulkan キューへ描画コマンドを送信
- `SkiaVulkanRendererBase` がスワップチェーンの作成・再生成、レイアウト遷移、SkiaSurface の管理を担当

#### LinuxGlRenderer

**使用技術**: GLX (OpenGL Extension to X11)

```csharp
PlatformInitialize()
  - XOpenDisplay(): X11ディスプレイ接続
  - glXChooseVisual(): ビジュアル選択
  - glXCreateContext(): GLXコンテキスト作成

PlatformRender()
  - glXMakeCurrent(): コンテキストアクティブ化
  - glClear(), glBegin(), glEnd()等: OpenGL描画
  - glXSwapBuffers(): ダブルバッファスワップ
```

#### LinuxVulkanRenderer (SkiaSharp)

- `VK_KHR_xlib_surface` で X11 ウィンドウに紐づくサーフェスを生成
- SkiaSharp Vulkan バックエンドを用いて共通描画ロジックを実行
- スワップチェーンやイメージレイアウトの制御は `SkiaVulkanRendererBase` に集約

#### MacOSGlRenderer

**使用技術**: NSOpenGLContext (Cocoa)

```csharp
PlatformInitialize()
  - NSOpenGLPixelFormat作成
  - NSOpenGLContext作成
  - setView: ビューへの関連付け

PlatformRender()
  - makeCurrentContext: コンテキストアクティブ化
  - glClear(), glBegin(), glEnd()等: OpenGL描画
  - flushBuffer: バッファフラッシュ
```

## データフロー

### 初期化フロー

```
User creates GlHost in XAML/Code
         ↓
GlHost.CreateNativeControlCore()
         ↓
Platform detection (RuntimeInformation)
         ↓
┌─────────┬─────────┬─────────┐
│ Windows │  Linux  │  macOS  │
└────┬────┴────┬────┴────┬────┘
     │         │         │
     ├─ CreateChildWindow()
     │         │         │
     ├─────────┼─────────┤
     │  Create platform-specific renderer
     │         │         │
     └─────────┴─────────┘
              │
    renderer.Initialize(handle)
              │
    renderer.Start()
              │
    [レンダリングスレッド開始]
```

### レンダリングフロー

```
[60 FPS Loop on Render Thread]
         ↓
Get current size (with lock)
         ↓
PlatformResize() if needed
         ↓
PlatformRender()
  ├─> MakeCurrent
  ├─> Clear
  ├─> Draw Primitives
  └─> SwapBuffers
         ↓
Sleep(16ms)
         ↓
[Loop]
```

### サイズ変更フロー

```
Avalonia Layout System
         ↓
GlHost.Bounds changed
         ↓
OnBoundsChanged()
         ↓
renderer.Resize(width, height)
         ↓
Store new size (with lock)
         ↓
[Next render will use new size]
```

## スレッド安全性

### 同期ポイント

1. **サイズ変更**: `_sizeLock`でサイズ情報を保護
2. **開始/停止**: `volatile bool _running`で制御
3. **初期化状態**: `volatile bool _initialized`で管理

### OpenGLコンテキストの所有権

- OpenGLコンテキストは**レンダリングスレッド**が所有
- `MakeCurrent`は常にレンダリングスレッドで実行
- メインスレッドはOpenGL関数を直接呼ばない

## パフォーマンス考慮事項

### フレームレート制限

現在の実装では`Thread.Sleep(16)`で約60FPSに制限しています。

**改善案**:
- VSync同期の利用（`wglSwapIntervalEXT`等）
- より正確なタイミング制御（`Stopwatch`使用）
- Triple Buffering

### リソース管理

- ネイティブリソース（DC, Context等）は`Dispose`で確実に解放
- レンダリングスレッドは`IsBackground = true`でプロセス終了を妨げない

## 制約事項

### Airspace問題

NativeControlHostの制約として、ネイティブコントロールは常にAvaloniaコントロールの**上**に描画されます。

**影響**:
- 半透明オーバーレイ不可
- Z順序制御不可
- AvaloniaコントロールをGlHostの上に重ねられない

**回避策**:
- レイアウトを工夫してGlHostを独立した領域に配置
- 必要に応じてネイティブウィンドウで独自のオーバーレイを実装

### プラットフォーム差異

| プラットフォーム | 対応状況 | 備考 |
|--------------|---------|------|
| Windows | ✅ 完全対応 | WGLは安定 |
| Linux X11 | ✅ 対応 | GLX使用 |
| Linux Wayland | ❌ 未対応 | EGL対応が必要 |
| macOS | ⚠️ 対応（非推奨） | OpenGLは非推奨、Metalへの移行推奨 |

## 拡張ポイント

### カスタムレンダリング

`PlatformRender()`をオーバーライドして独自の描画処理を実装：

```csharp
public class CustomRenderer : WindowsGlRenderer
{
    protected override void PlatformRender()
    {
        base.PlatformRender(); // 基本処理

        // カスタム描画
        // - テクスチャの読み込み
        // - シェーダーの使用
        // - 3Dモデルの描画
        // etc.
    }
}
```

### レンダラーのプラグイン化

`IGlRenderer`インターフェースを実装することで、完全にカスタムなレンダラーを作成可能：

```csharp
public class VulkanRenderer : IGlRenderer
{
    // Vulkanを使用した実装
}

// GlHostで使用
public class VulkanHost : NativeControlHost
{
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _renderer = new VulkanRenderer();
        // ...
    }
}
```

## まとめ

このアーキテクチャは以下の特性を持ちます：

**✅ 利点**:
- Avaloniaの描画システムと完全に独立
- マルチスレッドで高パフォーマンス
- プラットフォーム間で統一されたインターフェース
- 拡張性が高い

**⚠️ 注意点**:
- Airspace問題による制約
- プラットフォームごとの実装差異
- ネイティブリソース管理の複雑さ

このアーキテクチャは、ゲームエンジンのビューポート、3Dモデルビューア、リアルタイム映像処理など、Avaloniaの通常の描画システムでは対応困難なユースケースに適しています。
