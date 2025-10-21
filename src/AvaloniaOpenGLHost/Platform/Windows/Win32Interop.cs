using System;
using System.Runtime.InteropServices;

namespace AvaloniaOpenGLHost.Platform.Windows;

/// <summary>
/// Windows Win32 API の P/Invoke 定義
/// </summary>
internal static class Win32Interop
{
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const uint WS_CLIPSIBLINGS = 0x04000000;

    public const int PFD_DOUBLEBUFFER = 0x00000001;
    public const int PFD_DRAW_TO_WINDOW = 0x00000004;
    public const int PFD_SUPPORT_OPENGL = 0x00000020;
    public const int PFD_TYPE_RGBA = 0;
    public const int PFD_MAIN_PLANE = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits;
        public byte cRedShift;
        public byte cGreenBits;
        public byte cGreenShift;
        public byte cBlueBits;
        public byte cBlueShift;
        public byte cAlphaBits;
        public byte cAlphaShift;
        public byte cAccumBits;
        public byte cAccumRedBits;
        public byte cAccumGreenBits;
        public byte cAccumBlueBits;
        public byte cAccumAlphaBits;
        public byte cDepthBits;
        public byte cStencilBits;
        public byte cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask;
        public uint dwVisibleMask;
        public uint dwDamageMask;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);

    [DllImport("gdi32.dll")]
    public static extern bool SetPixelFormat(IntPtr hdc, int iPixelFormat, ref PIXELFORMATDESCRIPTOR pfd);

    [DllImport("gdi32.dll")]
    public static extern bool SwapBuffers(IntPtr hdc);

    [DllImport("opengl32.dll")]
    public static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll")]
    public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

    [DllImport("opengl32.dll")]
    public static extern bool wglDeleteContext(IntPtr hglrc);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    // OpenGL 関数
    [DllImport("opengl32.dll")]
    public static extern void glViewport(int x, int y, int width, int height);

    [DllImport("opengl32.dll")]
    public static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport("opengl32.dll")]
    public static extern void glClear(uint mask);

    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_DEPTH_BUFFER_BIT = 0x00000100;

    [DllImport("opengl32.dll")]
    public static extern void glBegin(uint mode);

    [DllImport("opengl32.dll")]
    public static extern void glEnd();

    [DllImport("opengl32.dll")]
    public static extern void glVertex3f(float x, float y, float z);

    [DllImport("opengl32.dll")]
    public static extern void glColor3f(float red, float green, float blue);

    [DllImport("opengl32.dll")]
    public static extern void glRotatef(float angle, float x, float y, float z);

    public const uint GL_TRIANGLES = 0x0004;

    /// <summary>
    /// 子ウィンドウを作成
    /// </summary>
    public static IntPtr CreateChildWindow(IntPtr parentHwnd)
    {
        var hwnd = CreateWindowEx(
            0,
            "Static",
            "",
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 1, 1,
            parentHwnd,
            IntPtr.Zero,
            GetModuleHandle(null!),
            IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create child window. Error: {Marshal.GetLastWin32Error()}");
        }

        return hwnd;
    }
}
