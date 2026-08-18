using System.Runtime.InteropServices;

namespace Cartex.Desktop;

/// Avalonia va .NET runtime yuklanguncha bir necha soniya hech narsa ko'rinmaydi va
/// foydalanuvchi tugmani qayta-qayta bosadi. Bu oyna sof Win32 bo'lgani uchun jarayon
/// boshlanishi bilanoq chiziladi; Avalonia'ning o'z splash oynasi chiqqach yopiladi.
internal static class NativeSplash
{
    private const int BaseW = 440;
    private const int BaseH = 270;

    private static IntPtr _hwnd;
    private static volatile bool _closed;
    private static WndProcDelegate? _wndProc;
    private static double _s = 1;
    private static int _marquee;

    public static void Show()
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        var thread = new Thread(Run) { IsBackground = true, Name = "NativeSplash" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void Close()
    {
        _closed = true;
        var hwnd = Interlocked.Exchange(ref _hwnd, IntPtr.Zero);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);
    }

    private static void Run()
    {
        _s = GetDpiForSystem() / 96.0;
        var w = Px(BaseW);
        var h = Px(BaseH);
        _wndProc = WndProc;

        var cls = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(32512) /*IDC_ARROW*/),
            lpszClassName = "CartexSplash",
        };
        if (RegisterClassExW(ref cls) == 0) return;

        var x = (GetSystemMetrics(0 /*SM_CXSCREEN*/) - w) / 2;
        var y = (GetSystemMetrics(1 /*SM_CYSCREEN*/) - h) / 2;
        var hwnd = CreateWindowExW(0x0080 /*WS_EX_TOOLWINDOW*/, cls.lpszClassName, "Cartex",
            unchecked((uint)0x80000000) /*WS_POPUP*/, x, y, w, h, IntPtr.Zero, IntPtr.Zero, cls.hInstance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) return;

        SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w + 1, h + 1, Px(32), Px(32)), true);
        Interlocked.Exchange(ref _hwnd, hwnd);
        if (_closed)
        {
            Interlocked.Exchange(ref _hwnd, IntPtr.Zero);
            DestroyWindow(hwnd);
            return;
        }
        ShowWindow(hwnd, 5 /*SW_SHOW*/);
        SetTimer(hwnd, 1, 30, IntPtr.Zero);

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private static int Px(double logical) => (int)Math.Round(logical * _s);

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case 0x000F: // WM_PAINT
                Paint(hwnd);
                return IntPtr.Zero;
            case 0x0113: // WM_TIMER
                _marquee = (_marquee + Px(4)) % (Px(BaseW - 60) + Px(90));
                var bar = new RECT { Left = Px(30), Top = Px(224), Right = Px(BaseW - 30), Bottom = Px(229) };
                InvalidateRect(hwnd, ref bar, false);
                return IntPtr.Zero;
            case 0x0002: // WM_DESTROY
                KillTimer(hwnd, 1);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static void Paint(IntPtr hwnd)
    {
        var hdc = BeginPaint(hwnd, out var ps);
        var white = CreateSolidBrush(0xFFFFFF);
        var full = new RECT { Left = 0, Top = 0, Right = Px(BaseW), Bottom = Px(BaseH) };
        FillRect(hdc, ref full, white);
        DeleteObject(white);
        SetBkMode(hdc, 1 /*TRANSPARENT*/);

        DrawMonogram(hdc);
        DrawCentered(hdc, "Cartex", 30, 700, 0x1F2423, 126);
        DrawCentered(hdc, "Savdo boshqaruvi tizimi", 13, 400, 0x88908A, 170);
        DrawBar(hdc);

        EndPaint(hwnd, ref ps);
    }

    private static void DrawMonogram(IntPtr hdc)
    {
        var font = CreateFontW(-Px(66), 0, 0, 0, 900, 0, 0, 0, 0, 0, 0, 5 /*CLEARTYPE*/, 0, "Segoe UI");
        var old = SelectObject(hdc, font);
        GetTextExtentPoint32W(hdc, "C", 1, out var sizeC);
        GetTextExtentPoint32W(hdc, "x", 1, out var sizeX);
        var kern = Px(8);
        var x = (Px(BaseW) - (sizeC.cx + sizeX.cx - kern)) / 2;
        var y = Px(34);
        SetTextColor(hdc, 0x1F2423);
        TextOutW(hdc, x, y, "C", 1);
        SetTextColor(hdc, 0x385E27);
        TextOutW(hdc, x + sizeC.cx - kern, y + (sizeC.cy - sizeX.cy), "x", 1);
        SelectObject(hdc, old);
        DeleteObject(font);
    }

    private static void DrawCentered(IntPtr hdc, string text, int size, int weight, uint color, int top)
    {
        var font = CreateFontW(-Px(size), 0, 0, 0, weight, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        var old = SelectObject(hdc, font);
        GetTextExtentPoint32W(hdc, text, text.Length, out var extent);
        SetTextColor(hdc, color);
        TextOutW(hdc, (Px(BaseW) - extent.cx) / 2, Px(top), text, text.Length);
        SelectObject(hdc, old);
        DeleteObject(font);
    }

    private static void DrawBar(IntPtr hdc)
    {
        var track = new RECT { Left = Px(30), Top = Px(224), Right = Px(BaseW - 30), Bottom = Px(228) };
        var trackBrush = CreateSolidBrush(0xECF0ED);
        FillRect(hdc, ref track, trackBrush);
        DeleteObject(trackBrush);

        var head = track.Left + _marquee;
        var segment = new RECT
        {
            Left = Math.Max(track.Left, head - Px(90)),
            Top = track.Top,
            Right = Math.Min(track.Right, head),
            Bottom = track.Bottom,
        };
        if (segment.Right <= segment.Left) return;
        var brush = CreateSolidBrush(0x385E27);
        FillRect(hdc, ref segment, brush);
        DeleteObject(brush);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmdShow);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr cursorName);
    [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint elapse, IntPtr proc);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hwnd, IntPtr id);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hwnd, ref RECT rect, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT paint);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? moduleName);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool GetTextExtentPoint32W(IntPtr hdc, string text, int length, out SIZE size);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool TextOutW(IntPtr hdc, int x, int y, string text, int length);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
}
