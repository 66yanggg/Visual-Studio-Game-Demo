using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FlightGame;

/// <summary>把 GameWorld 画到 Graphics 上。无状态，可复用。</summary>
public static class GameRenderer
{
    private static readonly Font FontTitle = new("Microsoft YaHei UI", 58f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font FontSubtitle = new("Microsoft YaHei UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font FontBody = new("Microsoft YaHei UI", 17f, FontStyle.Regular, GraphicsUnit.Pixel);
    private static readonly Font FontHud = new("Consolas", 21f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font FontHudSmall = new("Consolas", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
    private static readonly Font FontBanner = new("Microsoft YaHei UI", 40f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font FontIcon = new("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
    private static readonly Font FontPill = new("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);

    private static readonly Dictionary<(float W, float H), GraphicsPath> PlanePaths = new();
    private static readonly Dictionary<(float W, float H), GraphicsPath> BossPaths = new();

    private static LinearGradientBrush? _skyBrush;
    private static PathGradientBrush? _topGlow;
    private static PathGradientBrush? _bottomGlow;

    // ------------------------------------------------------------------ main

    public static void Render(Graphics g, GameWorld world)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        DrawBackground(g, world);

        GraphicsState state = g.Save();

        if (world.ShakeTime > 0f)
        {
            float mag = world.ShakePower * Math.Clamp(world.ShakeTime / 0.22f, 0f, 1f);
            float ox = ((float)Random.Shared.NextDouble() * 2f - 1f) * mag;
            float oy = ((float)Random.Shared.NextDouble() * 2f - 1f) * mag;
            g.TranslateTransform(ox, oy);
        }

        DrawPowerUps(g, world);
        DrawEnemies(g, world);
        DrawBoss(g, world);
        DrawEnemyBullets(g, world);
        DrawBullets(g, world);
        DrawPlayer(g, world);
        DrawParticles(g, world);

        g.Restore(state);

        DrawHud(g, world);
        DrawBossBar(g, world);
        DrawBanner(g, world);
        DrawOverlay(g, world);
    }

    // ------------------------------------------------------------ background

    private static GraphicsPath? _topGlowPath;
    private static GraphicsPath? _bottomGlowPath;

    private static GraphicsPath TopGlowPath => _topGlowPath ??= EllipsePath(-340, -620, 1580, 1240);
    private static GraphicsPath BottomGlowPath => _bottomGlowPath ??= EllipsePath(-260, 400, 1420, 760);

    private static void DrawBackground(Graphics g, GameWorld world)
    {
        EnsureBackgroundBrushes();

        g.FillRectangle(_skyBrush!, 0, 0, GameWorld.Width, GameWorld.Height);
        g.FillPath(_topGlow!, TopGlowPath);
        g.FillPath(_bottomGlow!, BottomGlowPath);

        using var starBrush = new SolidBrush(Color.White);

        foreach (Star s in world.Stars)
        {
            float twinkle = 0.72f + 0.28f * MathF.Sin(s.Twinkle);
            int alpha = (int)Math.Clamp(s.Alpha * twinkle * 255f, 0f, 255f);

            starBrush.Color = Color.FromArgb(alpha, s.Tint);
            g.FillEllipse(starBrush, s.X, s.Y, s.Size, s.Size);
        }
    }

    private static void EnsureBackgroundBrushes()
    {
        if (_skyBrush != null) return;

        var rect = new Rectangle(0, 0, GameWorld.Width, GameWorld.Height);
        _skyBrush = new LinearGradientBrush(
            rect,
            Color.FromArgb(9, 12, 34),
            Color.FromArgb(42, 15, 60),
            90f);

        _topGlow = new PathGradientBrush(TopGlowPath)
        {
            CenterColor = Color.FromArgb(72, 92, 70, 176),
            SurroundColors = TransparentSurround(TopGlowPath),
        };

        _bottomGlow = new PathGradientBrush(BottomGlowPath)
        {
            CenterColor = Color.FromArgb(48, 34, 92, 156),
            SurroundColors = TransparentSurround(BottomGlowPath),
        };
    }

    private static GraphicsPath EllipsePath(float x, float y, float w, float h)
    {
        var path = new GraphicsPath();
        path.AddEllipse(x, y, w, h);
        return path;
    }

    private static Color[] TransparentSurround(GraphicsPath path)
    {
        var colors = new Color[path.PathPoints.Length];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.FromArgb(0, 0, 0, 0);
        return colors;
    }

    // ---------------------------------------------------------------- planes

    private static GraphicsPath PlanePath(float w, float h)
    {
        if (PlanePaths.TryGetValue((w, h), out GraphicsPath? cached))
            return cached;

        float[][] points =
        [
            [0.00f, -0.50f],
            [0.10f, -0.30f],
            [0.14f, -0.05f],
            [0.46f,  0.10f],
            [0.46f,  0.22f],
            [0.18f,  0.18f],
            [0.14f,  0.36f],
            [0.32f,  0.46f],
            [0.32f,  0.52f],
            [0.06f,  0.46f],
            [0.00f,  0.42f],
            [-0.06f, 0.46f],
            [-0.32f, 0.52f],
            [-0.32f, 0.46f],
            [-0.14f, 0.36f],
            [-0.18f, 0.18f],
            [-0.46f, 0.22f],
            [-0.46f, 0.10f],
            [-0.14f, -0.05f],
            [-0.10f, -0.30f],
        ];

        var path = new GraphicsPath();
        path.AddPolygon(points.Select(p => new PointF(p[0] * w, p[1] * h)).ToArray());

        PlanePaths[(w, h)] = path;
        return path;
    }

    private static GraphicsPath BossPath(float w, float h)
    {
        if (BossPaths.TryGetValue((w, h), out GraphicsPath? cached))
            return cached;

        float[][] points =
        [
            [-0.50f, -0.22f],
            [-0.34f, -0.46f],
            [0.34f,  -0.46f],
            [0.50f,  -0.22f],
            [0.44f,  0.08f],
            [0.24f,  0.28f],
            [0.10f,  0.50f],
            [-0.10f, 0.50f],
            [-0.24f, 0.28f],
            [-0.44f, 0.08f],
        ];

        var path = new GraphicsPath();
        path.AddPolygon(points.Select(p => new PointF(p[0] * w, p[1] * h)).ToArray());

        BossPaths[(w, h)] = path;
        return path;
    }

    /// <summary>在已平移/旋转的坐标系里画一架朝上的飞机。</summary>
    private static void DrawPlaneShape(Graphics g, float w, float h, Color body, Color trim, Color cockpit, float flame)
    {
        GraphicsPath path = PlanePath(w, h);

        if (flame > 0f)
        {
            float length = h * (0.30f + 0.42f * flame);
            PointF[] fire =
            [
                new(-w * 0.13f, h * 0.40f),
                new(0f, h * 0.40f + length),
                new(w * 0.13f, h * 0.40f),
            ];

            using var inner = new SolidBrush(Color.FromArgb(235, 255, 240, 170));
            using var outer = new SolidBrush(Color.FromArgb(150, 255, 150, 50));

            var longer = new[]
            {
                new PointF(-w * 0.20f, h * 0.40f),
                new PointF(0f, h * 0.40f + length * 1.45f),
                new PointF(w * 0.20f, h * 0.40f),
            };

            g.FillPolygon(outer, longer);
            g.FillPolygon(inner, fire);
        }

        using (var bodyBrush = new SolidBrush(body))
            g.FillPath(bodyBrush, path);

        using (var trimPen = new Pen(trim, 2f))
            g.DrawPath(trimPen, path);

        float cw = w * 0.24f;
        float ch = h * 0.26f;
        using (var glass = new SolidBrush(cockpit))
            g.FillEllipse(glass, -cw * 0.5f, -h * 0.20f, cw, ch);

        using (var rim = new Pen(Color.FromArgb(150, 255, 255, 255), 1f))
            g.DrawEllipse(rim, -cw * 0.5f, -h * 0.20f, cw, ch);
    }

    private static void DrawPlayer(Graphics g, GameWorld world)
    {
        Player p = world.Player;
        if (!p.IsAlive) return;

        if (p.IsInvulnerable && ((int)(p.InvulnerableFor * 14f) % 2 == 0)) return;

        GraphicsState playerState = g.Save();
        g.TranslateTransform(p.X, p.Y);
        g.RotateTransform(p.Bank * 16f);

        float flame = 0.55f + 0.45f * MathF.Sin(world.AnimTime * 26f);
        Color body = p.IsInvulnerable
            ? Color.FromArgb(150, 120, 210, 255)
            : Color.FromArgb(255, 70, 170, 235);

        DrawPlaneShape(g, p.W, p.H, body, Color.FromArgb(255, 190, 240, 255), Color.FromArgb(255, 200, 245, 255), flame);

        g.Restore(playerState);

        // 护盾泡
        if (p.ShieldFor > 0f)
        {
            float radius = 34f + 2.5f * MathF.Sin(world.AnimTime * 8f);
            int alpha = (int)(90f + 50f * MathF.Sin(world.AnimTime * 10f));

            using var shieldPath = new GraphicsPath();
            shieldPath.AddEllipse(p.X - radius, p.Y - radius, radius * 2f, radius * 2f);

            using var brush = new PathGradientBrush(shieldPath)
            {
                CenterColor = Color.FromArgb(30, 120, 220, 255),
                SurroundColors = FilledSurround(shieldPath, Color.FromArgb(alpha, 130, 220, 255)),
            };

            g.FillPath(brush, shieldPath);

            using var pen = new Pen(Color.FromArgb(Math.Min(255, alpha + 80), 150, 230, 255), 1.8f);
            g.DrawEllipse(pen, p.X - radius, p.Y - radius, radius * 2f, radius * 2f);
        }
    }

    private static Color[] FilledSurround(GraphicsPath path, Color color)
    {
        var colors = new Color[path.PathPoints.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        return colors;
    }

    private static void DrawEnemies(Graphics g, GameWorld world)
    {
        foreach (Enemy e in world.Enemies)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(e.X, e.Y);
            g.RotateTransform(180f + MathF.Sin(e.Spin) * 7f);

            float flame = 0.4f + 0.35f * MathF.Sin(world.AnimTime * 19f + e.X);

            if (e.Heavy)
            {
                DrawPlaneShape(g, e.W, e.H,
                    Color.FromArgb(255, 138, 70, 196),
                    Color.FromArgb(255, 226, 170, 255),
                    Color.FromArgb(255, 250, 210, 255),
                    flame);
            }
            else
            {
                DrawPlaneShape(g, e.W, e.H,
                    Color.FromArgb(255, 214, 68, 68),
                    Color.FromArgb(255, 255, 170, 160),
                    Color.FromArgb(255, 255, 220, 210),
                    flame);
            }

            g.Restore(state);

            if (!e.Heavy || e.Hp >= e.MaxHp) continue;

            float barW = e.W * 0.9f;
            float barX = e.X - barW * 0.5f;
            float barY = e.Y - e.H * 0.5f - 12f;

            using (var back = new SolidBrush(Color.FromArgb(160, 20, 20, 30)))
                g.FillRectangle(back, barX, barY, barW, 4f);

            using (var fill = new SolidBrush(Color.FromArgb(255, 255, 190, 90)))
                g.FillRectangle(fill, barX, barY, barW * e.Hp / e.MaxHp, 4f);
        }
    }

    // ------------------------------------------------------------------ boss

    private static void DrawBoss(Graphics g, GameWorld world)
    {
        if (world.Boss is not { } boss) return;

        bool flash = boss.HitFlash > 0f;
        bool enraged = boss.Hp <= boss.MaxHp * 0.5f;

        GraphicsState bossState = g.Save();
        g.TranslateTransform(boss.X, boss.Y);

        GraphicsPath hull = BossPath(boss.W, boss.H);

        // 外发光
        using (var glow = new SolidBrush(Color.FromArgb(flash ? 110 : 60, 170, 90, 240)))
        {
            var glowPath = new GraphicsPath();
            glowPath.AddPolygon(ScalePoints(hull.PathPoints, 1.06f));
            g.FillPath(glow, glowPath);
            glowPath.Dispose();
        }

        Color body = Color.FromArgb(255, 62, 50, 92);
        Color trim = Color.FromArgb(255, 190, 140, 235);

        using (var bodyBrush = new SolidBrush(body))
            g.FillPath(bodyBrush, hull);

        using (var trimPen = new Pen(trim, 2.5f))
            g.DrawPath(trimPen, hull);

        // 侧翼吊舱
        float podW = boss.W * 0.24f;
        float podH = boss.H * 0.28f;

        using (var podBrush = new SolidBrush(Color.FromArgb(255, 92, 72, 138)))
        using (var podPen = new Pen(trim, 1.6f))
        {
            g.FillEllipse(podBrush, -boss.W * 0.50f + podW * 0.08f, -boss.H * 0.05f, podW, podH);
            g.DrawEllipse(podPen, -boss.W * 0.50f + podW * 0.08f, -boss.H * 0.05f, podW, podH);

            g.FillEllipse(podBrush, boss.W * 0.50f - podW * 1.08f, -boss.H * 0.05f, podW, podH);
            g.DrawEllipse(podPen, boss.W * 0.50f - podW * 1.08f, -boss.H * 0.05f, podW, podH);
        }

        // 炮口
        using (var cannon = new SolidBrush(Color.FromArgb(255, 40, 34, 66)))
        {
            g.FillRectangle(cannon, -boss.W * 0.34f, boss.H * 0.22f, boss.W * 0.12f, boss.H * 0.20f);
            g.FillRectangle(cannon, boss.W * 0.22f, boss.H * 0.22f, boss.W * 0.12f, boss.H * 0.20f);
        }

        // 核心
        float pulse = 0.65f + 0.35f * MathF.Sin(world.AnimTime * (enraged ? 13f : 6f));
        int coreAlpha = (int)(120 + 110 * pulse);
        Color coreColor = enraged
            ? Color.FromArgb(coreAlpha, 255, 80, 70)
            : Color.FromArgb(coreAlpha, 150, 120, 255);

        using (var core = new SolidBrush(coreColor))
            g.FillEllipse(core, -boss.W * 0.13f, -boss.H * 0.03f, boss.W * 0.26f, boss.H * 0.26f);

        using (var coreRim = new Pen(Color.FromArgb(200, 235, 215, 255), 1.4f))
            g.DrawEllipse(coreRim, -boss.W * 0.13f, -boss.H * 0.03f, boss.W * 0.26f, boss.H * 0.26f);

        // 受击高光：半透明叠加，不会把整架 Boss 糊成白色
        if (flash)
        {
            int alpha = (int)Math.Clamp(boss.HitFlash / 0.10f * 130f, 0f, 150f);

            using var flashBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.FillPath(flashBrush, hull);
        }

        g.Restore(bossState);
    }

    private static PointF[] ScalePoints(PointF[] points, float scale)
    {
        var result = new PointF[points.Length];
        for (int i = 0; i < points.Length; i++)
            result[i] = new PointF(points[i].X * scale, points[i].Y * scale);
        return result;
    }

    private static void DrawBossBar(Graphics g, GameWorld world)
    {
        if (world.Boss is not { } boss) return;

        // 入场阶段还没血条，避免压住 Boss 本体
        if (boss.Y < 199f) return;

        float width = 560f;
        float height = 15f;
        float x = (GameWorld.Width - width) * 0.5f;
        float y = 98f;

        float ratio = Math.Clamp(boss.Hp / boss.MaxHp, 0f, 1f);
        bool enraged = ratio <= 0.5f;

        using (var border = new SolidBrush(Color.FromArgb(150, 10, 10, 18)))
            g.FillRectangle(border, x - 3f, y - 3f, width + 6f, height + 6f);

        using (var back = new SolidBrush(Color.FromArgb(200, 30, 24, 44)))
            g.FillRectangle(back, x, y, width, height);

        Color fillColor = enraged
            ? Color.FromArgb(255, 255, 70, 70)
            : Color.FromArgb(255, 255, 160, 70);

        using (var fill = new SolidBrush(fillColor))
            g.FillRectangle(fill, x, y, width * ratio, height);

        using (var shine = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            g.FillRectangle(shine, x, y, width * ratio, height * 0.42f);

        using (var pen = new Pen(Color.FromArgb(190, 210, 190, 255), 1.4f))
            g.DrawRectangle(pen, x, y, width, height);

        DrawCenteredText(g, enraged ? "BOSS  ·  狂暴" : "BOSS", FontHudSmall,
            enraged ? Color.FromArgb(255, 255, 130, 130) : Color.FromArgb(220, 235, 220, 255),
            GameWorld.Width * 0.5f, y - 14f);
    }

    // --------------------------------------------------------------- bullets

    private static void DrawEnemyBullets(Graphics g, GameWorld world)
    {
        using var core = new SolidBrush(Color.FromArgb(255, 255, 238, 205));
        using var glow = new SolidBrush(Color.FromArgb(70, 255, 110, 80));
        using var ring = new Pen(Color.FromArgb(150, 255, 150, 110), 1.4f);

        foreach (EnemyBullet b in world.EnemyBullets)
        {
            float r = b.Radius;

            g.FillEllipse(glow, b.X - r * 2.0f, b.Y - r * 2.0f, r * 4.0f, r * 4.0f);
            g.FillEllipse(core, b.X - r * 0.65f, b.Y - r * 0.65f, r * 1.3f, r * 1.3f);

            using var ringBrush = new SolidBrush(Color.FromArgb(120, 255, 120, 90));
            g.FillEllipse(ringBrush, b.X - r * 0.95f, b.Y - r * 0.95f, r * 1.9f, r * 1.9f);
            g.DrawEllipse(ring, b.X - r * 1.05f, b.Y - r * 1.05f, r * 2.1f, r * 2.1f);
        }
    }

    private static void DrawBullets(Graphics g, GameWorld world)
    {
        using var core = new SolidBrush(Color.FromArgb(255, 255, 255, 220));
        using var glow = new SolidBrush(Color.FromArgb(90, 120, 220, 255));

        foreach (Bullet b in world.Bullets)
        {
            g.FillEllipse(glow, b.X - 7f, b.Y - 14f, 14f, 22f);
            g.FillRectangle(core, b.X - 2f, b.Y - 12f, 4f, 16f);
        }
    }

    // -------------------------------------------------------------- powerups

    private static Color PowerUpColor(PowerUpKind kind) => kind switch
    {
        PowerUpKind.Rapid => Color.FromArgb(255, 255, 226, 120),
        PowerUpKind.Spread => Color.FromArgb(255, 130, 235, 150),
        PowerUpKind.Shield => Color.FromArgb(255, 120, 200, 255),
        PowerUpKind.Life => Color.FromArgb(255, 255, 130, 180),
        _ => Color.FromArgb(255, 255, 205, 100),
    };

    private static string PowerUpGlyph(PowerUpKind kind) => kind switch
    {
        PowerUpKind.Rapid => "R",
        PowerUpKind.Spread => "S",
        PowerUpKind.Shield => "O",
        PowerUpKind.Life => "+",
        _ => "$",
    };

    private static void DrawPowerUps(Graphics g, GameWorld world)
    {
        foreach (PowerUp p in world.PowerUps)
        {
            Color color = PowerUpColor(p.Kind);

            float pulse = 0.75f + 0.25f * MathF.Sin(world.AnimTime * 7f + p.Spin);
            using (var glow = new SolidBrush(Color.FromArgb((int)(55 * pulse), color)))
                g.FillEllipse(glow, p.X - 26f, p.Y - 26f, 52f, 52f);

            GraphicsState state = g.Save();
            g.TranslateTransform(p.X, p.Y);
            g.RotateTransform(p.Spin * 42f);

            using var path = new GraphicsPath();
            path.AddPolygon(
            [
                new PointF(0f, -15f),
                new PointF(15f, 0f),
                new PointF(0f, 15f),
                new PointF(-15f, 0f),
            ]);

            using (var fill = new SolidBrush(Color.FromArgb(230, color)))
                g.FillPath(fill, path);

            using (var pen = new Pen(Color.FromArgb(240, 255, 255, 255), 1.8f))
                g.DrawPath(pen, path);

            g.Restore(state);

            DrawCenteredText(g, PowerUpGlyph(p.Kind), FontIcon, Color.FromArgb(255, 20, 24, 40), p.X, p.Y);
        }
    }

    private static void DrawParticles(Graphics g, GameWorld world)
    {
        using var brush = new SolidBrush(Color.White);

        foreach (Particle p in world.Particles)
        {
            float t = Math.Clamp(p.Life / MathF.Max(p.MaxLife, 0.0001f), 0f, 1f);
            int alpha = (int)(t * 255f);

            brush.Color = Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B);

            float size = p.Size * (0.35f + 0.65f * t);
            g.FillEllipse(brush, p.X - size * 0.5f, p.Y - size * 0.5f, size, size);
        }
    }

    // ------------------------------------------------------------------- hud

    private static void DrawHud(Graphics g, GameWorld world)
    {
        using var primary = new SolidBrush(Color.FromArgb(240, 232, 240, 255));
        using var secondary = new SolidBrush(Color.FromArgb(150, 178, 196, 255));

        g.DrawString($"SCORE {world.Score:000000}", FontHud, primary, 18f, 12f);
        g.DrawString($"HI    {world.HighScore:000000}", FontHudSmall, secondary, 19f, 40f);
        g.DrawString($"LEVEL {world.Level}", FontHudSmall, secondary, 19f, 61f);

        for (int i = 0; i < world.Player.Lives; i++)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(GameWorld.Width - 38f - i * 34f, 34f);
            DrawPlaneShape(g, 22f, 24f,
                Color.FromArgb(230, 90, 190, 245),
                Color.FromArgb(200, 200, 240, 255),
                Color.FromArgb(200, 220, 250, 255),
                0f);
            g.Restore(state);
        }

        DrawBuffs(g, world);
    }

    private static void DrawBuffs(Graphics g, GameWorld world)
    {
        float y = 86f;

        if (world.Player.RapidFor > 0f)
        {
            DrawPill(g, 18f, y, "连射", world.Player.RapidFor, 12f, PowerUpColor(PowerUpKind.Rapid));
            y += 26f;
        }

        if (world.Player.SpreadFor > 0f)
        {
            DrawPill(g, 18f, y, "散射", world.Player.SpreadFor, 12f, PowerUpColor(PowerUpKind.Spread));
            y += 26f;
        }

        if (world.Player.ShieldFor > 0f)
            DrawPill(g, 18f, y, "护盾", world.Player.ShieldFor, 10f, PowerUpColor(PowerUpKind.Shield));
    }

    private static void DrawPill(Graphics g, float x, float y, string label, float remaining, float total, Color color)
    {
        float width = 104f;
        float height = 20f;

        using (var back = new SolidBrush(Color.FromArgb(150, 8, 10, 22)))
            g.FillRectangle(back, x, y, width, height);

        float ratio = Math.Clamp(remaining / total, 0f, 1f);

        using (var fill = new SolidBrush(Color.FromArgb(70, color)))
            g.FillRectangle(fill, x, y, width * ratio, height);

        using (var dot = new SolidBrush(color))
            g.FillRectangle(dot, x, y, 3f, height);

        using (var text = new SolidBrush(Color.FromArgb(235, 235, 240, 255)))
            g.DrawString($"{label} {remaining:0.0}s", FontPill, text, x + 8f, y + 3f);
    }

    private static void DrawBanner(Graphics g, GameWorld world)
    {
        if (world.BannerTimer <= 0f || string.IsNullOrEmpty(world.BannerText)) return;
        if (world.State != GameState.Playing && world.State != GameState.Paused) return;

        float alpha = Math.Clamp(world.BannerTimer / 0.6f, 0f, 1f);
        int a = (int)(alpha * 235f);

        DrawCenteredText(g, world.BannerText, FontBanner, Color.FromArgb(a, 255, 255, 255),
            GameWorld.Width * 0.5f, 320f);
    }

    // --------------------------------------------------------------- overlays

    private static void DrawOverlay(Graphics g, GameWorld world)
    {
        if (world.State == GameState.Playing && world.Phase == LevelPhase.BossWarning)
            DrawBossWarning(g, world);

        switch (world.State)
        {
            case GameState.Menu:
                DrawDim(g, 165);
                DrawMenu(g, world);
                break;

            case GameState.Paused:
                DrawDim(g, 120);
                DrawCenteredText(g, "已暂停", FontTitle, Color.FromArgb(245, 245, 255), GameWorld.Width * 0.5f, 240f);
                DrawCenteredText(g, "按 [P] 或 [回车] 继续", FontBody, Color.FromArgb(190, 200, 225), GameWorld.Width * 0.5f, 330f);
                break;

            case GameState.GameOver:
                DrawDim(g, 175);
                DrawGameOver(g, world);
                break;
        }
    }

    private static void DrawBossWarning(Graphics g, GameWorld world)
    {
        using (var tint = new SolidBrush(Color.FromArgb(52, 140, 10, 20)))
            g.FillRectangle(tint, 0, 0, GameWorld.Width, GameWorld.Height);

        bool blink = world.PhaseTimer % 0.55f > 0.22f;
        if (!blink) return;

        float cx = GameWorld.Width * 0.5f;

        DrawCenteredText(g, "WARNING", FontTitle, Color.FromArgb(90, 255, 60, 60), cx + 2f, 262f);
        DrawCenteredText(g, "WARNING", FontTitle, Color.FromArgb(255, 255, 90, 90), cx, 260f);
        DrawCenteredText(g, "强大的敌机正在接近", FontSubtitle, Color.FromArgb(230, 255, 190, 190), cx, 330f);
    }

    private static void DrawMenu(Graphics g, GameWorld world)
    {
        float cx = GameWorld.Width * 0.5f;

        DrawCenteredText(g, "SKY RAIDER", FontTitle, Color.FromArgb(70, 90, 190, 255), cx + 2f, 152f);
        DrawCenteredText(g, "SKY RAIDER", FontTitle, Color.FromArgb(255, 255, 255, 255), cx, 150f);
        DrawCenteredText(g, "飞  行  器  大  作  战", FontSubtitle, Color.FromArgb(235, 120, 220, 255), cx, 215f);

        using var line = new Pen(Color.FromArgb(80, 120, 190, 255), 1.5f);
        g.DrawLine(line, cx - 200f, 250f, cx + 200f, 250f);

        DrawCenteredText(g, "方向键 / WASD   移动", FontBody, Color.FromArgb(210, 220, 240), cx, 312f);
        DrawCenteredText(g, "空格   发射激光", FontBody, Color.FromArgb(210, 220, 240), cx, 350f);
        DrawCenteredText(g, "P 暂停      R 重新开始      M 静音", FontBody, Color.FromArgb(210, 220, 240), cx, 388f);

        DrawCenteredText(g, "击落敌机得分 · 撞机损失一架 · 打完全部敌机迎接 BOSS", FontBody,
            Color.FromArgb(170, 188, 220), cx, 440f);

        DrawCenteredText(g, "道具：  R 连射   S 散射   O 护盾   + 加命   $ 分数", FontBody,
            Color.FromArgb(255, 226, 150), cx, 484f);

        bool blink = world.AnimTime % 1.1f < 0.75f;
        if (blink)
            DrawCenteredText(g, "按 [回车] 或 [空格] 开始", FontSubtitle, Color.FromArgb(255, 255, 225, 130), cx, 528f);

        string audioNote = Audio.Available ? (Audio.Muted ? "音效：已静音" : "音效：已开启") : "音效：未检测到音频设备";
        DrawCenteredText(g, audioNote, FontHudSmall, Color.FromArgb(140, 170, 190, 220), cx, 612f);

        DrawCenteredText(g, $"最高分  {world.HighScore:000000}", FontHudSmall, Color.FromArgb(150, 178, 196, 255), cx, 644f);
    }

    private static void DrawGameOver(Graphics g, GameWorld world)
    {
        float cx = GameWorld.Width * 0.5f;

        DrawCenteredText(g, "GAME OVER", FontTitle, Color.FromArgb(90, 255, 80, 80), cx + 2f, 202f);
        DrawCenteredText(g, "GAME OVER", FontTitle, Color.FromArgb(255, 255, 255, 255), cx, 200f);

        DrawCenteredText(g, $"本局得分   {world.Score:000000}", FontSubtitle, Color.FromArgb(240, 240, 255), cx, 292f);
        DrawCenteredText(g, $"最高分     {world.HighScore:000000}", FontBody, Color.FromArgb(175, 195, 230), cx, 340f);
        DrawCenteredText(g, $"抵达第 {world.Level} 关 · 存活 {world.Elapsed:0.0} 秒", FontBody,
            Color.FromArgb(160, 180, 215), cx, 382f);

        bool blink = world.AnimTime % 1.1f < 0.75f;
        if (blink)
            DrawCenteredText(g, "按 [R] 重新开始", FontSubtitle, Color.FromArgb(255, 255, 225, 130), cx, 480f);
    }

    private static void DrawDim(Graphics g, int alpha)
    {
        using var brush = new SolidBrush(Color.FromArgb(alpha, 6, 8, 22));
        g.FillRectangle(brush, 0, 0, GameWorld.Width, GameWorld.Height);
    }

    private static void DrawCenteredText(Graphics g, string text, Font font, Color color, float cx, float cy)
    {
        using var brush = new SolidBrush(color);
        SizeF size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, cx - size.Width * 0.5f, cy - size.Height * 0.5f);
    }
}
