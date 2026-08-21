using System.Diagnostics.CodeAnalysis;
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
    private static Palette _colors = Palette.Light;

    /// GDI ranglari BGR tartibida (0x00BBGGRR), shuning uchun qiymatlar CSS'dagidan teskari yozilgan.
    private readonly record struct Palette(
        uint Background, uint Title, uint Subtitle, uint Monogram, uint Accent, uint Track)
    {
        public static readonly Palette Light = new(0xFFFFFF, 0x1F2423, 0x88908A, 0x1F2423, 0x385E27, 0xECF0ED);
        public static readonly Palette Dark = new(0x241D1A, 0xF9F5F1, 0xB8A394, 0xF9F5F1, 0x578A39, 0x39312D);
    }

    /// Splash Avalonia'dan oldin chiziladi, shuning uchun mavzuni sozlama faylidan o'zi o'qiydi —
    /// aks holda qorong'i mavzuda oq oyna chaqnab, keyin qorong'i ilova ochilardi.
    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Oyna Win32 xabar sikligacha sinxron chiziladi; kichik sozlama fayli.")]
    private static Palette ReadPalette()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cartex", "settings.json");
            if (!File.Exists(path)) return Palette.Light;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("Theme", out var theme)
                   && theme.ValueKind == System.Text.Json.JsonValueKind.Number
                   && theme.GetInt32() == 1
                ? Palette.Dark
                : Palette.Light;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Text.Json.JsonException)
        {
            return Palette.Light;
        }
    }

    public static void Show()
    {
        if (!OperatingSystem.IsWindows()) return;
        _colors = ReadPalette();
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
        var background = CreateSolidBrush(_colors.Background);
        var full = new RECT { Left = 0, Top = 0, Right = Px(BaseW), Bottom = Px(BaseH) };
        FillRect(hdc, ref full, background);
        DeleteObject(background);
        SetBkMode(hdc, 1 /*TRANSPARENT*/);

        DrawMonogram(hdc);
        DrawCentered(hdc, "Cartex", 30, 700, _colors.Title, 126);
        DrawCentered(hdc, "Savdo boshqaruvi tizimi", 13, 400, _colors.Subtitle, 170);
        DrawBar(hdc);

        EndPaint(hwnd, ref ps);
    }

    // Splash logotipi ilovaning o'z logotipi bilan bir xil bo'lishi uchun shrift harflari emas,
    // `Controls/CxLogo.axaml` dagi aynan o'sha vektor konturlari chiziladi.
    private const string LogoViewBoxPathC = "M245 24L267 24L299 28L316 32L351 45L389 68L407 83L427 104L426 107L367 172L355 158L333 138L317 128L293 118L275 114L246 113L210 121L183 135L170 145L153 162L137 185L128 204L122 223L118 249L118 271L123 301L132 325L144 346L155 360L179 382L201 395L232 405L246 407L282 405L300 400L325 388L344 374L366 350L429 413L423 422L401 443L365 468L342 479L317 488L271 496L224 494L184 484L145 466L129 456L107 438L89 420L74 401L64 386L50 359L37 321L30 280L31 232L41 185L62 138L87 103L120 71L158 47L193 33L213 28Z";
    private const string LogoViewBoxPathX = "M602 51L718 52L717 56L681 102L555 260L556 265L709 455L718 467L717 470L596 469L495 338L492 338L488 342L458 380L448 391L446 391L384 329L384 325L388 319L433 263L428 253L385 199L384 196L388 189L443 128L446 128L449 131L488 180L494 185Z";
    private const double LogoViewBoxW = 751;
    private const double LogoViewBoxH = 529;

    private static void DrawMonogram(IntPtr hdc)
    {
        var height = Px(84);
        var scale = height / LogoViewBoxH;
        var left = (Px(BaseW) - (int)Math.Round(LogoViewBoxW * scale)) / 2;
        var top = Px(30);

        SetPolyFillMode(hdc, 2 /*WINDING*/);
        FillPolygon(hdc, LogoViewBoxPathC, scale, left, top, _colors.Monogram, 0);
        // Vektorda "x" konturi 9 birlik chiziq bilan qalinlashtirilgan — u ham ko'chiriladi.
        FillPolygon(hdc, LogoViewBoxPathX, scale, left, top, _colors.Accent, (int)Math.Round(9 * scale));
    }

    private static void FillPolygon(IntPtr hdc, string path, double scale, int dx, int dy, uint color, int outline)
    {
        var points = ParsePath(path, scale, dx, dy);
        if (points.Length < 3) return;

        var brush = CreateSolidBrush(color);
        var pen = outline > 0 ? CreatePen(0 /*PS_SOLID*/, outline, color) : GetStockObject(8 /*NULL_PEN*/);
        var oldBrush = SelectObject(hdc, brush);
        var oldPen = SelectObject(hdc, pen);
        Polygon(hdc, points, points.Length);
        SelectObject(hdc, oldBrush);
        SelectObject(hdc, oldPen);
        DeleteObject(brush);
        if (outline > 0) DeleteObject(pen);
    }

    private static POINT[] ParsePath(string path, double scale, int dx, int dy)
    {
        var tokens = path.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries);
        var points = new POINT[tokens.Length];
        var count = 0;
        foreach (var token in tokens)
        {
            var space = token.IndexOf(' ');
            if (space <= 0) continue;
            points[count++] = new POINT
            {
                X = dx + (int)Math.Round(int.Parse(token[..space]) * scale),
                Y = dy + (int)Math.Round(int.Parse(token[(space + 1)..]) * scale)
            };
        }
        return count == points.Length ? points : points[..count];
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
        var trackBrush = CreateSolidBrush(_colors.Track);
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
        var brush = CreateSolidBrush(_colors.Accent);
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
    [DllImport("gdi32.dll")] private static extern bool Polygon(IntPtr hdc, POINT[] points, int count);
    [DllImport("gdi32.dll")] private static extern int SetPolyFillMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
}
