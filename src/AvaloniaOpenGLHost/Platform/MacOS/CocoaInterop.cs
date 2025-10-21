using System;
using System.Runtime.InteropServices;

namespace AvaloniaOpenGLHost.Platform.MacOS;

/// <summary>
/// macOS Cocoa および OpenGL の P/Invoke 定義
/// </summary>
internal static class CocoaInterop
{
    // Objective-C Runtime
    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, double arg1);

    [DllImport("/usr/lib/libobjc.dylib")]
    public static extern void objc_msgSend_stret(out NSRect retval, IntPtr receiver, IntPtr selector);

    [StructLayout(LayoutKind.Sequential)]
    public struct NSRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public NSRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    // OpenGL 関数（macOS）
    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glViewport(int x, int y, int width, int height);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glClearColor(float red, float green, float blue, float alpha);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glClear(uint mask);

    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_DEPTH_BUFFER_BIT = 0x00000100;

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glBegin(uint mode);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glEnd();

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glVertex3f(float x, float y, float z);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glColor3f(float red, float green, float blue);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glRotatef(float angle, float x, float y, float z);

    [DllImport("/System/Library/Frameworks/OpenGL.framework/OpenGL")]
    public static extern void glFlush();

    public const uint GL_TRIANGLES = 0x0004;

    // ヘルパーメソッド
    public static IntPtr GetClass(string name) => objc_getClass(name);
    public static IntPtr GetSelector(string name) => sel_registerName(name);

    public static IntPtr SendMessage(IntPtr receiver, string selector)
        => objc_msgSend(receiver, GetSelector(selector));

    public static IntPtr SendMessage(IntPtr receiver, string selector, IntPtr arg1)
        => objc_msgSend(receiver, GetSelector(selector), arg1);

    public static IntPtr SendMessage(IntPtr receiver, string selector, double arg1)
        => objc_msgSend(receiver, GetSelector(selector), arg1);

    public static NSRect GetFrame(IntPtr view)
    {
        objc_msgSend_stret(out NSRect frame, view, GetSelector("frame"));
        return frame;
    }
}
