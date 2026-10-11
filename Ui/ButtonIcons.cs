using System.Drawing.Drawing2D;

namespace ModFrame.Ui;

internal enum ButtonIcon
{
    Back,
    Home,
    Refresh,
    NewFolder,
    UploadFile,
    UploadFolder,
    Unzip,
    LaunchOptions,
    Download,
    Rename,
    Delete
}

internal static class ButtonIcons
{
    public static Bitmap Create(ButtonIcon icon, Color color)
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(color, 1.65F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (icon)
        {
            case ButtonIcon.Back:
                graphics.DrawLines(pen, [new PointF(8, 4), new PointF(3, 9), new PointF(8, 14)]);
                graphics.DrawLine(pen, 3, 9, 15, 9);
                break;

            case ButtonIcon.Home:
                graphics.DrawLines(pen, [new PointF(2.5F, 8), new PointF(9, 2.5F), new PointF(15.5F, 8)]);
                graphics.DrawLines(pen, [new PointF(4.5F, 7), new PointF(4.5F, 15), new PointF(13.5F, 15), new PointF(13.5F, 7)]);
                graphics.DrawRectangle(pen, 7.5F, 10.5F, 3F, 4.5F);
                break;

            case ButtonIcon.Refresh:
                graphics.DrawArc(pen, 3, 3, 12, 12, 35, 285);
                graphics.DrawLines(pen, [new PointF(12.5F, 2.5F), new PointF(15.5F, 4.5F), new PointF(13.5F, 7)]);
                break;

            case ButtonIcon.NewFolder:
                DrawFolder(graphics, pen);
                graphics.DrawLine(pen, 11, 10.5F, 16, 10.5F);
                graphics.DrawLine(pen, 13.5F, 8, 13.5F, 13);
                break;

            case ButtonIcon.UploadFile:
                graphics.DrawLines(pen, [new PointF(4, 2.5F), new PointF(11, 2.5F), new PointF(14, 5.5F), new PointF(14, 15.5F), new PointF(4, 15.5F), new PointF(4, 2.5F)]);
                graphics.DrawLines(pen, [new PointF(11, 2.5F), new PointF(11, 5.5F), new PointF(14, 5.5F)]);
                DrawUpArrow(graphics, pen, 9, 13);
                break;

            case ButtonIcon.UploadFolder:
                DrawFolder(graphics, pen);
                DrawUpArrow(graphics, pen, 10.5F, 14);
                break;

            case ButtonIcon.Unzip:
                graphics.DrawRectangle(pen, 3, 2.5F, 12, 13F);
                graphics.DrawLine(pen, 9, 2.5F, 9, 6);
                graphics.DrawLine(pen, 7.5F, 4, 10.5F, 4);
                graphics.DrawLine(pen, 7.5F, 6, 10.5F, 6);
                graphics.DrawLines(pen, [new PointF(9, 8), new PointF(9, 13), new PointF(6.5F, 10.5F)]);
                graphics.DrawLine(pen, 9, 13, 11.5F, 10.5F);
                break;

            case ButtonIcon.LaunchOptions:
                graphics.DrawLine(pen, 3, 5, 15, 5);
                graphics.DrawLine(pen, 3, 9, 15, 9);
                graphics.DrawLine(pen, 3, 13, 15, 13);
                graphics.DrawEllipse(pen, 5, 3, 4, 4);
                graphics.DrawEllipse(pen, 10, 7, 4, 4);
                graphics.DrawEllipse(pen, 4, 11, 4, 4);
                break;

            case ButtonIcon.Download:
                graphics.DrawLine(pen, 9, 2.5F, 9, 11.5F);
                graphics.DrawLines(pen, [new PointF(5.5F, 8.5F), new PointF(9, 12), new PointF(12.5F, 8.5F)]);
                graphics.DrawLines(pen, [new PointF(3, 13), new PointF(3, 15.5F), new PointF(15, 15.5F), new PointF(15, 13)]);
                break;

            case ButtonIcon.Rename:
                graphics.DrawLine(pen, 3, 14.5F, 6, 14);
                graphics.DrawLines(pen, [new PointF(4, 13.5F), new PointF(12.5F, 5), new PointF(15, 7.5F), new PointF(6.5F, 16)]);
                graphics.DrawLine(pen, 11.5F, 6, 14, 8.5F);
                break;

            case ButtonIcon.Delete:
                graphics.DrawLines(pen, [new PointF(5, 6), new PointF(5.8F, 15), new PointF(12.2F, 15), new PointF(13, 6)]);
                graphics.DrawLine(pen, 3.5F, 5, 14.5F, 5);
                graphics.DrawLine(pen, 7, 2.8F, 11, 2.8F);
                graphics.DrawLine(pen, 8, 8, 8, 12.5F);
                graphics.DrawLine(pen, 10.5F, 8, 10.5F, 12.5F);
                break;
        }

        return bitmap;
    }

    private static void DrawFolder(Graphics graphics, Pen pen)
    {
        graphics.DrawLines(pen,
        [
            new PointF(2.5F, 5),
            new PointF(7, 5),
            new PointF(8.5F, 7),
            new PointF(15.5F, 7),
            new PointF(15.5F, 15),
            new PointF(2.5F, 15),
            new PointF(2.5F, 5)
        ]);
    }

    private static void DrawUpArrow(Graphics graphics, Pen pen, float centerX, float bottomY)
    {
        graphics.DrawLine(pen, centerX, bottomY, centerX, bottomY - 6);
        graphics.DrawLines(pen,
        [
            new PointF(centerX - 2.5F, bottomY - 3.5F),
            new PointF(centerX, bottomY - 6),
            new PointF(centerX + 2.5F, bottomY - 3.5F)
        ]);
    }
}
