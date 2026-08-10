using System;
using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// stdin/stdout transport. <c>Console.In.ReadLine()</c> blocks, so it runs on a background thread
/// whose only job is to enqueue strings — it never touches a Godot object.
///
/// A ConcurrentQueue drained from _Process is used rather than CallDeferred from the reader thread:
/// it gives unambiguous ordering, natural back-pressure, and avoids marshalling a Callable from a
/// non-Godot thread.
/// </summary>
public sealed class StdioTransport : ICliTransport
{
    private readonly ConcurrentQueue<string> _inbox = new();
    private readonly object _writeLock = new();
    private Thread _reader;
    private volatile bool _eof;
    private volatile bool _stopped;

    public bool IsOpen => !_eof && !_stopped;

    public void Start()
    {
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "qg-cli-stdin" };
        _reader.Start();
    }

    private void ReadLoop()
    {
        try
        {
            while (!_stopped)
            {
                string line = Console.In.ReadLine();
                if (line == null) { _eof = true; return; }   // pipe closed / ^D
                _inbox.Enqueue(line);
            }
        }
        catch (Exception)
        {
            // Nothing useful to do from a background thread with no Godot access; the session sees
            // IsOpen go false and shuts down cleanly.
            _eof = true;
        }
    }

    public bool TryRead(out string line) => _inbox.TryDequeue(out line);

    public void Write(string text)
    {
        // Locked so a future socket transport can share this shape, and so a line never interleaves
        // with another writer mid-string.
        lock (_writeLock) { Console.Out.Write(text); Console.Out.Flush(); }
    }

    public void Stop() => _stopped = true;
}
