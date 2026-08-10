using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Answers <see cref="InputRequest"/>s from the terminal. Renders the prompt with its enumerated
/// legal options, then parks the game loop on a TaskCompletionSource until an answer command lands.
///
/// The TCS mirrors NetworkApi.SendInputRequest's own rendezvous, and follows the project's stated
/// preference for TaskCompletionSource over Godot signals when bridging into a single awaitable.
/// </summary>
public sealed class CliInputProvider : IInputProvider
{
    private readonly CliRenderer _renderer;

    private TaskCompletionSource<bool> _pending;
    private InputRequest _request;
    private InputRequestSpec _spec;

    /// <summary>Auto-answer the next N prompts by passing, for `run`/`auto`.</summary>
    public int AutoPassCount { get; set; }

    public bool HasOpenPrompt => _pending != null;
    public InputRequestSpec OpenSpec => _spec;
    public InputRequest OpenRequest => _request;

    public CliInputProvider(CliRenderer renderer) { _renderer = renderer; }

    public async Task Resolve(InputRequest request)
    {
        InputRequestSpec spec = InputRequestSpec.For(request);

        if (AutoPassCount > 0)
        {
            AutoPassCount--;
            spec.ApplyPass(request);
            _renderer.Emit(new CliEvent("auto_passed")
                .Set("kind", spec.Kind).Set("faction", spec.Faction.ToString())
                .Text($"AUTO {spec.Kind} {spec.Faction} — passed ({AutoPassCount} left)"));
            return;
        }

        _request = request;
        _spec = spec;
        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _renderer.Prompt(spec, request);

        // Register with PendingLocalInput so the host abandoning this request — input timeout, error
        // recovery sweep, AbortInputRequest — releases the CLI prompt through exactly the same path
        // the GUI uses. Without it an aborted request would leave the terminal waiting forever on a
        // prompt the game has already given up on.
        using (PendingLocalInput.Register(() => ForceRelease()))
        {
            await _pending.Task;
        }
    }

    /// <summary>Re-emit the open prompt. The recovery command when a prompt line is lost in output.</summary>
    public bool Reprompt()
    {
        if (_pending == null) return false;
        _renderer.Prompt(_spec, _request);
        return true;
    }

    /// <summary>Answer by 1-based option indices. Returns an error string, or null on success.</summary>
    public string AnswerByIndex(List<int> indices)
    {
        if (_pending == null) return "no prompt is open";

        foreach (int i in indices)
            if (i < 1 || i > _spec.Options.Count)
                return $"index {i} out of range (1-{_spec.Options.Count})";

        return Answer(indices.Select(i => _spec.Options[i - 1]).ToList());
    }

    /// <summary>Answer by raw game ids, matched against the prompt's own option list.</summary>
    public string AnswerById(List<int> ids)
    {
        if (_pending == null) return "no prompt is open";

        List<CliOption> chosen = new();
        foreach (int id in ids)
        {
            // Match against the offered options, not the whole game: choosing an id that is real but
            // not legal here must be rejected, not silently written into the response.
            CliOption option = _spec.Options.FirstOrDefault(o => o.Id == id);
            if (option == null) return $"id {id} is not one of this prompt's options";
            chosen.Add(option);
        }
        return Answer(chosen);
    }

    /// <summary>Answer by name (country, card, or faction), matched against the option list.</summary>
    public string AnswerByName(string name)
    {
        if (_pending == null) return "no prompt is open";

        List<CliOption> matches = _spec.Options
            .Where(o => o.Label.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            matches = _spec.Options
                .Where(o => o.Label.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (matches.Count == 0) return $"no option matching '{name}'";
        if (matches.Count > 1)
            return $"'{name}' is ambiguous: {string.Join(", ", matches.Select(m => m.Label))}";

        return Answer(matches);
    }

    private string Answer(List<CliOption> chosen)
    {
        if (chosen.Count < _spec.MinSelections)
            return $"need at least {_spec.MinSelections} selection(s), got {chosen.Count}";
        if (chosen.Count > _spec.MaxSelections)
            return $"at most {_spec.MaxSelections} selection(s) allowed, got {chosen.Count}";

        _spec.Apply(_request, chosen);
        _renderer.Emit(new CliEvent("answered")
            .Set("kind", _spec.Kind)
            .Set("faction", _spec.Faction.ToString())
            .Set("chosen", chosen.Select(c => c.Label).ToList())
            .Text($"OK  {_spec.Kind} {_spec.Faction} <- {string.Join(", ", chosen.Select(c => c.Label))}"));

        Release();
        return null;
    }

    /// <summary>Decline, using this request type's own pass idiom (see PassMode).</summary>
    public string Pass()
    {
        if (_pending == null) return "no prompt is open";
        if (!_spec.CanPass) return $"{_spec.Kind} is mandatory — you must choose {_spec.MinSelections}";

        _spec.ApplyPass(_request);
        _renderer.Emit(new CliEvent("answered")
            .Set("kind", _spec.Kind).Set("faction", _spec.Faction.ToString()).Set("passed", true)
            .Text($"OK  {_spec.Kind} {_spec.Faction} <- pass"));

        Release();
        return null;
    }

    /// <summary>Release without answering, for a request the host has abandoned.</summary>
    private void ForceRelease()
    {
        if (_pending == null) return;
        _spec.ApplyPass(_request);
        _renderer.Emit(new CliEvent("prompt_aborted")
            .Set("kind", _spec.Kind)
            .Text($"--- {_spec.Kind} was abandoned by the host; released as a pass"));
        Release();
    }

    private void Release()
    {
        TaskCompletionSource<bool> pending = _pending;
        _pending = null;
        _request = null;
        _spec = null;
        pending?.TrySetResult(true);
    }
}
