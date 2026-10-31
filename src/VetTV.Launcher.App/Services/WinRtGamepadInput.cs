using Microsoft.UI.Dispatching;
using Windows.Gaming.Input;
using VetTV.Launcher.Core.Input;
namespace VetTV.Launcher.App.Services;
/// Polls Windows.Gaming.Input at ~60 Hz on the UI thread and feeds NavMapper.
public sealed class WinRtGamepadInput : IGamepadInput
{
    readonly NavMapper _mapper = new();
    readonly DispatcherQueueTimer _timer;
    public event EventHandler<NavAction>? Action;
    public WinRtGamepadInput(DispatcherQueue q)
    {
        _timer = q.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => Poll();
    }
    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
    void Poll()
    {
        var pad = Gamepad.Gamepads.FirstOrDefault();
        if (pad is null) return;
        var r = pad.GetCurrentReading();
        var b = PadButtons.None;
        void M(GamepadButtons g, PadButtons p) { if (r.Buttons.HasFlag(g)) b |= p; }
        M(GamepadButtons.A, PadButtons.A); M(GamepadButtons.B, PadButtons.B); M(GamepadButtons.X, PadButtons.X); M(GamepadButtons.Y, PadButtons.Y);
        M(GamepadButtons.DPadUp, PadButtons.DPadUp); M(GamepadButtons.DPadDown, PadButtons.DPadDown);
        M(GamepadButtons.DPadLeft, PadButtons.DPadLeft); M(GamepadButtons.DPadRight, PadButtons.DPadRight);
        M(GamepadButtons.Menu, PadButtons.Menu); M(GamepadButtons.View, PadButtons.View);
        M(GamepadButtons.LeftShoulder, PadButtons.LeftShoulder); M(GamepadButtons.RightShoulder, PadButtons.RightShoulder);
        foreach (var a in _mapper.Update(new PadState(b, r.LeftThumbstickX, r.LeftThumbstickY), DateTimeOffset.Now))
            Action?.Invoke(this, a);
    }
}
