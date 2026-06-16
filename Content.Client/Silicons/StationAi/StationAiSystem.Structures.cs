using Content.Shared.Doors.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private void InitializeStructures()
    {
        SubscribeLocalEvent<StationAiBlastDoorControllableComponent, GetStationAiRadialEvent>(OnBlastDoorGetRadial);
    }

    private void OnBlastDoorGetRadial(Entity<StationAiBlastDoorControllableComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Abrir/fechar comporta (toggle pelo estado da porta) — qualquer lei. Botão único.
        var open = !TryComp<DoorComponent>(ent.Owner, out var door) || door.State != DoorState.Closed;

        // Ícones próprios de porta: aberta → ação de fechar (general_close); fechada → abrir (general_open).
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, open ? "general_close" : "general_open"),
            Tooltip = Loc.GetString(open ? "ai-blastdoor-close" : "ai-blastdoor-open"),
            Event = new StationAiBlastDoorEvent { Close = open },
        });
    }
}
