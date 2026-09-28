using System.IO;
using System.Media;

namespace BimArena.Desktop;

internal sealed class GameAudio : IDisposable
{
    private readonly MemoryStream stream;
    private readonly SoundPlayer player;
    public bool Muted { get; set; }
    public GameAudio()
    {
        // Original, synthesized shotgun report. No Doom/WAD assets are required.
        const int rate = 22050, samples = 5500;
        stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(samples * 2);
            var random = new Random(77);
            for (int i = 0; i < samples; i++)
            {
                double time = (double)i / rate;
                double sound = ((random.NextDouble() * 2 - 1) * .60 + Math.Sin(time * 2 * Math.PI * 75) * .4) * Math.Exp(-time * 21);
                writer.Write((short)(sound * 20000));
            }
        }
        stream.Position = 0; player = new SoundPlayer(stream);
    }
    public void Shot() { if (!Muted) { try { player.Play(); } catch (Exception) { Muted = true; } } }
    public void Dispose() { player.Stop(); player.Dispose(); stream.Dispose(); }
}
