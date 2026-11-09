namespace FlightGame;

/// <summary>一帧的输入快照。</summary>
public readonly record struct InputState(bool Left, bool Right, bool Up, bool Down, bool Fire)
{
    public static readonly InputState Empty = new(false, false, false, false, false);
}
