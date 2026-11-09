using System.Drawing;

namespace FlightGame;

/// <summary>玩家的飞行器。</summary>
public sealed class Player
{
    public const int MaxLives = 5;

    public float X;
    public float Y;

    public float W = 42f;
    public float H = 46f;

    public float Speed = 380f;
    public int Lives = 3;

    /// <summary>受击后的无敌时间（秒）。</summary>
    public float InvulnerableFor;

    public float FireCooldown;
    public float FireInterval = 0.14f;

    /// <summary>侧移时的视觉倾斜量。</summary>
    public float Bank;

    // ---- 道具带来的增益 ----
    public float RapidFor;   // 急速射击
    public float SpreadFor;  // 散射
    public float ShieldFor;  // 护盾（挡一次伤害后破掉）

    public float Radius => 15f;
    public bool IsAlive => Lives > 0;
    public bool IsInvulnerable => InvulnerableFor > 0f;
}

/// <summary>玩家子弹。</summary>
public sealed class Bullet
{
    public float X;
    public float Y;
    public float VX;
    public float VY = -700f;
    public float Radius = 3.5f;
    public bool Dead;
}

/// <summary>敌人（含 Boss）发射的弹幕。</summary>
public sealed class EnemyBullet
{
    public float X;
    public float Y;
    public float VX;
    public float VY;
    public float Radius = 7f;
    public bool Dead;
}

/// <summary>普通敌机。</summary>
public sealed class Enemy
{
    public float X;
    public float Y;

    public float W = 34f;
    public float H = 34f;

    public float Speed = 140f;
    public float Drift;
    public float Spin;

    public int Hp = 1;
    public int MaxHp = 1;
    public int Score = 100;
    public float Radius = 15f;

    /// <summary>重型敌机的开火冷却。</summary>
    public float FireTimer = 2f;

    public bool Heavy;
    public bool Dead;
}

/// <summary>Boss：血厚、多阶段弹幕。</summary>
public sealed class Boss
{
    public float X;
    public float Y;

    public float W = 170f;
    public float H = 132f;

    public float Hp = 80f;
    public float MaxHp = 80f;

    public float Radius = 74f;
    public float DirX = 95f;

    public float FireTimer = 1.4f;
    public int ShotsFired;

    public float HitFlash;
    public float Spin;
}

public enum PowerUpKind
{
    Rapid,
    Spread,
    Shield,
    Life,
    Score,
}

/// <summary>掉落的道具。</summary>
public sealed class PowerUp
{
    public float X;
    public float Y;
    public float Speed = 95f;
    public float Spin;
    public float Radius = 17f;
    public PowerUpKind Kind;
    public bool Dead;
}

/// <summary>爆炸 / 火花粒子。</summary>
public sealed class Particle
{
    public float X;
    public float Y;
    public float VX;
    public float VY;
    public float Life;
    public float MaxLife;
    public float Size;
    public float Drag = 0.94f;
    public Color Color;

    public bool IsDead => Life <= 0f;
}

/// <summary>背景星空中的一颗星。</summary>
public sealed class Star
{
    public float X;
    public float Y;
    public float Speed;
    public float Size;
    public float Alpha;
    public float Twinkle;
    public Color Tint;
}
