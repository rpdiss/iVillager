using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace iVillager.Services;

public sealed class AudioService : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private DateTime _lastPlayUtc = DateTime.MinValue;
    private string? _lastKey;

    private readonly Dictionary<string, string> _tempCache =
        new(StringComparer.OrdinalIgnoreCase);

    public void PlayEmbedded(string resourceName)
    {
        var now = DateTime.UtcNow;

        if (_lastKey == resourceName &&
            (now - _lastPlayUtc).TotalMilliseconds < 400)
            return;

        // Sprawdź czy zasób istnieje
        var resourceExists = _assembly.GetManifestResourceNames()
            .Any(n => n.Equals(resourceName, StringComparison.OrdinalIgnoreCase));

        if (!resourceExists)
        {
            // Możesz dodać fallback do szukania pliku
            var fileName = Path.GetFileName(resourceName);
            var fallbackPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Sounds",
                fileName);

            if (File.Exists(fallbackPath))
            {
                Play(fallbackPath);
                return;
            }

            throw new FileNotFoundException($"Resource not found: {resourceName}");
        }

        var tempPath = EnsureTempExtract(resourceName, _assembly);
        PlayInternal(tempPath, resourceName);
    }

    public void Play(string pathOrRelative)
    {
        // >>> ważne: obsłuż absolutne ścieżki poprawnie
        var path = Path.IsPathRooted(pathOrRelative)
            ? pathOrRelative
            : Path.Combine(AppContext.BaseDirectory, pathOrRelative);

        if (!File.Exists(path))
            return;

        var now = DateTime.UtcNow;
        if (_lastKey == path &&
            (now - _lastPlayUtc).TotalMilliseconds < 400)
            return;

        PlayInternal(path, path);
    }

    public void Stop()
    {
        try
        {
            _output?.Stop(); // PlaybackStopped zrobi resztę sprzątania
        }
        catch { /* ignore */ }

        CleanupReaderOnly();
        _lastKey = null;
    }

    public void Dispose()
    {
        try
        {
            if (_output != null)
            {
                _output.PlaybackStopped -= Output_PlaybackStopped;
                _output.Stop();
                _output.Dispose();
                _output = null;
            }
        }
        catch { /* ignore */ }

        CleanupReaderOnly();
    }

    private void PlayInternal(string path, string key)
    {
        EnsureOutput();

        // reader podmieniamy za każdym razem
        CleanupReaderOnly();

        _reader = new AudioFileReader(path);
        _output!.Init(_reader);
        _output.Play();

        _lastPlayUtc = DateTime.UtcNow;
        _lastKey = key;
    }

    private void EnsureOutput()
    {
        if (_output != null)
            return;

        _output = new WaveOutEvent{};
        _output.PlaybackStopped += Output_PlaybackStopped;
    }

    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        CleanupReaderOnly();
    }

    private void CleanupReaderOnly()
    {
        try { _reader?.Dispose(); } catch { /* ignore */ }
        _reader = null;
    }

    private string EnsureTempExtract(string resourceName, Assembly asm)
    {
        if (_tempCache.TryGetValue(resourceName, out var cached) &&
            File.Exists(cached) &&
            new FileInfo(cached).Length > 0)
            return cached;

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded resource stream missing: {resourceName}");

        var dir = Path.Combine(Path.GetTempPath(), "iVillager", "sounds");
        Directory.CreateDirectory(dir);

        const string marker = ".Assets.Sounds.";
        var idx = resourceName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        var fileName = idx >= 0
            ? resourceName[(idx + marker.Length)..]
            : Path.GetFileName(resourceName);

        var asmStamp = File.GetLastWriteTimeUtc(asm.Location)
            .ToString("yyyyMMddHHmmss");

        var tempPath = Path.Combine(dir, $"{asmStamp}_{fileName}");

        if (!File.Exists(tempPath))
        {
            using var fs = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            stream.CopyTo(fs);
            fs.Flush(true);
        }

        if (new FileInfo(tempPath).Length <= 0)
            throw new IOException($"Extracted audio is empty: {tempPath}");

        _tempCache[resourceName] = tempPath;
        return tempPath;
    }
}
