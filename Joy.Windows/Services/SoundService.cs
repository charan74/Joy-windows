// SoundService.cs — Joy Windows — MASTER VERSION
// Clean square-wave synthesis. Falls back to Console.Beep if NAudio fails.

using System.IO;
using NAudio.Wave;

namespace Joy.Windows.Services;

public sealed class SoundService : IDisposable
{
    public bool SoundEnabled { get; set; } = true;

    // Each cue is a sequence of (frequency Hz, duration seconds)
    public void PlayStartup()     => Play([(523,.10),(659,.10),(784,.10),(1047,.22)]);
    public void PlayFocusGained() => Play([(440,.10),(587,.18)]);
    public void PlayDistracted()  => Play([(330,.12),(247,.20)]);
    public void PlayCelebration() => Play([(784,.09),(988,.09),(1175,.09),(1568,.28)]);
    public void PlayTimerReset()  => Play([(494,.14),(392,.14),(294,.28)]);
    public void PlayTimerEnd()    => Play([(1047,.14),(784,.14),(1047,.14),(1568,.28)]);

    private void Play((double f, double d)[] tones)
    {
        if (!SoundEnabled) return;
        Task.Run(() =>
        {
            try   { PlayNAudio(tones); }
            catch { PlayBeep(tones);   }
        });
    }

    private static void PlayNAudio((double f, double d)[] tones)
    {
        const int rate = 44_100;
        var floats = new List<float>();

        foreach (var (freq, dur) in tones)
        {
            int n = (int)(rate * dur);
            for (int i = 0; i < n; i++)
            {
                double t   = (double)i / rate;
                // Square wave via sign of sine
                float  sq  = Math.Sin(2 * Math.PI * freq * t) > 0 ? 0.25f : -0.25f;
                // Decay envelope — avoids clicks
                float  env = (float)Math.Max(0.0, 1.0 - t / dur * 0.55);
                floats.Add(sq * env);
            }
            // Short silence between tones to prevent smearing
            for (int i = 0; i < (int)(rate * 0.015); i++) floats.Add(0f);
        }

        var bytes = new byte[floats.Count * 4];
        Buffer.BlockCopy(floats.ToArray(), 0, bytes, 0, bytes.Length);

        using var stream   = new MemoryStream(bytes);
        using var provider = new RawSourceWaveStream(stream,
            WaveFormat.CreateIeeeFloatWaveFormat(rate, 1));
        using var output   = new WaveOutEvent { Volume = 0.55f };

        output.Init(provider);
        output.Play();
        while (output.PlaybackState == PlaybackState.Playing)
            Thread.Sleep(15);
    }

    private static void PlayBeep((double f, double d)[] tones)
    {
        foreach (var (f, d) in tones)
            Console.Beep(Math.Clamp((int)f, 37, 32767), Math.Max((int)(d * 1000), 50));
    }

    public void Dispose() { }
}
