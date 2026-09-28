using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BimArena.Core;

namespace BimArena.Desktop;

/// <summary>Modal, owned viewport overlay. No Revit objects or API calls enter the game loop.</summary>
public sealed class GameWindow : Window
{
    private readonly GameSession game;
    private readonly SceneRenderer renderer;
    private readonly GameHud hud;
    private readonly GameAudio audio = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly HashSet<Key> keys = [];
    private readonly nint owner;
    private readonly PixelRect? viewport;
    private Native.Rect ownerAtStart;
    private Native.Point savedCursor;
    private nint handle;
    private bool firing, captured, subscribed;
    private double lastFrame, fpsElapsed;
    private int frames;
    public Exception? Failure { get; private set; }

    public GameWindow(GameSession session, nint ownerHandle = 0, PixelRect? viewportRectangle = null)
    {
        game = session; owner = ownerHandle; viewport = viewportRectangle;
        Title = "BIM Arena"; Width = 1280; Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = owner == 0 ? WindowStyle.SingleBorderWindow : WindowStyle.None;
        ResizeMode = owner == 0 ? ResizeMode.CanResize : ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(20, 28, 38));
        ShowInTaskbar = owner == 0; UseLayoutRounding = true;
        if (owner != 0) new WindowInteropHelper(this).Owner = owner;
        renderer = new(session.Scene); hud = new(session) { IsHitTestVisible = false };
        var root = new Grid { ClipToBounds = true, Focusable = true };
        root.Children.Add(renderer.View); root.Children.Add(hud); Content = root;
        game.Shot += audio.Shot;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            Native.GetCursorPos(out savedCursor);
            if (owner != 0) Native.GetWindowRect(owner, out ownerAtStart);
            PositionOverlay();
        };
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnFrame; subscribed = true;
            lastFrame = clock.Elapsed.TotalSeconds; root.Focus(); renderer.Update(game);
        };
        PreviewKeyDown += OnKeyDown;
        PreviewKeyUp += (_, e) => { keys.Remove(e.Key); e.Handled = true; };
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (game.State is RunState.Ready or RunState.Paused) Resume();
            else if (game.State == RunState.Playing) firing = true;
            e.Handled = true;
        };
        PreviewMouseUp += (_, _) => firing = false;
        Deactivated += (_, _) => Pause();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0) { Close(); return; }
        keys.Add(key);
        if (!e.IsRepeat)
        {
            switch (key)
            {
                case Key.Escape: Close(); break;
                case Key.P: if (game.State == RunState.Playing) Pause(); else Resume(); break;
                case Key.Enter:
                    if (game.State == RunState.Defeated) game.Restart();
                    Resume(); break;
                case Key.M: audio.Muted = !audio.Muted; break;
                case Key.F1: hud.Help = !hud.Help; if (hud.Help) Pause(); else Resume(); break;
            }
        }
        e.Handled = true;
    }

    private void Resume()
    {
        hud.Help = false; keys.Clear(); firing = false;
        game.Start(); lastFrame = clock.Elapsed.TotalSeconds;
        if (game.State == RunState.Playing) CaptureCursor();
    }
    private void Pause()
    {
        game.Pause(); keys.Clear(); firing = false; ReleaseCursor();
    }

    private void OnFrame(object? sender, EventArgs args)
    {
        try
        {
            double now = clock.Elapsed.TotalSeconds, elapsed = now - lastFrame; lastFrame = now;
            PositionOverlay();
            if (game.State == RunState.Playing && IsActive)
            {
                if (!captured) CaptureCursor();
                ReadMouse();
                var input = new PlayerInput((Down(Key.W) ? 1 : 0) - (Down(Key.S) ? 1 : 0), (Down(Key.D) ? 1 : 0) - (Down(Key.A) ? 1 : 0),
                    Down(Key.LeftShift) || Down(Key.RightShift), Down(Key.Space), firing, Down(Key.R));
                game.Advance(elapsed, input);
                if (game.State == RunState.Defeated) ReleaseCursor();
            }
            fpsElapsed += elapsed; frames++;
            if (fpsElapsed >= .5) { hud.FramesPerSecond = frames / fpsElapsed; fpsElapsed = 0; frames = 0; }
            hud.MutedAudio = audio.Muted; renderer.Update(game); hud.InvalidateVisual();
        }
        catch (Exception ex) { Failure = ex; ReleaseCursor(); Close(); }
    }
    private bool Down(Key key) => keys.Contains(key);

    private void PositionOverlay()
    {
        if (handle == 0 || owner == 0 || viewport is not { } view) return;
        if (Native.IsIconic(owner)) { Pause(); return; }
        if (!Native.GetWindowRect(owner, out var current)) return;
        int left = current.Left + view.Left - ownerAtStart.Left, top = current.Top + view.Top - ownerAtStart.Top;
        int right = current.Right - (ownerAtStart.Right - view.Right), bottom = current.Bottom - (ownerAtStart.Bottom - view.Bottom);
        if (right <= left || bottom <= top) { Pause(); return; }
        if (Native.GetWindowRect(handle, out var actual) && actual.Left == left && actual.Top == top && actual.Right == right && actual.Bottom == bottom) return;
        // Physical screen coordinates from UIView; SetWindowPos avoids DIP/DPI conversion errors.
        Native.SetWindowPos(handle, 0, left, top, right - left, bottom - top, 0x0014); // NOZORDER | NOACTIVATE
    }

    private (int X, int Y) Center()
    {
        var point = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
        return ((int)point.X, (int)point.Y);
    }
    private void CaptureCursor()
    {
        if (!IsActive || captured) return;
        captured = true; Cursor = Cursors.None;
        var center = Center(); Native.SetCursorPos(center.X, center.Y);
    }
    private void ReadMouse()
    {
        if (!captured) return;
        var center = Center();
        if (Native.GetCursorPos(out var mouse))
        {
            // Right-handed Z-up scene: screen-right turns clockwise (negative yaw).
            game.LookBy(-(mouse.X - center.X) * .0022, -(mouse.Y - center.Y) * .0022);
        }
        var topLeft = PointToScreen(new Point(2, 2)); var bottomRight = PointToScreen(new Point(ActualWidth - 2, ActualHeight - 2));
        var rect = new Native.Rect { Left = (int)topLeft.X, Top = (int)topLeft.Y, Right = (int)bottomRight.X, Bottom = (int)bottomRight.Y };
        Native.Clip(ref rect); Native.SetCursorPos(center.X, center.Y);
    }
    private void ReleaseCursor()
    {
        if (!captured) return;
        Native.Unclip(0); captured = false; Cursor = Cursors.Arrow;
    }
    protected override void OnClosed(EventArgs e)
    {
        if (subscribed) { CompositionTarget.Rendering -= OnFrame; subscribed = false; }
        ReleaseCursor(); Native.SetCursorPos(savedCursor.X, savedCursor.Y);
        game.Shot -= audio.Shot; audio.Dispose();
        base.OnClosed(e);
    }
}
