using System.Text.RegularExpressions;
using AudioControlApp.App;
using AudioControlApp.Config;

namespace AudioControlApp.Automation;

public sealed record AutoSwitchDecision(uint Layout, SwitchRule? Rule, string? MatchedProcess);

/// <summary>
/// Polls the process list and decides which speaker layout should be active. Decisions are
/// edge-triggered: the engine only asks for a change when the outcome of the rules changes, so a manual
/// switch made in between is respected until the matching state changes again.
/// </summary>
public sealed class AutoSwitchEngine : IDisposable
{
    private sealed record CompiledRule(SwitchRule Rule, Regex Pattern);

    private readonly SynchronizationContext _sync;
    private readonly object _gate = new();
    private System.Threading.Timer? _timer;
    private IReadOnlyList<CompiledRule> _rules = Array.Empty<CompiledRule>();
    private uint? _fallback;
    private int _intervalMs = 2000;
    private uint? _lastDesired;
    private bool _hasLast;
    private int _busy;

    /// <summary>Raised on the UI thread when the desired layout changes.</summary>
    public event Action<AutoSwitchDecision>? DecisionChanged;

    public bool IsRunning => _timer is not null;

    public AutoSwitchEngine(SynchronizationContext sync) => _sync = sync;

    public void Configure(AppSettings settings)
    {
        lock (_gate)
        {
            _rules = settings.Rules
                .Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.ProcessName))
                .Select(r => new CompiledRule(r.Clone(), BuildPattern(r.ProcessName)))
                .ToList();
            _fallback = settings.FallbackLayout;
            _intervalMs = Math.Clamp(settings.PollIntervalMs, 500, 60000);
        }

        if (settings.AutoSwitchEnabled)
        {
            Restart();
        }
        else
        {
            Stop();
        }
    }

    public void Restart()
    {
        Stop();
        _hasLast = false;
        _lastDesired = null;
        _timer = new System.Threading.Timer(Tick, null, 750, _intervalMs);
        Logger.Info($"Auto switch engine started ({_rules.Count} active rule(s), every {_intervalMs} ms).");
    }

    public void Stop()
    {
        var timer = Interlocked.Exchange(ref _timer, null);
        if (timer is not null)
        {
            timer.Dispose();
            Logger.Info("Auto switch engine stopped.");
        }
    }

    /// <summary>Evaluates the rules against a snapshot. First matching rule wins.</summary>
    public AutoSwitchDecision? Evaluate(ProcessSnapshot snapshot)
    {
        IReadOnlyList<CompiledRule> rules;
        uint? fallback;
        lock (_gate)
        {
            rules = _rules;
            fallback = _fallback;
        }

        foreach (var compiled in rules)
        {
            switch (compiled.Rule.Trigger)
            {
                case RuleTrigger.Foreground:
                    if (snapshot.Foreground is not null && compiled.Pattern.IsMatch(snapshot.Foreground))
                    {
                        return new AutoSwitchDecision(compiled.Rule.Layout, compiled.Rule, snapshot.Foreground);
                    }

                    break;

                default:
                    string? hit = snapshot.Running.FirstOrDefault(name => compiled.Pattern.IsMatch(name));
                    if (hit is not null)
                    {
                        return new AutoSwitchDecision(compiled.Rule.Layout, compiled.Rule, hit);
                    }

                    break;
            }
        }

        return fallback is null ? null : new AutoSwitchDecision(fallback.Value, null, null);
    }

    private void Tick(object? state)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = ProcessSnapshot.Capture();
            AutoSwitchDecision? decision = Evaluate(snapshot);
            uint? desired = decision?.Layout;

            if (_hasLast && desired == _lastDesired)
            {
                return;
            }

            _hasLast = true;
            _lastDesired = desired;

            if (decision is not null)
            {
                Logger.Info($"Auto switch decision: {decision.Layout:X} (rule: {decision.Rule?.ProcessName ?? "fallback"}, process: {decision.MatchedProcess ?? "-"})");
                _sync.Post(_ => DecisionChanged?.Invoke(decision), null);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Auto switch tick failed: " + ex.Message);
        }
        finally
        {
            _busy = 0;
        }
    }

    /// <summary>Converts "game*.exe" style patterns into a regular expression matching the executable name.</summary>
    public static Regex BuildPattern(string pattern)
    {
        string p = pattern.Trim();

        // Accept full paths – only the file name matters.
        int slash = p.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0)
        {
            p = p[(slash + 1)..];
        }

        bool hasWildcard = p.Contains('*') || p.Contains('?');
        if (!hasWildcard && !p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            p += ".exe";
        }

        string regex = "^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return new Regex(regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public void Dispose() => Stop();
}
