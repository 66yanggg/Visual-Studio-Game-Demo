using System.Diagnostics;

namespace FlightGame;

public sealed class GameForm : Form
{
    private readonly GameWorld _world = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<Keys> _down = [];

    private double _lastTime;

    public GameForm()
    {
        Text = "Sky Raider · 飞行器大作战";
        ClientSize = new Size(GameWorld.Width, GameWorld.Height);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.Black;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        KeyPreview = true;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _timer.Interval = 16;
        _timer.Tick += OnTick;

        _lastTime = _clock.Elapsed.TotalSeconds;
        _timer.Start();

        Audio.Init();
    }

    /// <summary>让窗体直接接收方向键，避免被当成焦点导航。</summary>
    protected override bool IsInputKey(Keys keyData) => true;

    private void OnTick(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)(now - _lastTime);
        _lastTime = now;

        // 窗口最小化或断点调试后回到前台时，避免一帧跳太远。
        dt = Math.Clamp(dt, 0f, 0.05f);

        var input = new InputState(
            Left: _down.Contains(Keys.Left) || _down.Contains(Keys.A),
            Right: _down.Contains(Keys.Right) || _down.Contains(Keys.D),
            Up: _down.Contains(Keys.Up) || _down.Contains(Keys.W),
            Down: _down.Contains(Keys.Down) || _down.Contains(Keys.S),
            Fire: _down.Contains(Keys.Space));

        _world.Update(dt, input);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 长按会重复触发 KeyDown，只有第一次按下才算“新按键”。
        bool isNewPress = _down.Add(e.KeyCode);
        if (!isNewPress) return;

        switch (e.KeyCode)
        {
            case Keys.P:
            case Keys.Escape:
                _world.TogglePause();
                break;
            case Keys.R:
                _world.Restart();
                break;
            case Keys.M:
                Audio.Muted = !Audio.Muted;
                if (!Audio.Muted) Audio.Play(SoundId.Pickup, 0.6f);
                break;
            case Keys.Enter:
                _world.Confirm();
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _down.Remove(e.KeyCode);
        base.OnKeyUp(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        // 切走窗口时松开所有按键，回来不会“粘键”。
        _down.Clear();
        _world.PauseIfPlaying();

        base.OnDeactivate(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        GameRenderer.Render(e.Graphics, _world);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _world.SaveHighScore();
        _timer.Stop();
        _timer.Dispose();
        Audio.Shutdown();
        base.OnFormClosing(e);
    }
}
