using System.Drawing;
using System.Drawing.Imaging;

namespace DeskWidgets;

/// <summary>
/// A premultiplied-alpha 32-bit bitmap backed by a Windows DIB section. Drawing with GDI+ into
/// <see cref="BeginGraphics"/> and then calling <see cref="Present"/> gives true per-pixel
/// transparency (soft shadows, clean rounded edges) with very little CPU or memory.
/// </summary>
internal sealed class LayeredSurface : IDisposable
{
    private IntPtr _memDc = IntPtr.Zero;
    private IntPtr _dib = IntPtr.Zero;
    private IntPtr _oldObject = IntPtr.Zero;
    private IntPtr _bits = IntPtr.Zero;
    private Bitmap? _bitmap;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>Makes sure the surface is exactly this size. Returns false if Windows could not allocate it.</summary>
    public bool Ensure(int width, int height)
    {
        if (width <= 0 || height <= 0) return false;
        if (_bitmap != null && width == Width && height == Height) return true;

        Release();

        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        try
        {
            _memDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (_memDc == IntPtr.Zero) return false;

            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // negative = top-down rows, matching GDI+
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB
            };

            _dib = NativeMethods.CreateDIBSection(screenDc, ref header, NativeMethods.DIB_RGB_COLORS,
                out _bits, IntPtr.Zero, 0);
            if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
            {
                Release();
                return false;
            }

            _oldObject = NativeMethods.SelectObject(_memDc, _dib);
            try
            {
                _bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, _bits);
            }
            catch (ArgumentException)
            {
                Release();
                return false;
            }
            Width = width;
            Height = height;
            return true;
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public Graphics BeginGraphics()
    {
        if (_bitmap == null) throw new InvalidOperationException("Surface is not allocated.");
        return Graphics.FromImage(_bitmap);
    }

    /// <summary>Pushes the bitmap to the layered window. The window keeps its current position.</summary>
    public bool Present(IntPtr hwnd)
    {
        if (_bitmap == null || _memDc == IntPtr.Zero) return false;
        var size = new NativeMethods.SIZE(Width, Height);
        var srcPoint = new NativeMethods.POINT(0, 0);
        var blend = new NativeMethods.BLENDFUNCTION
        {
            BlendOp = NativeMethods.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = NativeMethods.AC_SRC_ALPHA
        };
        return NativeMethods.UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, _memDc,
            ref srcPoint, 0, ref blend, NativeMethods.ULW_ALPHA);
    }

    private void Release()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        if (_memDc != IntPtr.Zero && _oldObject != IntPtr.Zero)
            NativeMethods.SelectObject(_memDc, _oldObject);
        _oldObject = IntPtr.Zero;
        if (_dib != IntPtr.Zero) NativeMethods.DeleteObject(_dib);
        _dib = IntPtr.Zero;
        if (_memDc != IntPtr.Zero) NativeMethods.DeleteDC(_memDc);
        _memDc = IntPtr.Zero;
        _bits = IntPtr.Zero;
        Width = Height = 0;
    }

    public void Dispose() => Release();
}
