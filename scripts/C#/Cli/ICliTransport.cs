using System;

/// <summary>
/// Where CLI commands come from and where output goes. The only reason this is an interface rather
/// than direct Console calls is so a TCP control socket can be dropped in later without touching the
/// command layer, the renderer, or the input provider.
///
/// Contract: <see cref="TryRead"/> is polled from the Godot main thread. Implementations that block
/// (stdin does) must do so on their own thread and hand lines over through a thread-safe queue —
/// nothing here may touch a Godot object off the main thread.
/// </summary>
public interface ICliTransport
{
    void Start();
    bool TryRead(out string line);
    void Write(string text);
    void Stop();

    /// <summary>False once the input source is exhausted (EOF on a pipe, closed socket).</summary>
    bool IsOpen { get; }
}
