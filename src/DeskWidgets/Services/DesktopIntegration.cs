using System.Drawing;

namespace DeskWidgets.Services;

/// <summary>
/// Finds the Windows desktop windows and places the dashboard relative to them.
/// Every call fails safely (returns false / IntPtr.Zero) so the caller can fall back to a normal window.
/// </summary>
internal static class DesktopIntegration
{
    /// <summary>The top-level window that hosts the desktop icons (Progman, or a WorkerW on some setups).</summary>
    public static IntPtr FindIconHostWindow()
    {
        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>The WorkerW window that sits behind the desktop icons, or IntPtr.Zero if it cannot be found.</summary>
    public static IntPtr FindWorkerW()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        // Ask Explorer to create the WorkerW window behind the icons (undocumented but widely used).
        NativeMethods.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1),
            NativeMethods.SMTO_NORMAL, 1000, out _);

        IntPtr workerW = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                IntPtr sibling = NativeMethods.FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                if (sibling != IntPtr.Zero) workerW = sibling;
            }
            return true;
        }, IntPtr.Zero);
        return workerW;
    }

    /// <summary>Makes the window a child of the given parent (the WorkerW window).</summary>
    public static bool AttachToParent(IntPtr hwnd, IntPtr parent)
    {
        if (hwnd == IntPtr.Zero || parent == IntPtr.Zero || !NativeMethods.IsWindow(parent)) return false;
        try
        {
            // Per the SetParent documentation: switch the styles first, then change the parent.
            uint oldStyle = unchecked((uint)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64());
            uint childStyle = (oldStyle & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(unchecked((int)childStyle)));
            NativeMethods.SetParent(hwnd, parent);

            // SetParent returns NULL both on failure and for a top-level window's old parent,
            // so the only reliable check is to ask who the parent is now.
            if (NativeMethods.GetParent(hwnd) != parent)
            {
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(unchecked((int)oldStyle)));
                return false;
            }

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns the window to being an ordinary top-level window.</summary>
    public static void DetachFromParent(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;
        try
        {
            NativeMethods.SetParent(hwnd, IntPtr.Zero);
            uint style = unchecked((uint)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64());
            style = (style & ~NativeMethods.WS_CHILD) | NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(unchecked((int)style)));
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        }
        catch
        {
            // Nothing more can be done; the caller keeps working with the window as it is.
        }
    }

    /// <summary>Window rectangle in screen coordinates (works for top-level and child windows).</summary>
    public static Rectangle GetScreenRect(IntPtr hwnd)
        => NativeMethods.GetWindowRect(hwnd, out var r) ? r.ToRectangle() : Rectangle.Empty;

    /// <summary>Moves and sizes a window using screen coordinates, converting for child windows.</summary>
    public static void SetScreenBounds(IntPtr hwnd, IntPtr parent, Rectangle screenRect)
    {
        int x = screenRect.X, y = screenRect.Y;
        if (parent != IntPtr.Zero)
        {
            var p = new NativeMethods.POINT(x, y);
            NativeMethods.ScreenToClient(parent, ref p);
            x = p.X;
            y = p.Y;
        }
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, screenRect.Width, screenRect.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Places the window directly ABOVE the desktop icon window and below normal application windows.
    /// (SetWindowPos places a window *below* its "insert after" window, so we insert after the window
    /// that currently sits just above the desktop.) Returns false if that is not possible, and the
    /// caller then falls back to an ordinary window.
    /// </summary>
    public static bool PlaceAboveDesktop(IntPtr hwnd)
    {
        IntPtr host = FindIconHostWindow();
        if (host == IntPtr.Zero) return false;

        IntPtr above = NativeMethods.GetWindow(host, NativeMethods.GW_HWNDPREV);
        if (above == hwnd) return true;                 // already directly above the desktop
        if (above == IntPtr.Zero) return false;         // desktop is the top window: cannot place relative to it

        long exStyle = NativeMethods.GetWindowLongPtr(above, NativeMethods.GWL_EXSTYLE).ToInt64();
        if ((exStyle & NativeMethods.WS_EX_TOPMOST) != 0) return false; // would make us topmost

        return NativeMethods.SetWindowPos(hwnd, above, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public static void PlaceNormal(IntPtr hwnd)
        => NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    public static void PlaceTopMost(IntPtr hwnd)
        => NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
}
