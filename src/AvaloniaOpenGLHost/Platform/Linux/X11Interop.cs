using System;
using System.Runtime.InteropServices;

namespace AvaloniaOpenGLHost.Platform.Linux;

/// <summary>
/// X11 および GLX の P/Invoke 定義
/// </summary>
internal static class X11Interop
{
    // X11 基本型
    public struct Display { }
    public struct Window { }
    public struct XVisualInfo { }
    public struct GLXContext { }

    // X11 定数
    public const int CWBackPixel = 1 << 1;
    public const int CWBorderPixel = 1 << 3;
    public const int CWColormap = 1 << 13;

    // GLX 定数
    public const int GLX_RGBA = 4;
    public const int GLX_DOUBLEBUFFER = 5;
    public const int GLX_DEPTH_SIZE = 12;
    public const int GLX_RED_SIZE = 8;
    public const int GLX_GREEN_SIZE = 9;
    public const int GLX_BLUE_SIZE = 10;

    [StructLayout(LayoutKind.Sequential)]
    public struct XSetWindowAttributes
    {
        public IntPtr background_pixmap;
        public ulong background_pixel;
        public IntPtr border_pixmap;
        public ulong border_pixel;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public int save_under;
        public long event_mask;
        public long do_not_propagate_mask;
        public int override_redirect;
        public IntPtr colormap;
        public IntPtr cursor;
    }

    // X11 関数
    [DllImport("libX11.so.6")]
    public static extern IntPtr XOpenDisplay(IntPtr display_name);

    [DllImport("libX11.so.6")]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern int XDefaultScreen(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XRootWindow(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XCreateWindow(
        IntPtr display,
        IntPtr parent,
        int x,
        int y,
        uint width,
        uint height,
        uint border_width,
        int depth,
        uint @class,
        IntPtr visual,
        ulong valuemask,
        ref XSetWindowAttributes attributes);

    [DllImport("libX11.so.6")]
    public static extern int XDestroyWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    public static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    public static extern int XFlush(IntPtr display);

    // GLX 関数
    [DllImport("libGL.so.1")]
    public static extern IntPtr glXChooseVisual(IntPtr display, int screen, int[] attribList);

    [DllImport("libGL.so.1")]
    public static extern IntPtr glXCreateContext(IntPtr display, IntPtr vis, IntPtr shareList, bool direct);

    [DllImport("libGL.so.1")]
    public static extern void glXDestroyContext(IntPtr display, IntPtr ctx);

    [DllImport("libGL.so.1")]
    public static extern bool glXMakeCurrent(IntPtr display, IntPtr drawable, IntPtr ctx);

    [DllImport("libGL.so.1")]
    public static extern void glXSwapBuffers(IntPtr display, IntPtr drawable);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XCreateColormap(IntPtr display, IntPtr window, IntPtr visual, int alloc);

    // OpenGL 関数
    [DllImport("libGL.so.1")]
    public static extern void glViewport(int x, int y, int width, int height);

    [DllImport("libGL.so.1")]
    public static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport("libGL.so.1")]
    public static extern void glClear(uint mask);

    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_DEPTH_BUFFER_BIT = 0x00000100;

    [DllImport("libGL.so.1")]
    public static extern void glBegin(uint mode);

    [DllImport("libGL.so.1")]
    public static extern void glEnd();

    [DllImport("libGL.so.1")]
    public static extern void glVertex3f(float x, float y, float z);

    [DllImport("libGL.so.1")]
    public static extern void glColor3f(float red, float green, float blue);

    [DllImport("libGL.so.1")]
    public static extern void glRotatef(float angle, float x, float y, float z);

    public const uint GL_TRIANGLES = 0x0004;
}
