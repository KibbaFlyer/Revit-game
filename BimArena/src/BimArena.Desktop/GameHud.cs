using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BimArena.Core;

namespace BimArena.Desktop;

internal sealed class GameHud(GameSession game) : FrameworkElement
{
    private static readonly Brush Ink = Brush(0xEE11181E), Paper = Brush(0xFFF5EEDC), Muted = Brush(0xFFABB6B8);
    private static readonly Brush Orange = Brush(0xFFFFB66E), Green = Brush(0xFF88EAC8), Purple = Brush(0xFFD29AF6);
    public double FramesPerSecond { get; set; }
    public bool MutedAudio { get; set; }
    public bool Help { get; set; }

    protected override void OnRender(DrawingContext d)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 100 || h < 100) return;
        // Keep text and controls legible and non-overlapping in tiled/high-DPI viewports.
        double scale = Math.Min(1, Math.Min(w / 960, h / 600));
        d.PushTransform(new ScaleTransform(scale, scale));
        w /= scale; h /= scale;
        d.DrawRectangle(Brush(0xDB11181E), null, new Rect(0, 0, w, 68));
        Text(d, "BIM / ARENA", 24, 15, 24, Paper, true);
        Text(d, game.Scene.Name.Length > 55 ? game.Scene.Name[..52] + "…" : game.Scene.Name, 25, 45, 11, Muted);
        Text(d, $"WAVE {game.Wave:00}   /   {game.Enemies.Count:00} HOSTILES", Math.Max(260, w / 2 - 115), 23, 15, Purple, true);
        Text(d, $"{game.Score:000000} PTS", w - 174, 18, 20, Paper, true);
        Text(d, $"{FramesPerSecond:0} FPS   ·   {(MutedAudio ? "MUTED" : "AUDIO ON")}", w - 174, 44, 10, Muted);

        if (game.State == RunState.Playing)
        {
            DrawWeapon(d, w, h);
            var cross = new Pen(game.HitFlash > 0 ? Orange : Paper, 2);
            double x = w / 2, y = h / 2;
            d.DrawLine(cross, new(x - 13, y), new(x - 5, y)); d.DrawLine(cross, new(x + 5, y), new(x + 13, y));
            d.DrawLine(cross, new(x, y - 13), new(x, y - 5)); d.DrawLine(cross, new(x, y + 5), new(x, y + 13));
            if (game.HitFlash > 0)
                for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2)
                    d.DrawLine(new(Orange, 2), new(x + a * 17, y + b * 17), new(x + a * 22, y + b * 22));
            if (game.Enemies.Count == 0) Text(d, $"NEXT WAVE  /  {Math.Ceiling(game.NextWaveIn):0}", w / 2 - 95, 91, 16, Orange, true);
            if (game.ReloadRemaining > 0) Text(d, "RELOADING", w / 2 - 45, h / 2 + 42, 13, Orange);
        }

        d.DrawRectangle(Ink, null, new Rect(0, h - 92, w, 92));
        d.DrawRectangle(Orange, null, new Rect(0, h - 92, w, 2));
        Text(d, "HEALTH", 26, h - 78, 11, Muted, true);
        Text(d, game.Health.ToString("000"), 24, h - 60, 36, game.Health < 30 ? Orange : Green, true);
        d.DrawRectangle(Brush(0xFF2D3940), null, new Rect(113, h - 45, 100, 7));
        d.DrawRectangle(game.Health < 30 ? Orange : Green, null, new Rect(113, h - 45, game.Health, 7));
        Text(d, "BREACHER / 12 GA", w - 247, h - 78, 11, Muted, true);
        Text(d, $"{game.Shells:00}", w - 247, h - 60, 36, Orange, true);
        Text(d, $"/ {game.Reserve:00}", w - 175, h - 46, 21, Paper);
        Text(d, $"{game.Kills} KILLS", w - 91, h - 45, 12, Muted);
        if (w > 850)
        {
            Text(d, game.Message, 254, h - 71, 13, Paper);
            Text(d, "WASD move   SHIFT run   CLICK fire   R reload   P pause   ESC exit", 254, h - 39, 11, Muted);
        }
        if (game.DamageFlash > 0)
            d.DrawRectangle(null, new Pen(Brush((uint)((byte)(game.DamageFlash / .3 * 180) << 24) | 0x00ED4E42), 22), new Rect(11, 11, w - 22, h - 22));

        if (game.State != RunState.Playing || Help)
        {
            d.DrawRectangle(Brush(0xC00B1016), null, new Rect(0, 0, w, h));
            double cw = Math.Min(610, w - 48), ch = Math.Min(425, h - 36), left = (w - cw) / 2, top = (h - ch) / 2;
            d.DrawRoundedRectangle(Brush(0xFA151E27), new Pen(Brush(0xFF4F626A), 1), new Rect(left, top, cw, ch), 8, 8);
            d.DrawRectangle(Orange, null, new Rect(left + 28, top + 29, 32, 4));
            Text(d, "REVIT 2027  /  LIVE VIEW ARENA", left + 73, top + 21, 11, Muted, true);
            string title = game.State switch { RunState.Ready => "BREACH THE MODEL.", RunState.Defeated => "RUN ENDED.", _ => "TAKE A BREATHER." };
            Text(d, title, left + 28, top + 64, Math.Min(34, cw / 17), Paper, true);
            string subtitle = game.State == RunState.Defeated ? $"{game.Score:N0} points · {game.Kills} kills · wave {game.Wave}" : "Your building. Real geometry. Survive the waves.";
            Text(d, subtitle, left + 30, top + 112, 15, Purple);
            Text(d, "W / A / S / D       Move     ·     Mouse to look", left + 30, top + 164, 14, Paper);
            Text(d, "Left click         Fire     ·     R to reload", left + 30, top + 194, 14, Paper);
            Text(d, "Shift / Space      Run / Jump", left + 30, top + 224, 14, Paper);
            Text(d, "P  Pause     M  Mute     F1  Help     Esc  Exit", left + 30, top + 254, 13, Muted);
            d.DrawRoundedRectangle(Orange, null, new Rect(left + 28, top + ch - 100, cw - 56, 44), 4, 4);
            Text(d, game.State == RunState.Defeated ? "ENTER  /  REDEPLOY" : "ENTER OR CLICK  /  DEPLOY", left + 46, top + ch - 89, 15, Ink, true);
            Text(d, "Exit returns to your unchanged Revit view.", left + 30, top + ch - 37, 12, Muted);
        }
        d.Pop();
    }

    private void DrawWeapon(DrawingContext d, double w, double h)
    {
        double recoil = game.ShotFlash / .13;
        double reload = game.ReloadRemaining > 0 ? 75 * Math.Sin(Math.PI * game.ReloadRemaining / 1.35) : 0;
        double x = w * .59, y = h - 91 + recoil * 15 + reload;
        d.PushTransform(new TranslateTransform(x, y));
        Polygon(d, Brush(0xFF4F656F), new(-78, 0), new(-62, -125), new(-36, -175), new(38, -175), new(66, -119), new(88, 0));
        Polygon(d, Brush(0xFF1C2B34), new(-52, 0), new(-41, -150), new(-20, -202), new(22, -202), new(46, -150), new(57, 0));
        d.DrawRectangle(Brush(0xFF9AA8AA), null, new Rect(-18, -190, 37, 71));
        d.DrawRectangle(Brush(0xFF0B151C), null, new Rect(-12, -180, 25, 67));
        d.DrawRectangle(Brush(0xFFCE8559), null, new Rect(-40, -94, 80, 64));
        for (int i = 0; i < 5; i++) d.DrawRectangle(Brush(0xFF79513C), null, new Rect(-39, -88 + i * 11, 78, 3));
        d.DrawRectangle(Green, null, new Rect(-3, -202, 6, 11));
        if (recoil > .35)
            Polygon(d, Orange, new(-5, -213), new(-34, -224), new(-18, -249), new(-5, -234), new(7, -280), new(18, -235), new(37, -249), new(28, -218), new(8, -210));
        d.Pop();
    }
    private void Text(DrawingContext d, string text, double x, double y, double size, Brush brush, bool bold = false)
    {
        var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        d.DrawText(formatted, new Point(x, y));
    }
    private static Brush Brush(uint argb) { var brush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)); brush.Freeze(); return brush; }
    private static void Polygon(DrawingContext d, Brush brush, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open()) { c.BeginFigure(points[0], true, true); c.PolyLineTo(points.Skip(1).ToArray(), true, false); }
        geometry.Freeze(); d.DrawGeometry(brush, null, geometry);
    }
}
