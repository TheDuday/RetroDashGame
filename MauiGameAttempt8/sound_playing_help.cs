using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
public class SoundManager : IDisposable
{
    private readonly MixingSampleProvider mixer;
    private readonly WaveOutEvent outputDevice;
    private readonly ConcurrentDictionary<string, CachedSound> cachedSounds = new();
    private IWavePlayer? musicOutput;
    private WaveStream? musicStream;
    private long musicPosition;  // Track the position for pausing/resuming
    private bool isMusicPlaying;

    public SoundManager()
    {
        mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
        mixer.ReadFully = true;

        outputDevice = new WaveOutEvent();
        outputDevice.Init(mixer);
        outputDevice.Play();
    }

    public void PlayLoopingMusic(string key)
    {
        StopMusic();

        if (cachedSounds.TryGetValue(key, out var sound))
        {
            // Directly use the cached sound file path
            string filePath = sound.FilePath;

            // Create an AudioFileReader directly from the file path
            musicStream = new LoopStream(new AudioFileReader(filePath)); // loop enabled
            musicOutput = new WaveOutEvent();
            musicOutput.Init(musicStream);
            musicOutput.Play();
            isMusicPlaying = true;
        }
    }

    public void StopMusic()
    {
        if (musicOutput != null)
        {
            musicOutput.Stop();
            musicOutput.Dispose();
            musicStream?.Dispose();
            musicOutput = null;
            musicStream = null;
            isMusicPlaying = false;
        }
    }

    public void PauseMusic()
    {
        if (isMusicPlaying && musicOutput != null)
        {
            musicPosition = musicStream.Position;  // Store current position before pausing
            musicOutput.Stop();
            isMusicPlaying = false;
        }
    }

    public void ResumeMusic()
    {
        if (!isMusicPlaying && musicStream != null)
        {
            musicStream.Position = musicPosition;  // Resume from the stored position
            musicOutput?.Play();
            isMusicPlaying = true;
        }
    }
    public void LoadSound(string key, string filePath)
    {
        if (!cachedSounds.ContainsKey(key))
        {
            var sound = new CachedSound(filePath);
            cachedSounds[key] = sound;
        }
    }

    public void Play(string key)
    {
        if (cachedSounds.TryGetValue(key, out var sound))
        {
            var sourceProvider = new CachedSoundSampleProvider(sound);

            // Convert to mixer format (44100 Hz stereo float)
            var resampled = new WdlResamplingSampleProvider(sourceProvider, mixer.WaveFormat.SampleRate);

            ISampleProvider provider = resampled;

            // Ensure stereo
            if (resampled.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                provider = new MonoToStereoSampleProvider(resampled);
            }

            mixer.AddMixerInput(provider);
        }
    }

    public void Dispose()
    {
        outputDevice?.Stop();
        outputDevice?.Dispose();
        musicOutput?.Dispose();
        musicStream?.Dispose();
    }
}
public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound cachedSound;
    private long position;

    public CachedSoundSampleProvider(CachedSound sound)
    {
        cachedSound = sound;
    }

    public WaveFormat WaveFormat => cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = cachedSound.AudioData.Length - position;
        var samplesToCopy = Math.Min(availableSamples, count);
        Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
        position += samplesToCopy;
        return (int)samplesToCopy;
    }
}
public class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }
    public string FilePath { get; }

    public CachedSound(string fileName)
    {
        FilePath = fileName;  // Save the path for direct access
        using var audioFileReader = new AudioFileReader(fileName);
        WaveFormat = audioFileReader.WaveFormat;

        var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
        var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];

        int samplesRead;
        while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            wholeFile.AddRange(readBuffer.Take(samplesRead));
        }

        AudioData = wholeFile.ToArray();
    }
}
public class LoopStream : WaveStream
{
    private readonly WaveStream sourceStream;

    public LoopStream(WaveStream sourceStream)
    {
        this.sourceStream = sourceStream;
        EnableLooping = true;
    }

    public bool EnableLooping { get; set; }

    public override WaveFormat WaveFormat => sourceStream.WaveFormat;
    public override long Length => sourceStream.Length;

    public override long Position
    {
        get => sourceStream.Position;
        set => sourceStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = sourceStream.Read(buffer, offset, count);
        if (read == 0 && EnableLooping)
        {
            sourceStream.Position = 0;
            read = sourceStream.Read(buffer, offset, count);
        }
        return read;
    }
}