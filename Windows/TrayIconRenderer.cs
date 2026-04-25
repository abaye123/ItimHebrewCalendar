using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ItimHebrewCalendar.Windows
{
    // Renders a calendar-page-style HICON: rounded frame, colored header strip,
    // and the day text centered below. After sunset the header turns orange and
    // shows a half-set sun; the text area is left untouched.
    // Caller must release the HICON via DestroyIcon.
    public static class TrayIconRenderer
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static IntPtr RenderHIcon(string text, bool darkMode, bool afterSunset = false, int size = 32)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

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
                    : Color.FromArgb(0x20, 0x20, 0x20);

                float padding = 1f;
                float cornerR = MathF.Max(2f, size * 0.18f);
                var frameRect = new RectangleF(padding, padding, size - padding * 2, size - padding * 2);

                using (var bodyPath = CreateRoundedRect(frameRect, cornerR))
                using (var bodyBrush = new SolidBrush(bodyColor))
                {
                    g.FillPath(bodyBrush, bodyPath);
                }

                float headerHeight = size * 0.22f;
                var headerRect = new RectangleF(padding, padding, size - padding * 2, headerHeight);
                using (var headerPath = CreateRoundedRectTop(headerRect, cornerR))
                using (var headerBrush = new SolidBrush(headerColor))
                {
                    g.FillPath(headerBrush, headerPath);
                }

                if (afterSunset)
                {
                    DrawSettingSunInHeader(g, size, padding, headerHeight);
                }
                else
                {
                    // Two binding holes mimicking a wall calendar page.
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
                {
                    g.DrawPath(borderPen, borderPath);
                }

                var len = text.Length;
                float textAreaTop = padding + headerHeight;
                float textAreaHeight = size - padding - textAreaTop;

                float fontSize = len switch
                {
                    <= 1 => textAreaHeight * 1.15f,
                    2 => textAreaHeight * 1.00f,
                    3 => textAreaHeight * 0.72f,
                    _ => textAreaHeight * 0.58f
                };

                using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                using var textBrush = new SolidBrush(textColor);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoClip
                };
                var textRect = new RectangleF(0, textAreaTop, size, textAreaHeight);
                g.DrawString(text, font, textBrush, textRect, sf);
            }

            return bmp.GetHicon();
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

        public static Icon Render(string text, bool darkMode, bool afterSunset = false, int size = 32)
        {
            var hIcon = RenderHIcon(text, darkMode, afterSunset, size);
            return Icon.FromHandle(hIcon);
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
