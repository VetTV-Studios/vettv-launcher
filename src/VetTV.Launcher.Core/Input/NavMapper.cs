namespace VetTV.Launcher.Core.Input;
/// Turns polled pad states into NavActions: edge-triggered buttons, held-direction auto-repeat,
/// stick deadzone, and the recovery chord (View + Menu held for RecoveryHold) => RecoveryRequested.
public sealed class NavMapper
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(400);
    public TimeSpan RepeatInterval { get; init; } = TimeSpan.FromMilliseconds(120);
    public TimeSpan RecoveryHold { get; init; } = TimeSpan.FromSeconds(3);
    public double Deadzone { get; init; } = 0.5;

    PadButtons _prev;
    NavAction? _heldDir; DateTimeOffset _dirSince, _nextRepeat;
    DateTimeOffset? _chordSince; bool _chordFired;

    static readonly (PadButtons b, NavAction a)[] Edge =
    {
        (PadButtons.A, NavAction.Accept), (PadButtons.B, NavAction.Back), (PadButtons.Menu, NavAction.Menu),
        (PadButtons.View, NavAction.View), (PadButtons.LeftShoulder, NavAction.PageLeft), (PadButtons.RightShoulder, NavAction.PageRight),
    };

    public IReadOnlyList<NavAction> Update(PadState s, DateTimeOffset now)
    {
        var outp = new List<NavAction>();
        const PadButtons chord = PadButtons.View | PadButtons.Menu;
        bool chordHeld = (s.Buttons & chord) == chord;
        if (chordHeld)
        {
            _chordSince ??= now;
            if (!_chordFired && now - _chordSince >= RecoveryHold) { outp.Add(NavAction.RecoveryRequested); _chordFired = true; }
        }
        else { _chordSince = null; _chordFired = false; }

        if (!chordHeld)
            foreach (var (b, a) in Edge)
                if (s.Buttons.HasFlag(b) && !_prev.HasFlag(b) && !IsChordPartPressedWithOther(s.Buttons, b)) outp.Add(a);

        var dir = Direction(s);
        if (dir != _heldDir)
        {
            _heldDir = dir;
            if (dir is { } d) { outp.Add(d); _dirSince = now; _nextRepeat = now + InitialDelay; }
        }
        else if (dir is { } d && now >= _nextRepeat) { outp.Add(d); _nextRepeat = now + RepeatInterval; }

        _prev = s.Buttons;
        return outp;
    }

    static bool IsChordPartPressedWithOther(PadButtons cur, PadButtons b) =>
        (b == PadButtons.View && cur.HasFlag(PadButtons.Menu)) || (b == PadButtons.Menu && cur.HasFlag(PadButtons.View));

    NavAction? Direction(PadState s)
    {
        var b = s.Buttons;
        if (b.HasFlag(PadButtons.DPadUp) || s.LeftY > Deadzone) return NavAction.Up;
        if (b.HasFlag(PadButtons.DPadDown) || s.LeftY < -Deadzone) return NavAction.Down;
        if (b.HasFlag(PadButtons.DPadLeft) || s.LeftX < -Deadzone) return NavAction.Left;
        if (b.HasFlag(PadButtons.DPadRight) || s.LeftX > Deadzone) return NavAction.Right;
        return null;
    }
}
