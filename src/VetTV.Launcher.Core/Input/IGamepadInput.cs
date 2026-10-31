namespace VetTV.Launcher.Core.Input;
public enum NavAction { Up, Down, Left, Right, Accept, Back, Menu, View, Guide, PageLeft, PageRight, RecoveryRequested }
[Flags] public enum PadButtons
{
    None = 0, A = 1, B = 2, X = 4, Y = 8, DPadUp = 16, DPadDown = 32, DPadLeft = 64, DPadRight = 128,
    Menu = 256, View = 512, LeftShoulder = 1024, RightShoulder = 2048
}
/// One poll of the pad, platform-neutral. Sticks are -1..1.
public readonly record struct PadState(PadButtons Buttons, double LeftX = 0, double LeftY = 0);
/// Implemented in the app via Windows.Gaming.Input; keyboard maps to the same actions.
public interface IGamepadInput { event EventHandler<NavAction>? Action; void Start(); void Stop(); }
