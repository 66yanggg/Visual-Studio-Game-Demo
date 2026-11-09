using System.Drawing;

namespace FlightGame;

public enum GameState
{
    Menu,
    Playing,
    Paused,
    GameOver,
}

/// <summary>一关内部的四个阶段。</summary>
public enum LevelPhase
{
    Wave,        // 铺怪
    BossWarning, // 预警
    Boss,        // Boss 战
    Cleared,     // 过关结算
}

/// <summary>游戏世界：状态机、关卡流程、实体更新与碰撞判定。</summary>
public sealed class GameWorld
{
    public const int Width = 900;
    public const int Height = 680;

    public GameState State { get; private set; } = GameState.Menu;
    public LevelPhase Phase { get; private set; } = LevelPhase.Wave;
    public int Level { get; private set; } = 1;

    public Player Player { get; } = new();
    public Boss? Boss { get; private set; }

    public List<Bullet> Bullets { get; } = [];
    public List<EnemyBullet> EnemyBullets { get; } = [];
    public List<Enemy> Enemies { get; } = [];
    public List<PowerUp> PowerUps { get; } = [];
    public List<Particle> Particles { get; } = [];
    public List<Star> Stars { get; } = [];

    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public float Elapsed { get; private set; }

    public float ShakeTime { get; private set; }
    public float ShakePower { get; private set; }
    public float AnimTime { get; private set; }

    /// <summary>当前阶段倒计时（预警/过关）。</summary>
    public float PhaseTimer { get; private set; }

    public string BannerText { get; private set; } = "";
    public float BannerTimer { get; private set; }

    private readonly Random _rng = new();
    private float _spawnTimer;
    private int _enemiesToSpawn;
    private float _phaseTimer;

    private static string HighScoreFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlightGame", "highscore.txt");

    public GameWorld()
    {
        BuildStars();
        HighScore = LoadHighScore();
        PlacePlayerAtStart();
    }

    // ---------------------------------------------------------------- update

    public void Update(float dt, InputState input)
    {
        AnimTime += dt;
        UpdateStars(dt);
        UpdateParticles(dt);
        UpdateBanner(dt);

        if (ShakeTime > 0f)
        {
            ShakeTime = MathF.Max(0f, ShakeTime - dt);
            if (ShakeTime == 0f) ShakePower = 0f;
        }

        if (State != GameState.Playing) return;

        Elapsed += dt;

        UpdatePlayer(dt, input);
        UpdateBullets(dt);
        UpdateEnemyBullets(dt);
        UpdateEnemies(dt);
        UpdateBoss(dt);
        UpdatePowerUps(dt);
        UpdateLevelFlow(dt);
        ResolveCollisions();
    }

    private void UpdateBanner(float dt)
    {
        if (BannerTimer > 0f) BannerTimer = MathF.Max(0f, BannerTimer - dt);
    }

    // ----------------------------------------------------------------- player

    private void UpdatePlayer(float dt, InputState input)
    {
        float dirX = (input.Right ? 1f : 0f) - (input.Left ? 1f : 0f);
        float dirY = (input.Down ? 1f : 0f) - (input.Up ? 1f : 0f);

        if (dirX != 0f && dirY != 0f)
        {
            dirX *= 0.70710678f;
            dirY *= 0.70710678f;
        }

        Player.X += dirX * Player.Speed * dt;
        Player.Y += dirY * Player.Speed * dt;

        float pad = 8f;
        Player.X = Math.Clamp(Player.X, Player.W * 0.5f + pad, Width - Player.W * 0.5f - pad);
        Player.Y = Math.Clamp(Player.Y, Player.H * 0.5f + pad, Height - Player.H * 0.5f - pad);

        Player.Bank += (dirX * 0.9f - Player.Bank) * MathF.Min(1f, dt * 12f);

        if (Player.InvulnerableFor > 0f)
            Player.InvulnerableFor = MathF.Max(0f, Player.InvulnerableFor - dt);

        Player.RapidFor = MathF.Max(0f, Player.RapidFor - dt);
        Player.SpreadFor = MathF.Max(0f, Player.SpreadFor - dt);
        Player.ShieldFor = MathF.Max(0f, Player.ShieldFor - dt);

        Player.FireCooldown -= dt;

        float interval = Player.RapidFor > 0f ? Player.FireInterval * 0.45f : Player.FireInterval;
        if (input.Fire && Player.FireCooldown <= 0f)
        {
            Player.FireCooldown = interval;
            FireWeapon();
        }
    }

    private void FireWeapon()
    {
        float noseY = Player.Y - Player.H * 0.5f + 10f;

        Audio.Play(SoundId.Shoot, 0.45f);

        if (Player.SpreadFor > 0f)
        {
            // 五向散射
            float[] angles = [-0.30f, -0.15f, 0f, 0.15f, 0.30f];
            foreach (float a in angles)
            {
                float speed = 720f;
                Bullets.Add(new Bullet
                {
                    X = Player.X,
                    Y = noseY,
                    VX = MathF.Sin(a) * speed,
                    VY = -MathF.Cos(a) * speed,
                });
            }
        }
        else
        {
            Bullets.Add(new Bullet { X = Player.X - 9f, Y = noseY });
            Bullets.Add(new Bullet { X = Player.X + 9f, Y = noseY });
        }
    }

    private void UpdateBullets(float dt)
    {
        foreach (Bullet b in Bullets)
        {
            b.X += b.VX * dt;
            b.Y += b.VY * dt;
        }

        Bullets.RemoveAll(b => b.Y < -30f || b.X < -30f || b.X > Width + 30f);
    }

    private void UpdateEnemyBullets(float dt)
    {
        foreach (EnemyBullet b in EnemyBullets)
        {
            b.X += b.VX * dt;
            b.Y += b.VY * dt;
        }

        EnemyBullets.RemoveAll(b => b.Y > Height + 40f || b.Y < -80f || b.X < -60f || b.X > Width + 60f);
    }

    // ---------------------------------------------------------------- enemies

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy e in Enemies)
        {
            e.Y += e.Speed * dt;
            e.X += e.Drift * dt;
            e.Spin += dt * 2.4f;

            if (e.X < e.W * 0.5f + 24f || e.X > Width - e.W * 0.5f - 24f)
                e.Drift = -e.Drift;

            // 重型敌机会打冷枪
            if (!e.Heavy) continue;

            e.FireTimer -= dt;
            if (e.FireTimer > 0f) continue;

            e.FireTimer = 2.4f + (float)_rng.NextDouble() * 1.2f;

            float dx = Player.X - e.X;
            float dy = Player.Y - e.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1f) len = 1f;

            float speed = 190f + Level * 8f;
            AddEnemyBullet(e.X, e.Y + e.H * 0.4f, dx / len * speed, dy / len * speed);
            Audio.Play(SoundId.EnemyShoot, 0.28f);
        }

        Enemies.RemoveAll(e => e.Y > Height + 70f);
    }

    private void AddEnemyBullet(float x, float y, float vx, float vy)
    {
        EnemyBullets.Add(new EnemyBullet { X = x, Y = y, VX = vx, VY = vy });
    }

    // -------------------------------------------------------------------- boss

    private void UpdateBoss(float dt)
    {
        if (Boss is not { } boss) return;

        if (boss.HitFlash > 0f) boss.HitFlash = MathF.Max(0f, boss.HitFlash - dt);

        // 入场：从画面上方压下来
        if (boss.Y < 200f)
        {
            boss.Y = MathF.Min(200f, boss.Y + 115f * dt);
            return;
        }

        boss.Spin += dt * 1.5f;

        boss.X += boss.DirX * dt;
        float margin = boss.W * 0.5f + 36f;
        if (boss.X < margin)
        {
            boss.X = margin;
            boss.DirX = MathF.Abs(boss.DirX);
        }
        else if (boss.X > Width - margin)
        {
            boss.X = Width - margin;
            boss.DirX = -MathF.Abs(boss.DirX);
        }

        boss.FireTimer -= dt;
        if (boss.FireTimer > 0f) return;

        BossFire(boss);

        bool enraged = boss.Hp <= boss.MaxHp * 0.5f;
        float interval = MathF.Max(0.55f, 1.55f - Level * 0.06f);
        if (enraged) interval *= 0.65f;

        boss.FireTimer = interval;
    }

    private void BossFire(Boss boss)
    {
        bool enraged = boss.Hp <= boss.MaxHp * 0.5f;
        int pattern = (boss.ShotsFired / 4) % 3;
        boss.ShotsFired++;

        Audio.Play(SoundId.EnemyShoot, 0.38f);

        float speed = 190f + Level * 10f;
        float muzzleY = boss.Y + boss.H * 0.34f;

        switch (pattern)
        {
            case 0:
            {
                // 瞄准三连（狂暴时五连）
                float dx = Player.X - boss.X;
                float dy = Player.Y - muzzleY;
                float baseAngle = MathF.Atan2(dy, dx);

                int count = enraged ? 5 : 3;
                for (int i = 0; i < count; i++)
                {
                    float angle = baseAngle + (i - (count - 1) / 2f) * 0.13f;
                    AddEnemyBullet(boss.X, muzzleY, MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
                }
                break;
            }

            case 1:
            {
                // 向下扇形
                int count = enraged ? 13 : 9;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathF.PI * (0.16f + 0.68f * i / (count - 1));
                    AddEnemyBullet(boss.X, muzzleY,
                        MathF.Cos(angle) * speed * 0.9f,
                        MathF.Sin(angle) * speed * 0.9f);
                }
                break;
            }

            default:
            {
                // 天降弹雨
                int count = enraged ? 7 : 5;
                for (int i = 0; i < count; i++)
                {
                    float x = 60f + (float)_rng.NextDouble() * (Width - 120f);
                    AddEnemyBullet(x, -12f,
                        ((float)_rng.NextDouble() - 0.5f) * 40f,
                        speed * 0.9f);
                }
                break;
            }
        }
    }

    // --------------------------------------------------------------- powerups

    private void UpdatePowerUps(float dt)
    {
        foreach (PowerUp p in PowerUps)
        {
            p.Y += p.Speed * dt;
            p.Spin += dt * 3f;
        }

        PowerUps.RemoveAll(p => p.Y > Height + 40f);
    }

    private void MaybeDropPowerUp(float x, float y, bool guaranteed = false)
    {
        if (!guaranteed && _rng.NextDouble() > 0.13) return;

        PowerUps.Add(new PowerUp { X = x, Y = y, Kind = PickKind() });
    }

    private PowerUpKind PickKind()
    {
        int roll = _rng.Next(100);
        if (roll < 28) return PowerUpKind.Rapid;
        if (roll < 56) return PowerUpKind.Spread;
        if (roll < 78) return PowerUpKind.Shield;
        if (roll < 92) return PowerUpKind.Score;
        return PowerUpKind.Life;
    }

    private void ApplyPowerUp(PowerUp powerUp)
    {
        Audio.Play(SoundId.Pickup, 0.7f);
        Score += 50;

        switch (powerUp.Kind)
        {
            case PowerUpKind.Rapid:
                Player.RapidFor = 12f;
                break;
            case PowerUpKind.Spread:
                Player.SpreadFor = 12f;
                break;
            case PowerUpKind.Shield:
                Player.ShieldFor = 10f;
                break;
            case PowerUpKind.Life:
                Player.Lives = Math.Min(Player.MaxLives, Player.Lives + 1);
                break;
            case PowerUpKind.Score:
                Score += 500;
                break;
        }
    }

    // ------------------------------------------------------------ level flow

    private void UpdateLevelFlow(float dt)
    {
        switch (Phase)
        {
            case LevelPhase.Wave:
                if (_enemiesToSpawn > 0)
                {
                    _spawnTimer -= dt;
                    if (_spawnTimer > 0f) break;

                    SpawnEnemy();
                    _enemiesToSpawn--;
                    _spawnTimer = SpawnInterval();
                }
                else if (Enemies.Count == 0)
                {
                    Phase = LevelPhase.BossWarning;
                    _phaseTimer = 2.4f;
                    Audio.Play(SoundId.Warning, 0.8f);
                }
                break;

            case LevelPhase.BossWarning:
                _phaseTimer -= dt;
                if (_phaseTimer > 0f) break;

                SpawnBoss();
                Phase = LevelPhase.Boss;
                break;

            case LevelPhase.Cleared:
                _phaseTimer -= dt;
                if (_phaseTimer > 0f) break;

                Level++;
                StartLevel();
                break;
        }

        PhaseTimer = _phaseTimer;
    }

    private float SpawnInterval() =>
        MathF.Max(0.24f, 0.85f - Level * 0.055f) * (0.8f + (float)_rng.NextDouble() * 0.5f);

    private void SpawnEnemy()
    {
        bool heavy = Level >= 2 && _rng.NextDouble() < 0.16 + Level * 0.04;

        var e = new Enemy
        {
            X = 60f + (float)_rng.NextDouble() * (Width - 120f),
            Y = -60f,
            Heavy = heavy,
            Drift = ((float)_rng.NextDouble() - 0.5f) * (heavy ? 45f : 100f),
            FireTimer = 1.2f + (float)_rng.NextDouble() * 1.5f,
        };

        if (heavy)
        {
            e.W = 54f;
            e.H = 52f;
            e.Hp = 3;
            e.MaxHp = 3;
            e.Speed = 70f + Level * 6f;
            e.Radius = 23f;
            e.Score = 300;
        }
        else
        {
            e.Speed = 118f + (float)_rng.NextDouble() * 60f + Level * 10f;
            e.Score = 100;
        }

        Enemies.Add(e);
    }

    private void SpawnBoss()
    {
        float hp = 55f + Level * 40f;

        Boss = new FlightGame.Boss
        {
            X = Width * 0.5f,
            Y = -150f,
            Hp = hp,
            MaxHp = hp,
            DirX = 90f + Level * 8f,
            FireTimer = 1.5f,
        };

        BannerText = $"第 {Level} 关 BOSS";
        BannerTimer = 1.8f;
    }

    private void KillBoss()
    {
        if (Boss is not { } boss) return;

        Boss = null;
        Score += 1200 * Level;
        EnemyBullets.Clear();

        SpawnExplosion(boss.X, boss.Y, 90, true);
        SpawnExplosion(boss.X - 60f, boss.Y + 20f, 30, false);
        SpawnExplosion(boss.X + 60f, boss.Y + 20f, 30, false);
        Shake(0.7f, 18f);
        Audio.Play(SoundId.ExplodeBig);

        // 固定掉三个道具，中间保证是加命
        for (int i = 0; i < 3; i++)
        {
            PowerUps.Add(new PowerUp
            {
                X = boss.X + (i - 1) * 58f,
                Y = boss.Y,
                Speed = 80f,
                Kind = i == 1 ? PowerUpKind.Life : PickKind(),
            });
        }

        Phase = LevelPhase.Cleared;
        _phaseTimer = 3.2f;
        PhaseTimer = _phaseTimer;

        BannerText = "BOSS 击破！";
        BannerTimer = 2.4f;
    }

    private void StartLevel()
    {
        Phase = LevelPhase.Wave;

        Bullets.Clear();
        Enemies.Clear();
        EnemyBullets.Clear();
        PowerUps.Clear();
        Boss = null;

        _enemiesToSpawn = 8 + Level * 3;
        _spawnTimer = 1.0f;
        _phaseTimer = 0f;
        PhaseTimer = 0f;

        BannerText = $"第 {Level} 关";
        BannerTimer = 2.0f;

        Audio.Play(SoundId.LevelUp, 0.6f);
    }

    // ------------------------------------------------------------ collisions

    private void ResolveCollisions()
    {
        ResolveBulletHits();
        ResolvePlayerHits();
        ResolvePowerUpPickups();
    }

    private void ResolveBulletHits()
    {
        // 子弹 → 普通敌机
        foreach (Bullet b in Bullets)
        {
            if (b.Dead) continue;

            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;

                float dx = b.X - e.X;
                float dy = b.Y - e.Y;
                float r = b.Radius + e.Radius;
                if (dx * dx + dy * dy > r * r) continue;

                b.Dead = true;
                e.Hp--;

                if (e.Hp > 0)
                {
                    SpawnSparks(b.X, b.Y, 6);
                    Shake(0.05f, 1.5f);
                    break;
                }

                e.Dead = true;
                Score += e.Score;
                SpawnExplosion(e.X, e.Y, e.Heavy ? 38 : 22, e.Heavy);
                Shake(0.14f, e.Heavy ? 7f : 3f);
                Audio.Play(SoundId.Explode, e.Heavy ? 0.8f : 0.6f);
                MaybeDropPowerUp(e.X, e.Y);

                break;
            }
        }

        Bullets.RemoveAll(b => b.Dead);
        Enemies.RemoveAll(e => e.Dead);

        // 子弹 → Boss
        if (Boss is not { } boss) return;

        foreach (Bullet b in Bullets)
        {
            if (b.Dead) continue;

            if (MathF.Abs(b.X - boss.X) > boss.W * 0.5f) continue;
            if (MathF.Abs(b.Y - boss.Y) > boss.H * 0.5f) continue;

            b.Dead = true;
            boss.Hp -= 1f;
            boss.HitFlash = 0.10f;
            SpawnSparks(b.X, b.Y, 3);

            if (boss.Hp <= 0f) KillBoss();
        }

        Bullets.RemoveAll(b => b.Dead);
    }

    private void ResolvePlayerHits()
    {
        if (!Player.IsAlive) return;

        // 敌方弹幕
        foreach (EnemyBullet bullet in EnemyBullets)
        {
            if (bullet.Dead) continue;

            float dx = bullet.X - Player.X;
            float dy = bullet.Y - Player.Y;
            float r = bullet.Radius + Player.Radius;
            if (dx * dx + dy * dy > r * r) continue;

            bullet.Dead = true;
            DamagePlayer();
            break;
        }

        EnemyBullets.RemoveAll(b => b.Dead);

        if (!Player.IsAlive) return;

        // 撞机
        foreach (Enemy e in Enemies)
        {
            float dx = e.X - Player.X;
            float dy = e.Y - Player.Y;
            float r = e.Radius + Player.Radius;
            if (dx * dx + dy * dy > r * r) continue;

            e.Dead = true;
            SpawnExplosion(e.X, e.Y, 16, false);
            DamagePlayer();
            break;
        }

        Enemies.RemoveAll(e => e.Dead);

        if (!Player.IsAlive) return;

        // 撞 Boss
        if (Boss is { } boss)
        {
            float dx = boss.X - Player.X;
            float dy = boss.Y - Player.Y;
            float r = boss.Radius * 0.7f + Player.Radius;
            if (dx * dx + dy * dy <= r * r) DamagePlayer();
        }
    }

    private void ResolvePowerUpPickups()
    {
        foreach (PowerUp p in PowerUps)
        {
            if (p.Dead) continue;

            float dx = p.X - Player.X;
            float dy = p.Y - Player.Y;
            float r = p.Radius + Player.Radius + 6f;
            if (dx * dx + dy * dy > r * r) continue;

            p.Dead = true;
            ApplyPowerUp(p);
        }

        PowerUps.RemoveAll(p => p.Dead);
    }

    private void DamagePlayer()
    {
        if (!Player.IsAlive) return;

        // 护盾优先抵挡
        if (Player.ShieldFor > 0f)
        {
            Player.ShieldFor = 0f;
            Player.InvulnerableFor = 1.2f;
            SpawnExplosion(Player.X, Player.Y, 20, false);
            Shake(0.22f, 7f);
            Audio.Play(SoundId.ShieldBreak, 0.8f);
            return;
        }

        if (Player.InvulnerableFor > 0f) return;

        Player.Lives--;
        Player.InvulnerableFor = 2.2f;
        Player.RapidFor = 0f;
        Player.SpreadFor = 0f;

        SpawnExplosion(Player.X, Player.Y, 26, false);
        Shake(0.32f, 13f);
        Audio.Play(SoundId.Hit, 0.9f);

        if (Player.Lives > 0) return;

        Player.Lives = 0;
        SpawnExplosion(Player.X, Player.Y, 46, true);
        State = GameState.GameOver;

        Audio.Play(SoundId.GameOver, 0.9f);

        if (Score > HighScore)
        {
            HighScore = Score;
            SaveHighScore();
        }
    }

    private void Shake(float time, float power)
    {
        if (power < ShakePower && ShakeTime > 0f) return;

        ShakeTime = MathF.Max(ShakeTime, time);
        ShakePower = MathF.Max(ShakePower, power);
    }

    // --------------------------------------------------------------- effects

    private void SpawnExplosion(float x, float y, int count, bool big)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            float speed = (float)(_rng.NextDouble() * (big ? 420f : 280f) + 40f);
            float life = 0.35f + (float)_rng.NextDouble() * (big ? 0.85f : 0.5f);

            Particles.Add(new Particle
            {
                X = x,
                Y = y,
                VX = (float)Math.Cos(angle) * speed,
                VY = (float)Math.Sin(angle) * speed,
                MaxLife = life,
                Life = life,
                Size = 2f + (float)_rng.NextDouble() * (big ? 6f : 4f),
                Color = PickExplosionColor(),
            });
        }
    }

    private void SpawnSparks(float x, float y, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            float speed = (float)(_rng.NextDouble() * 160f + 40f);
            float life = 0.18f + (float)_rng.NextDouble() * 0.22f;

            Particles.Add(new Particle
            {
                X = x,
                Y = y,
                VX = (float)Math.Cos(angle) * speed,
                VY = (float)Math.Sin(angle) * speed,
                MaxLife = life,
                Life = life,
                Size = 1.5f + (float)_rng.NextDouble() * 2.5f,
                Color = Color.FromArgb(255, 255, 224, 130),
            });
        }
    }

    private Color PickExplosionColor()
    {
        int roll = _rng.Next(100);
        if (roll < 45) return Color.FromArgb(255, 255, 214, 120);
        if (roll < 75) return Color.FromArgb(255, 255, 150, 60);
        if (roll < 92) return Color.FromArgb(255, 255, 90, 50);
        return Color.FromArgb(255, 255, 255, 235);
    }

    private void UpdateStars(float dt)
    {
        foreach (Star s in Stars)
        {
            s.Y += s.Speed * dt;
            if (s.Y > Height + 4f)
            {
                s.Y = -6f;
                s.X = (float)_rng.NextDouble() * Width;
            }
            s.Twinkle += dt * 2.6f;
        }
    }

    private void UpdateParticles(float dt)
    {
        foreach (Particle p in Particles)
        {
            p.X += p.VX * dt;
            p.Y += p.VY * dt;

            float drag = MathF.Pow(p.Drag, dt * 60f);
            p.VX *= drag;
            p.VY *= drag;
            p.Life -= dt;
        }

        Particles.RemoveAll(p => p.IsDead);
    }

    private void BuildStars()
    {
        for (int i = 0; i < 165; i++)
        {
            float z = (float)_rng.NextDouble();
            Stars.Add(new Star
            {
                X = (float)_rng.NextDouble() * Width,
                Y = (float)_rng.NextDouble() * Height,
                Speed = 16f + z * 95f,
                Size = 1f + z * 2.2f,
                Alpha = 0.22f + z * 0.7f,
                Twinkle = (float)(_rng.NextDouble() * Math.PI * 2),
                Tint = z > 0.86f ? Color.FromArgb(200, 225, 255) : Color.White,
            });
        }
    }

    // ------------------------------------------------------------- commands

    public void Confirm()
    {
        switch (State)
        {
            case GameState.Menu:
            case GameState.GameOver:
                StartGame();
                break;
            case GameState.Paused:
                State = GameState.Playing;
                break;
        }
    }

    public void TogglePause()
    {
        if (State == GameState.Playing) State = GameState.Paused;
        else if (State == GameState.Paused) State = GameState.Playing;
    }

    public void PauseIfPlaying()
    {
        if (State == GameState.Playing) State = GameState.Paused;
    }

    public void Restart()
    {
        if (State is GameState.Playing or GameState.Paused or GameState.GameOver)
            StartGame();
    }

    public void StartGame()
    {
        Bullets.Clear();
        EnemyBullets.Clear();
        Enemies.Clear();
        PowerUps.Clear();
        Particles.Clear();
        Boss = null;

        Score = 0;
        Elapsed = 0f;
        Level = 1;

        ShakeTime = 0f;
        ShakePower = 0f;

        Player.Lives = 3;
        Player.InvulnerableFor = 0f;
        Player.FireCooldown = 0f;
        Player.Bank = 0f;
        Player.RapidFor = 0f;
        Player.SpreadFor = 0f;
        Player.ShieldFor = 0f;

        PlacePlayerAtStart();

        State = GameState.Playing;
        StartLevel();
    }

    private void PlacePlayerAtStart()
    {
        Player.X = Width * 0.5f;
        Player.Y = Height - 110f;
    }

    // ------------------------------------------------------------ high score

    private static int LoadHighScore()
    {
        try
        {
            if (!File.Exists(HighScoreFile)) return 0;
            string text = File.ReadAllText(HighScoreFile).Trim();
            return int.TryParse(text, out int value) && value > 0 ? value : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void SaveHighScore()
    {
        try
        {
            string? dir = Path.GetDirectoryName(HighScoreFile);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(HighScoreFile, HighScore.ToString());
        }
        catch
        {
            // 最高分保存失败不影响游戏
        }
    }
}
