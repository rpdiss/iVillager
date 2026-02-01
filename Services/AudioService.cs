using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace iVillager.Services;

public class AudioService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private Mp3FileReader? _mp3Reader;
    private bool _isDisposed;
    private readonly object _lock = new();

    public void PlayEmbedded(string resourceName)
    {
        lock (_lock)
        {
            Stop();

            try
            {
                Console.WriteLine($"=== AudioService.PlayEmbedded: {resourceName} ===");

                var assembly = Assembly.GetExecutingAssembly();

                var allResources = assembly.GetManifestResourceNames();
                Console.WriteLine("All embedded resources:");
                foreach (var res in allResources)
                {
                    Console.WriteLine($"  - {res}");
                }

                Stream? stream = null;

                Console.WriteLine($"Trying full resource name: {resourceName}");
                stream = assembly.GetManifestResourceStream(resourceName);


                if (stream == null)
                {
                    var fileName = Path.GetFileName(resourceName);
                    Console.WriteLine($"Trying file name only: {fileName}");

                    foreach (var res in allResources)
                    {
                        if (res.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Found match: {res}");
                            stream = assembly.GetManifestResourceStream(res);
                            break;
                        }
                    }
                }

                if (stream == null)
                {
                    var fileName = Path.GetFileName(resourceName);
                    var soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", fileName);
                    Console.WriteLine($"Trying file path: {soundPath}");

                    if (File.Exists(soundPath))
                    {
                        PlayFromFile(soundPath);
                        return;
                    }
                }

                if (stream == null)
                {
                    Console.WriteLine($"ERROR: Could not find sound: {resourceName}");
                    return;
                }

                PlayFromStream(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in PlayEmbedded: {ex.Message}");
                Cleanup();
            }
        }
    }

    private void PlayFromStream(Stream stream)
    {
        try
        {
            Console.WriteLine("Creating Mp3FileReader from stream...");

            if (stream.CanSeek)
                stream.Position = 0;

            _mp3Reader = new Mp3FileReader(stream);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_mp3Reader);

            _waveOut.PlaybackStopped += (s, e) =>
            {
                Console.WriteLine("Playback stopped.");
                Cleanup();
            };

            _waveOut.Play();
            Console.WriteLine("Playback started.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in PlayFromStream: {ex.Message}");
            Cleanup();
        }
    }

    private void PlayFromFile(string filePath)
    {
        try
        {
            Console.WriteLine($"Playing from file: {filePath}");

            _mp3Reader = new Mp3FileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_mp3Reader);

            _waveOut.PlaybackStopped += (s, e) =>
            {
                Console.WriteLine("Playback stopped.");
                Cleanup();
            };

            _waveOut.Play();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in PlayFromFile: {ex.Message}");
            Cleanup();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _mp3Reader?.Dispose();
            _mp3Reader = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Stop();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}