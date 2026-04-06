// SoundService.cs — Joy Windows
// Square-wave synthesis via NAudio with Console.Beep fallback.

using System.IO;
using NAudio.Wave;

namespace Joy.Windows.Services;

public sealed class SoundService : IDisposable
{
    public bool SoundEnabled { get; set; } = true;

    public void PlayStartup()     => Play([(523,.10),(659,.10),(784,.10),(1047,.20)]);
    public void PlayFocusGained() => Play([(440,.10),(494,.15)]);
    public void PlayDistracted()  => Play([(300,.12),(220,.18)]);
    public void PlayCelebration() => Play([(784,.10),(988,.10),(1175,.10),(1568,.30)]);
    public void PlayTimerReset()  => Play([(440,.15),(330,.15),(220,.30)]);
    public void PlayTimerEnd()    => Play([(1047,.15),(784,.15),(1047,.15),(1568,.30)]);

    private void Play(IEnumerable<(double freq, double dur)> tones)
    {
        if (!SoundEnabled) return;
        var arr = tones.ToArray();
        // Fire and forget — never blocks UI thread
        Task.Run(() =>
        {
            try   { PlayNAudio(arr); }
            catch { PlayBeep(arr);   }  // always works as fallback
        });
    }

    private static void PlayNAudio((double freq, double dur)[] tones)
    {
        const int rate = 44_100;
        var floats = new List<float>();

        foreach (var (freq, dur) in tones)
        {
            int n = (int)(rate * dur);
            for (int i = 0; i < n; i++)
            {
                double t  = (double)i / rate;
                float sq  = Math.Sin(2 * Math.PI * freq * t) > 0 ? 0.25f : -0.25f;
                float env = (float)Math.Max(0, 1.0 - t / dur * 0.6);
                floats.Add(sq * env);
            }
        }

        var bytes = new byte[floats.Count * 4];
        Buffer.BlockCopy(floats.ToArray(), 0, bytes, 0, bytes.Length);

        var fmt      = WaveFormat.CreateIeeeFloatWaveFormat(rate, 1);
        var provider = new RawSourceWaveStream(new MemoryStream(bytes), fmt);

        using var output = new WaveOutEvent { Volume = 0.6f };
        output.Init(provider);
        output.Play();
        while (output.PlaybackState == PlaybackState.Playing)
            Thread.Sleep(15);
    }

    private static void PlayBeep((double freq, double dur)[] tones)
    {
        foreach (var (f, d) in tones)
            Console.Beep(Math.Clamp((int)f, 37, 32767), Math.Max((int)(d * 1000), 50));
    }

    public void Dispose() { }
}
