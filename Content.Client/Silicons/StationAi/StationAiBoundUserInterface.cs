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
        // CPU atual da IA local. Usada para anotar custo e cinzar o que não dá pra pagar.
        // O multiplicador encarece as ações da IA leal (a Malf paga 1x).
        float? cpu = null;
        var mult = 1f;
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        if (player != null && EntMan.TryGetComponent<StationAiCpuComponent>(player.Value, out var cpuComp))
        {
            cpu = cpuComp.Cpu;
            mult = cpuComp.CostMultiplier;
        }

        var models = new RadialMenuActionOptionBase[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var cost = action.Event.CpuCost * mult;

            var tooltip = action.Tooltip;
            Color? bg = null;
            if (cost > 0f && cpu != null)
            {
                var afford = cpu.Value >= cost;
                tooltip = $"{action.Tooltip} ({(int) cost} CPU)";
                if (!afford)
                {
                    tooltip = $"{action.Tooltip} ({(int) cost} CPU {Loc.GetString("station-ai-cpu-low")})";
                    bg = new Color(0.25f, 0.25f, 0.25f); // cinza: sem saldo (servidor nega ao clicar)
                }
            }

            models[i] = new RadialMenuActionOption<BaseStationAiAction>(HandleRadialMenuClick, action.Event)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(action.Sprite),
                ToolTip = tooltip,
                BackgroundColor = bg,
            };
        }

        return models;
    }

    private void HandleRadialMenuClick(BaseStationAiAction p)
    {
        SendPredictedMessage(new StationAiRadialMessage { Event = p });
    }
}
