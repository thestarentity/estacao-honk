using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private void InitializeAtmos()
    {
        SubscribeLocalEvent<StationAiAirAlarmControllableComponent, GetStationAiRadialEvent>(OnAirAlarmGetRadial);
        SubscribeLocalEvent<FirelockComponent, GetStationAiRadialEvent>(OnFirelockGetRadial);
        SubscribeLocalEvent<StationAiFireAlarmControllableComponent, GetStationAiRadialEvent>(OnFireAlarmGetRadial);
    }

    private void OnFireAlarmGetRadial(Entity<StationAiFireAlarmControllableComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Disparar firelocks (fecha a área) e Resetar (reabre) — qualquer lei. Botões fixos.
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "turn_on_firealarm"),
            Tooltip = Loc.GetString("ai-firelocks-trigger"),
            Event = new StationAiFireAlarmEvent { Alert = true },
        });

        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "turn_off_firealarm"),
            Tooltip = Loc.GetString("ai-firelocks-reset"),
            Event = new StationAiFireAlarmEvent { Alert = false },
        });
    }

    private void OnFirelockGetRadial(Entity<FirelockComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Fechar/abrir firelock (toggle pelo estado da porta) — qualquer lei. Ícones próprios de porta.
        var open = !TryComp<DoorComponent>(ent.Owner, out var door) || door.State != DoorState.Closed;

        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, open ? "general_close" : "general_open"),
            Tooltip = Loc.GetString(open ? "ai-firelock-close" : "ai-firelock-open"),
            Event = new StationAiFirelockEvent { Close = open },
        });
    }

    private void OnAirAlarmGetRadial(Entity<StationAiAirAlarmControllableComponent> ent, ref GetStationAiRadialEvent args)
    {
        var mode = ent.Comp.CurrentMode;

        // Modos: o ícone reflete o estado (versão "_on" quando aquele modo está ativo).
        AddMode(args.Actions, mode, AirAlarmMode.Filtering, "airfilter", "ai-atmos-filter");
        AddMode(args.Actions, mode, AirAlarmMode.Fill, "airadd", "ai-atmos-fill");

        // Esvaziar/pânico (vácuo inescapável: suga o ar + tranca as airlocks; persiste até filtrar) — só lei hostil.
        if (LocalAiIsHostile())
            AddMode(args.Actions, mode, AirAlarmMode.Panic, "airdepravation", "ai-atmos-panic");
    }

    private void AddMode(List<StationAiRadial> actions, AirAlarmMode current, AirAlarmMode mode, string stateBase, string locKey)
    {
        actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, current == mode ? $"{stateBase}_on" : $"{stateBase}_off"),
            Tooltip = Loc.GetString(locKey),
            Event = new StationAiAirAlarmModeEvent { Mode = mode },
        });
    }
}
