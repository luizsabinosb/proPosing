using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Views.Controls;

public sealed class SkeletonOverlayControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<LandmarkPoint>> LandmarksProperty =
        AvaloniaProperty.Register<SkeletonOverlayControl, IReadOnlyList<LandmarkPoint>>(
            nameof(Landmarks),
            []);

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<SkeletonOverlayControl, string>(
            nameof(Status),
            "no_detection");

    public static readonly StyledProperty<int?> ImageWidthProperty =
        AvaloniaProperty.Register<SkeletonOverlayControl, int?>(
            nameof(ImageWidth));

    public static readonly StyledProperty<int?> ImageHeightProperty =
        AvaloniaProperty.Register<SkeletonOverlayControl, int?>(
            nameof(ImageHeight));

    private static readonly (int A, int B)[] Connections =
    [
        (11, 12), (11, 23), (12, 24), (23, 24),
        (11, 13), (13, 15), (12, 14), (14, 16),
        (23, 25), (25, 27), (24, 26), (26, 28)
    ];

    public IReadOnlyList<LandmarkPoint> Landmarks
    {
        get => GetValue(LandmarksProperty);
        set => SetValue(LandmarksProperty, value);
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public int? ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public int? ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LandmarksProperty ||
            change.Property == StatusProperty ||
            change.Property == ImageWidthProperty ||
            change.Property == ImageHeightProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Landmarks.Count == 0)
        {
            return;
        }

        var color = Status switch
        {
            "correct" => Color.Parse("#10B981"),
            "incorrect" => Color.Parse("#EF4444"),
            "adjustment_needed" => Color.Parse("#F59E0B"),
            _ => Color.Parse("#EF4444"),
        };

        var contentRect = GetContentRect(Bounds.Size);
        var points = Landmarks.Select(l =>
        {
            if (l.Visibility is not null && l.Visibility <= 0.5)
            {
                return (Point?)null;
            }

            var x = Math.Clamp(l.X, 0.0, 1.0);
            var y = Math.Clamp(l.Y, 0.0, 1.0);
            return new Point(
                contentRect.Left + (x * contentRect.Width),
                contentRect.Top + (y * contentRect.Height));
        }).ToList();

        var linePen = new Pen(new SolidColorBrush(color), 3, lineCap: PenLineCap.Round);
        foreach (var (a, b) in Connections)
        {
            if (a < points.Count && b < points.Count && points[a] is Point pa && points[b] is Point pb)
            {
                context.DrawLine(linePen, pa, pb);
            }
        }

        var glowBrush = new SolidColorBrush(color, 0.30);
        var pointBrush = new SolidColorBrush(color);
        var whiteBrush = Brushes.White;
        foreach (var idx in new[] { 11, 12, 13, 14, 15, 16, 23, 24, 25, 26 })
        {
            if (idx < points.Count && points[idx] is Point p)
            {
                context.DrawEllipse(glowBrush, null, p, 8, 8);
                context.DrawEllipse(pointBrush, null, p, 6, 6);
                context.DrawEllipse(whiteBrush, null, p, 3, 3);
            }
        }
    }

    private Rect GetContentRect(Size size)
    {
        if (ImageWidth is null || ImageHeight is null || ImageWidth <= 0 || ImageHeight <= 0)
        {
            return new Rect(0, 0, size.Width, size.Height);
        }

        var imageAspect = (double)ImageWidth.Value / ImageHeight.Value;
        var containerAspect = size.Width / size.Height;

        // Matches Image Stretch=Uniform: scale so the whole image fits inside the
        // container (letterboxed on the less-constrained axis, centered).
        if (imageAspect > containerAspect)
        {
            var h = size.Width / imageAspect;
            return new Rect(0, (size.Height - h) / 2, size.Width, h);
        }

        var w = size.Height * imageAspect;
        return new Rect((size.Width - w) / 2, 0, w, size.Height);
    }
}
