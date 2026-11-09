using System.Runtime.InteropServices;

namespace FlightGame;

public enum SoundId
{
    Shoot,
    EnemyShoot,
    Explode,
    ExplodeBig,
    Hit,
    ShieldBreak,
    Pickup,
    Warning,
    LevelUp,
    GameOver,
}

/// <summary>
/// 极简软件音频引擎：用 winmm 的 waveOut 输出，多个音效在内存里实时混音，
/// 因此不依赖任何 NuGet 包。没有可用音频设备时自动静音降级，绝不抛异常。
/// </summary>
public static class Audio
{
    private const int SampleRate = 22050;
    private const int BufferSamples = 512;
    private const int BufferCount = 6;
    private const int WAVE_MAPPER = -1;
    private const int WHDR_DONE = 0x00000001;
    private const int VoiceLimit = 24;

    public static bool Muted { get; set; }

    private sealed class Voice
    {
        public short[] Data = [];
        public int Pos;
        public float Gain;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public int dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr hwo, int uDeviceID, ref WAVEFORMATEX pwfx, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hwo, IntPtr pwh, int cbwh);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hwo);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hwo);

    private static readonly Dictionary<SoundId, short[]> Library = [];
    private static readonly List<Voice> Voices = [];
    private static readonly object Gate = new();

    private static readonly int[] MixScratch = new int[BufferSamples];
    private static readonly short[] OutScratch = new short[BufferSamples];

    private static IntPtr _device = IntPtr.Zero;
    private static IntPtr[] _headers = [];
    private static IntPtr[] _buffers = [];
    private static int _headerSize;
    private static Thread? _thread;
    private static volatile bool _running;

    public static bool Available => _device != IntPtr.Zero;

    public static void Init()
    {
        if (_device != IntPtr.Zero) return;

        try
        {
            BuildLibrary();

            var format = new WAVEFORMATEX
            {
                wFormatTag = 1,
                nChannels = 1,
                nSamplesPerSec = SampleRate,
                nAvgBytesPerSec = SampleRate * 2,
                nBlockAlign = 2,
                wBitsPerSample = 16,
                cbSize = 0,
            };

            if (waveOutOpen(out IntPtr device, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                return;

            _device = device;
            _headerSize = Marshal.SizeOf<WAVEHDR>();
            _headers = new IntPtr[BufferCount];
            _buffers = new IntPtr[BufferCount];

            for (int i = 0; i < BufferCount; i++)
            {
                int bytes = BufferSamples * 2;

                _buffers[i] = Marshal.AllocHGlobal(bytes);
                _headers[i] = Marshal.AllocHGlobal(_headerSize);

                // 先清零，避免开局有一声爆音
                Marshal.Copy(new byte[bytes], 0, _buffers[i], bytes);

                var header = new WAVEHDR
                {
                    lpData = _buffers[i],
                    dwBufferLength = bytes,
                };

                Marshal.StructureToPtr(header, _headers[i], false);
                waveOutPrepareHeader(_device, _headers[i], _headerSize);
                waveOutWrite(_device, _headers[i], _headerSize);
            }

            _running = true;
            _thread = new Thread(MixerLoop)
            {
                IsBackground = true,
                Name = "FlightGame.Audio",
                Priority = ThreadPriority.AboveNormal,
            };
            _thread.Start();
        }
        catch
        {
            _device = IntPtr.Zero;
            _running = false;
        }
    }

    public static void Shutdown()
    {
        _running = false;

        try { _thread?.Join(300); } catch { }
        _thread = null;

        if (_device == IntPtr.Zero) return;

        try
        {
            waveOutReset(_device);

            for (int i = 0; i < _headers.Length; i++)
            {
                if (_headers[i] == IntPtr.Zero) continue;
                waveOutUnprepareHeader(_device, _headers[i], _headerSize);
                Marshal.FreeHGlobal(_headers[i]);
                _headers[i] = IntPtr.Zero;
            }

            for (int i = 0; i < _buffers.Length; i++)
            {
                if (_buffers[i] == IntPtr.Zero) continue;
                Marshal.FreeHGlobal(_buffers[i]);
                _buffers[i] = IntPtr.Zero;
            }

            waveOutClose(_device);
        }
        catch
        {
            // 退出阶段的异常无所谓
        }
        finally
        {
            _device = IntPtr.Zero;
        }
    }

    public static void Play(SoundId id, float gain = 0.85f)
    {
        if (Muted || _device == IntPtr.Zero) return;
        if (!Library.TryGetValue(id, out short[]? data) || data.Length == 0) return;

        lock (Gate)
        {
            if (Voices.Count >= VoiceLimit) Voices.RemoveAt(0);
            Voices.Add(new Voice { Data = data, Gain = Math.Clamp(gain, 0f, 1f) });
        }
    }

    // ------------------------------------------------------------------ mixer

    private static void MixerLoop()
    {
        while (_running)
        {
            bool wrote = false;

            for (int i = 0; i < BufferCount; i++)
            {
                IntPtr headerPtr = _headers[i];
                if (headerPtr == IntPtr.Zero) continue;

                WAVEHDR header = Marshal.PtrToStructure<WAVEHDR>(headerPtr);
                if ((header.dwFlags & WHDR_DONE) == 0) continue;

                waveOutUnprepareHeader(_device, headerPtr, _headerSize);

                Fill(OutScratch);
                Marshal.Copy(OutScratch, 0, _buffers[i], BufferSamples);

                var again = new WAVEHDR
                {
                    lpData = _buffers[i],
                    dwBufferLength = BufferSamples * 2,
                };

                Marshal.StructureToPtr(again, headerPtr, false);
                waveOutPrepareHeader(_device, headerPtr, _headerSize);
                waveOutWrite(_device, headerPtr, _headerSize);
                wrote = true;
            }

            if (!wrote) Thread.Sleep(5);
        }
    }

    private static void Fill(short[] destination)
    {
        Array.Clear(MixScratch);

        lock (Gate)
        {
            for (int v = Voices.Count - 1; v >= 0; v--)
            {
                Voice voice = Voices[v];
                int available = voice.Data.Length - voice.Pos;
                int count = Math.Min(destination.Length, available);

                for (int i = 0; i < count; i++)
                    MixScratch[i] += (int)(voice.Data[voice.Pos + i] * voice.Gain);

                voice.Pos += count;
                if (voice.Pos >= voice.Data.Length) Voices.RemoveAt(v);
            }
        }

        for (int i = 0; i < destination.Length; i++)
            destination[i] = (short)Math.Clamp(MixScratch[i], -32000, 32000);
    }

    // ------------------------------------------------------------ synthesis

    /// <summary>单音扫频音，shape=true 时近似方波。</summary>
    private static short[] Sweep(float seconds, float startHz, float endHz, float decay, float gain, bool square = false)
    {
        int n = Math.Max(1, (int)(seconds * SampleRate));
        var data = new short[n];
        double phase = 0;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float progress = (float)i / n;
            float hz = startHz + (endHz - startHz) * progress;

            phase += 2 * Math.PI * hz / SampleRate;

            float s = (float)Math.Sin(phase);
            if (square) s = (float)Math.Tanh(s * 3.0);

            float envelope = MathF.Exp(-t * decay);
            data[i] = (short)(Math.Clamp(s * envelope * gain, -1f, 1f) * 32000);
        }

        return data;
    }

    /// <summary>低通白噪声，用来做爆炸声。</summary>
    private static short[] Noise(float seconds, float decay, float gain, float cutoff, int seed)
    {
        int n = Math.Max(1, (int)(seconds * SampleRate));
        var data = new short[n];
        var rng = new Random(seed);
        float lowpass = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowpass += (white - lowpass) * cutoff;

            float envelope = MathF.Exp(-t * decay);
            data[i] = (short)(Math.Clamp(lowpass * envelope * gain, -1f, 1f) * 32000);
        }

        return data;
    }

    private static short[] Blend(short[] a, short[] b)
    {
        int n = Math.Max(a.Length, b.Length);
        var result = new short[n];

        for (int i = 0; i < n; i++)
        {
            int va = i < a.Length ? a[i] : 0;
            int vb = i < b.Length ? b[i] : 0;
            result[i] = (short)Math.Clamp(va + vb, -32000, 32000);
        }

        return result;
    }

    private static short[] Concat(params short[][] parts)
    {
        int total = 0;
        foreach (short[] part in parts) total += part.Length;

        var result = new short[total];
        int offset = 0;
        foreach (short[] part in parts)
        {
            Array.Copy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    private static void BuildLibrary()
    {
        if (Library.Count > 0) return;

        Library[SoundId.Shoot] = Sweep(0.07f, 1350f, 520f, 44f, 0.28f);
        Library[SoundId.EnemyShoot] = Sweep(0.10f, 430f, 150f, 30f, 0.20f, square: true);

        Library[SoundId.Explode] = Blend(
            Noise(0.30f, 16f, 0.50f, 0.35f, 11),
            Sweep(0.30f, 220f, 60f, 14f, 0.22f));

        Library[SoundId.ExplodeBig] = Blend(
            Noise(1.00f, 5.5f, 0.70f, 0.18f, 22),
            Sweep(1.00f, 150f, 40f, 4.5f, 0.42f));

        Library[SoundId.Hit] = Blend(
            Noise(0.45f, 9f, 0.55f, 0.25f, 33),
            Sweep(0.45f, 300f, 70f, 8f, 0.38f, square: true));

        Library[SoundId.ShieldBreak] = Sweep(0.30f, 950f, 240f, 15f, 0.32f, square: true);

        Library[SoundId.Pickup] = Concat(
            Sweep(0.07f, 880f, 880f, 9f, 0.30f),
            Sweep(0.10f, 1320f, 1320f, 11f, 0.30f));

        Library[SoundId.Warning] = Concat(
            Sweep(0.22f, 660f, 660f, 2.5f, 0.30f, square: true),
            Sweep(0.09f, 0f, 0f, 200f, 0f),
            Sweep(0.22f, 440f, 440f, 2.5f, 0.30f, square: true));

        Library[SoundId.LevelUp] = Concat(
            Sweep(0.09f, 523f, 523f, 7f, 0.28f),
            Sweep(0.09f, 659f, 659f, 7f, 0.28f),
            Sweep(0.20f, 784f, 784f, 5f, 0.30f));

        Library[SoundId.GameOver] = Concat(
            Sweep(0.18f, 392f, 392f, 5f, 0.28f),
            Sweep(0.18f, 311f, 311f, 5f, 0.28f),
            Sweep(0.50f, 233f, 110f, 3f, 0.34f, square: true));
    }
}
