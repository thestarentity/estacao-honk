using Content.Client.Shuttles.UI;
using Content.Client.Silicons.StationAi;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Silicons.StationAi;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using RadarConsoleWindow = Content.Client.Shuttles.UI.RadarConsoleWindow;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed class RadarConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RadarConsoleWindow? _window;

    public RadarConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RadarConsoleWindow>();

        // Fork: se este radar pertence à IA (o Owner é o olho/held da IA), clicar move o olho e fecha.
        if (EntMan.HasComponent<StationAiHeldComponent>(Owner))
        {
            _window.OnRadarClick = coords =>
            {
                EntMan.System<StationAiSystem>().MoveEyeTo(coords);
                Close();
            };
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not NavBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(cState.State);
    }
}
