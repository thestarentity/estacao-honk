using Content.Client.UserInterface.Controls;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;

namespace Content.Client.Silicons.StationAi;

public sealed class StationAiBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        var ev = new GetStationAiRadialEvent();
        EntMan.EventBus.RaiseLocalEvent(Owner, ref ev);

        _menu = this.CreateWindow<SimpleRadialMenu>();
        var buttonModels = ConvertToButtons(ev.Actions);
        _menu.SetButtons(buttonModels);

        // Pilotando um borg (Fase 5A): a IA fica LONGE da máquina, então seguir a máquina (Track) faria
        // o menu se fechar sozinho quando ela sai da tela (o flicker que o usuário via de longe). Nesse
        // caso abrimos numa posição fixa (do mouse), estável. A IA no núcleo segue a máquina como antes.
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        if (player != null && EntMan.HasComponent<StationAiPilotingComponent>(player.Value))
        {
            _menu.OpenOverMouseScreenPosition();
        }
        else
        {
            _menu.Track(Owner);
            _menu.Open();
        }
    }

    private IEnumerable<RadialMenuActionOptionBase> ConvertToButtons(IReadOnlyList<StationAiRadial> actions)
    {
        var models = new RadialMenuActionOptionBase[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            models[i] = new RadialMenuActionOption<BaseStationAiAction>(HandleRadialMenuClick, action.Event)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(action.Sprite),
                ToolTip = action.Tooltip
            };
        }

        return models;
    }

    private void HandleRadialMenuClick(BaseStationAiAction p)
    {
        SendPredictedMessage(new StationAiRadialMessage { Event = p });
    }
}
