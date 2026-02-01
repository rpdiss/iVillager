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

    private DateTime _lastPlayUtc = DateTime.MinValue;
    private string? _lastKey;

    private readonly Dictionary<string, string> _tempCache =
        new(StringComparer.OrdinalIgnoreCase);

    public void PlayEmbedded(string resourceName, Assembly? assembly = null)
    {
        var now = DateTime.UtcNow;

        if (_lastKey == resourceName &&
            (now - _lastPlayUtc).TotalMilliseconds < 400)
            return;

        assembly ??= Assembly.GetExecutingAssembly();

        var tempPath = EnsureTempExtract(resourceName, assembly);

        PlayInternal(tempPath, resourceName);
    }

    public void Play(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
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
            _output?.Stop();
            _output?.Dispose();
            _reader?.Dispose();
        }
        catch { }

        _output = null;
        _reader = null;
    }

    public void Dispose() => Stop();

    private void PlayInternal(string path, string key)
    {
        Stop();

        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        _output.Init(_reader);
        _output.Play();

        _lastPlayUtc = DateTime.UtcNow;
        _lastKey = key;
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
