using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private readonly ResPath _aiActionsRsi = new ResPath("/Textures/Interface/Actions/actions_ai.rsi");

    private void InitializeAirlock()
    {
        SubscribeLocalEvent<DoorBoltComponent, GetStationAiRadialEvent>(OnDoorBoltGetRadial);
        SubscribeLocalEvent<AirlockComponent, GetStationAiRadialEvent>(OnEmergencyAccessGetRadial);
        SubscribeLocalEvent<ElectrifiedComponent, GetStationAiRadialEvent>(OnDoorElectrifiedGetRadial);
    }

    private SpriteSpecifier.Rsi Icon(string state) => new(_aiActionsRsi, state);

    /// <summary>
    /// A IA local está sob lei hostil? Só então as opções de "estação inteira" aparecem.
    /// </summary>
    private bool LocalAiIsHostile()
    {
        return _player.LocalEntity is { } ent && HasComp<StationAiHostileLawComponent>(ent);
    }

    /// <summary>
    /// A IA local está PILOTANDO um borg (Fase 5A)? Nesse caso o radial fica com ACESSO LIMITADO:
    /// só ações individuais/seguras (nada de área ou estação inteira).
    /// </summary>
    private bool LocalAiIsPiloting()
    {
        return _player.LocalEntity is { } ent && HasComp<StationAiPilotingComponent>(ent);
    }

    private void OnDoorBoltGetRadial(Entity<DoorBoltComponent> ent, ref GetStationAiRadialEvent args)
    {
        var willBolt = !ent.Comp.BoltsDown;

        // Porta individual (safe).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(willBolt ? "bolt_door_safe" : "unbolt_door_safe"),
                Tooltip = Loc.GetString(ent.Comp.BoltsDown ? "bolt-open" : "bolt-close"),
                Event = new StationAiBoltEvent { Bolted = willBolt },
            }
        );

        // Pilotando um borg: acesso limitado — só a ação individual acima.
        if (LocalAiIsPiloting())
            return;

        // Área (cautious).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(willBolt ? "bolt_door_cautious" : "unbolt_door_cautious"),
                Tooltip = Loc.GetString(willBolt ? "bolt-area" : "unbolt-area"),
                Event = new StationAiBoltAreaEvent { Bolted = willBolt },
            }
        );

        // Estação inteira (dangereous) — só sob lei hostil.
        if (LocalAiIsHostile())
        {
            args.Actions.Add(
                new StationAiRadial
                {
                    Sprite = Icon(willBolt ? "bolt_door_dangereous" : "unbolt_door_dangereous"),
                    Tooltip = Loc.GetString(willBolt ? "bolt-station" : "unbolt-station"),
                    Event = new StationAiBoltStationEvent { Bolted = willBolt },
                }
            );
        }
    }

    private void OnEmergencyAccessGetRadial(Entity<AirlockComponent> ent, ref GetStationAiRadialEvent args)
    {
        var willEnable = !ent.Comp.EmergencyAccess;

        // Porta individual (safe).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(ent.Comp.EmergencyAccess ? "emergency_off_safe" : "emergency_on_safe"),
                Tooltip = Loc.GetString(ent.Comp.EmergencyAccess ? "emergency-access-off" : "emergency-access-on"),
                Event = new StationAiEmergencyAccessEvent { EmergencyAccess = willEnable },
            }
        );

        // Pilotando um borg: acesso limitado — só a ação individual acima.
        if (LocalAiIsPiloting())
            return;

        // Área (cautious).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(willEnable ? "emergency_on_cautious" : "emergency_off_cautious"),
                Tooltip = Loc.GetString(willEnable ? "emergency-access-area-on" : "emergency-access-area-off"),
                Event = new StationAiEmergencyAccessAreaEvent { EmergencyAccess = willEnable },
            }
        );

        // Estação inteira (dangereous) — só sob lei hostil.
        if (LocalAiIsHostile())
        {
            args.Actions.Add(
                new StationAiRadial
                {
                    Sprite = Icon(willEnable ? "emergency_on_dangereous" : "emergency_off_dangereous"),
                    Tooltip = Loc.GetString(willEnable ? "emergency-access-station-on" : "emergency-access-station-off"),
                    Event = new StationAiEmergencyAccessStationEvent { EmergencyAccess = willEnable },
                }
            );
        }
    }

    private void OnDoorElectrifiedGetRadial(Entity<ElectrifiedComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Só portas devem mostrar as ações de eletrificar. Outras entidades com ElectrifiedComponent
        // (ex.: a APC, que dá choque ao ser hackeada) não devem ganhar esses botões no radial.
        if (!HasComp<DoorComponent>(ent.Owner))
            return;

        var willElectrify = !ent.Comp.Enabled;

        // Porta individual (safe).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(ent.Comp.Enabled ? "door_overcharge_off_safe" : "door_overcharge_on_safe"),
                Tooltip = Loc.GetString(ent.Comp.Enabled ? "electrify-door-off" : "electrify-door-on"),
                Event = new StationAiElectrifiedEvent { Electrified = willElectrify },
            }
        );

        // Pilotando um borg: acesso limitado — só a ação individual acima.
        if (LocalAiIsPiloting())
            return;

        // Área (cautious).
        args.Actions.Add(
            new StationAiRadial
            {
                Sprite = Icon(willElectrify ? "door_overcharge_on_cautious" : "door_overcharge_off_cautious"),
                Tooltip = Loc.GetString(willElectrify ? "electrify-area" : "electrify-area-off"),
                Event = new StationAiElectrifyAreaEvent { Electrified = willElectrify },
            }
        );

        // Estação inteira (dangereous) — só sob lei hostil.
        if (LocalAiIsHostile())
        {
            args.Actions.Add(
                new StationAiRadial
                {
                    Sprite = Icon(willElectrify ? "door_overcharge_on_dangereous" : "door_overcharge_off_dangereous"),
                    Tooltip = Loc.GetString(willElectrify ? "electrify-station" : "electrify-station-off"),
                    Event = new StationAiElectrifyStationEvent { Electrified = willElectrify },
                }
            );
        }
    }
}
