using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using ItimHebrewCalendar.Services;

namespace ItimHebrewCalendar.Windows
{
    // Renders the day glyph as a tray HICON in one of three styles:
    //   Tile     - flat solid rounded square, white bold text (Win11 tile look).
    //   TextOnly - transparent background, foreground-color regular text, like
    //              the built-in clock / Wi-Fi tray glyphs.
    //   Classic  - original calendar-page look: rounded body, accent header,
    //              binding holes; orange + setting sun after sunset.
    // Font size is locked to the width of the widest 2-letter day ("כט") and
    // the height of the tallest letter ("ל" - it has an ascender), so every
    // day renders at the same visual size. After sunset is signalled with
    // orange in every style.
    // Caller must release the HICON via DestroyIcon.
    public static class TrayIconRenderer
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static IntPtr RenderHIcon(
            string text,
            bool darkMode,
            bool afterSunset = false,
            TrayIconStyle style = TrayIconStyle.Tile,
            int size = 32)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

                switch (style)
                {
                    case TrayIconStyle.TextOnly:
                        RenderTextOnly(g, text, darkMode, afterSunset, size);
                        break;
                    case TrayIconStyle.Classic:
                        RenderClassic(g, text, darkMode, afterSunset, size);
                        break;
                    case TrayIconStyle.Tile:
                    default:
                        RenderTile(g, text, darkMode, afterSunset, size);
                        break;
                }
            }

            return bmp.GetHicon();
        }

        public static Icon Render(
            string text,
            bool darkMode,
            bool afterSunset = false,
            TrayIconStyle style = TrayIconStyle.Tile,
            int size = 32)
        {
            var hIcon = RenderHIcon(text, darkMode, afterSunset, style, size);
            return Icon.FromHandle(hIcon);
        }

        private static void RenderTile(Graphics g, string text, bool darkMode, bool afterSunset, int size)
        {
            var fillColor = afterSunset
                ? Color.FromArgb(255, 0xE0, 0x55, 0x10)
                : (darkMode
                    ? Color.FromArgb(255, 0x4C, 0xC2, 0xFF)
                    : Color.FromArgb(255, 0x00, 0x67, 0xC0));

            float padding = 0.5f;
            float cornerR = size * 0.22f;
            var frameRect = new RectangleF(padding, padding, size - padding * 2, size - padding * 2);

            using (var bodyPath = CreateRoundedRect(frameRect, cornerR))
            using (var bodyBrush = new SolidBrush(fillColor))
                g.FillPath(bodyBrush, bodyPath);

            using (var borderPath = CreateRoundedRect(frameRect, cornerR))
            using (var borderPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                g.DrawPath(borderPen, borderPath);

            // 2px inset keeps the glyph clear of the rounded corners.
            var textArea = new RectangleF(2f, 2f, size - 4f, size - 4f);
            DrawGlyphUniform(g, text, FontStyle.Bold, Color.White, textArea);
        }

        private static void RenderTextOnly(Graphics g, string text, bool darkMode, bool afterSunset, int size)
        {
            // Match Windows tray foreground: white in dark mode, near-black in
            // light mode. Regular weight matches the native clock / Wi-Fi
            // labels. Sunset uses a theme-tuned orange instead.
            var textColor = afterSunset
                ? (darkMode
                    ? Color.FromArgb(255, 0xFF, 0xB0, 0x60)
                    : Color.FromArgb(255, 0xC0, 0x45, 0x00))
                : (darkMode
                    ? Color.White
                    : Color.FromArgb(255, 0x20, 0x20, 0x20));

            var textArea = new RectangleF(0.5f, 0.5f, size - 1f, size - 1f);
            DrawGlyphUniform(g, text, FontStyle.Regular, textColor, textArea);
        }

        private static void RenderClassic(Graphics g, string text, bool darkMode, bool afterSunset, int size)
        {
            var bodyColor = darkMode
                ? Color.FromArgb(255, 0x2B, 0x2B, 0x2B)
                : Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
            var headerColor = afterSunset
                ? Color.FromArgb(255, 0xE0, 0x55, 0x10)
                : (darkMode
                    ? Color.FromArgb(255, 0x4C, 0xC2, 0xFF)
                    : Color.FromArgb(255, 0x00, 0x67, 0xC0));
            var borderColor = darkMode
                ? Color.FromArgb(200, 255, 255, 255)
                : Color.FromArgb(140, 0, 0, 0);
            var textColor = darkMode
                ? Color.White
                : Color.FromArgb(255, 0x20, 0x20, 0x20);

            float padding = 1f;
            float cornerR = MathF.Max(2f, size * 0.18f);
            var frameRect = new RectangleF(padding, padding, size - padding * 2, size - padding * 2);

            using (var bodyPath = CreateRoundedRect(frameRect, cornerR))
            using (var bodyBrush = new SolidBrush(bodyColor))
                g.FillPath(bodyBrush, bodyPath);

            float headerHeight = size * 0.22f;
            var headerRect = new RectangleF(padding, padding, size - padding * 2, headerHeight);
            using (var headerPath = CreateRoundedRectTop(headerRect, cornerR))
            using (var headerBrush = new SolidBrush(headerColor))
                g.FillPath(headerBrush, headerPath);

            if (afterSunset)
            {
                DrawSettingSunInHeader(g, size, padding, headerHeight);
            }
            else
            {
                float ringR = size * 0.035f;
                float ringY = padding + headerHeight * 0.45f;
                float ringX1 = size * 0.30f;
                float ringX2 = size * 0.70f;
                using var ringBrush = new SolidBrush(bodyColor);
                g.FillEllipse(ringBrush, ringX1 - ringR, ringY - ringR, ringR * 2, ringR * 2);
                g.FillEllipse(ringBrush, ringX2 - ringR, ringY - ringR, ringR * 2, ringR * 2);
            }

            using (var borderPath = CreateRoundedRect(frameRect, cornerR))
            using (var borderPen = new Pen(borderColor, 1f))
                g.DrawPath(borderPen, borderPath);

            float textTop = padding + headerHeight;
            float textHeight = size - padding - textTop;
            var textArea = new RectangleF(padding + 1f, textTop, size - padding * 2 - 2f, textHeight);
            DrawGlyphUniform(g, text, FontStyle.Bold, textColor, textArea);
        }

        // Draws `text` at a fixed font size: the largest size at which the
        // widest 2-letter day ("כט") still fits the area's width and the
        // tallest letter ("ל", with its ascender) still fits the area's
        // height. That keeps the glyph size identical day-to-day - a narrow
        // single letter like "י" does not balloon to fill the icon, and a
        // wide pair never overflows - and every letter fits inside the icon.
        private static void DrawGlyphUniform(
            Graphics g, string text, FontStyle fontStyle, Color color, RectangleF area)
        {
            if (string.IsNullOrEmpty(text) || area.Width <= 0 || area.Height <= 0) return;

            using var family = new FontFamily("Segoe UI");
            using var sf = new StringFormat { FormatFlags = StringFormatFlags.DirectionRightToLeft };

            const float refSize = 100f;
            float widthAtRef = MeasurePathBounds("כט", family, fontStyle, refSize, sf).Width;
            float heightAtRef = MeasurePathBounds("ל", family, fontStyle, refSize, sf).Height;
            if (widthAtRef <= 0 || heightAtRef <= 0) return;

            float scale = Math.Min(area.Width / widthAtRef, area.Height / heightAtRef);
            float finalSize = refSize * scale;

            using var path = new GraphicsPath();
            path.AddString(text, family, (int)fontStyle, finalSize, PointF.Empty, sf);
            var b = path.GetBounds();
            if (b.Width <= 0 || b.Height <= 0) return;

            using var matrix = new Matrix();
            matrix.Translate(
                area.Left + (area.Width - b.Width) / 2f - b.X,
                area.Top + (area.Height - b.Height) / 2f - b.Y);
            path.Transform(matrix);

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        private static RectangleF MeasurePathBounds(
            string text, FontFamily family, FontStyle fontStyle, float emSize, StringFormat sf)
        {
            using var p = new GraphicsPath();
            p.AddString(text, family, (int)fontStyle, emSize, PointF.Empty, sf);
            return p.GetBounds();
        }

        // Centers the sun on the header-body boundary; the lower half is hidden by
        // clipping so the visible top half looks like a sun setting at the horizon.
        private static void DrawSettingSunInHeader(Graphics g, int size, float padding, float headerHeight)
        {
            float horizonY = padding + headerHeight;
            float sunDiam = headerHeight * 1.7f;
            float sunX = (size - sunDiam) / 2;
            float sunY = horizonY - sunDiam / 2;

            var prevClip = g.Clip;
            g.SetClip(new RectangleF(padding, padding, size - padding * 2, headerHeight));

            var sunColor = Color.FromArgb(255, 0xFF, 0xE0, 0x80);
            using (var sunBrush = new SolidBrush(sunColor))
                g.FillEllipse(sunBrush, sunX, sunY, sunDiam, sunDiam);

            g.Clip = prevClip;
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundedRectTop(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + radius);
            path.CloseFigure();
            return path;
        }
    }
}
