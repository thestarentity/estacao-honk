using Content.Shared.Lock;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Turrets;
using Content.Shared.TurretController;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private void InitializeTurret()
    {
        // Radial no PAINEL de controle...
        SubscribeLocalEvent<DeployableTurretControllerComponent, GetStationAiRadialEvent>(OnTurretGetRadial);
        // ...e na TORRETA em si (delega ao painel ligado — mesmo menu).
        SubscribeLocalEvent<DeployableTurretComponent, GetStationAiRadialEvent>(OnTurretEntityGetRadial);
    }

    /// <summary>
    /// Alt-clique na TORRETA: monta o MESMO radial do painel, lendo o estado do painel ao qual ela
    /// está ligada (<see cref="DeployableTurretComponent.AiController"/>). As ações são roteadas para a
    /// torreta e o servidor as delega ao painel.
    /// </summary>
    private void OnTurretEntityGetRadial(Entity<DeployableTurretComponent> ent, ref GetStationAiRadialEvent args)
    {
        if (ent.Comp.AiController is not { } controller ||
            !TryComp<DeployableTurretControllerComponent>(controller, out var ctrl))
            return;

        BuildTurretRadial((controller, ctrl), ref args);
    }

    private void OnTurretGetRadial(Entity<DeployableTurretControllerComponent> ent, ref GetStationAiRadialEvent args)
    {
        BuildTurretRadial(ent, ref args);
    }

    private void BuildTurretRadial(Entity<DeployableTurretControllerComponent> ent, ref GetStationAiRadialEvent args)
    {
        var state = ent.Comp.ArmamentState;

        // Desligar (-1): usa o GENERAL TOGGLE, refletindo o estado (off quando é o modo ativo) — assim
        // o ícone muda ao ser clicado, mas continua sendo o toggle genérico (pedido do usuário).
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, state == -1 ? "turn_off" : "turn_on"),
            Tooltip = Loc.GetString("ai-turret-off"),
            Event = new StationAiTurretArmamentEvent { Armament = -1 },
        });

        // Modos 0 (atordoar) e 1 (letal): ícone do modo ATIVO = "current_sentry_mode";
        // disponíveis mas inativos usam "not_being_utilized_sentry_mode".
        AddTurretArmament(args.Actions, state, 0, "ai-turret-stun");

        // Hostil (letal) só sob lei hostil (o servidor reconfirma).
        if (LocalAiIsHostile())
            AddTurretArmament(args.Actions, state, 1, "ai-turret-lethal");

        // Trancar/Destrancar o painel de controle (LockComponent) — qualquer lei. A IA fura o ID.
        if (TryComp<LockComponent>(ent.Owner, out var lockComp))
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, lockComp.Locked ? "general_unlock" : "general_lock"),
                Tooltip = Loc.GetString(lockComp.Locked ? "ai-turret-unlock" : "ai-turret-lock"),
                Event = new StationAiTurretLockEvent { Lock = !lockComp.Locked },
            });
        }
    }

    private void AddTurretArmament(List<StationAiRadial> actions, int current, int armament, string locKey)
    {
        actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, current == armament ? "current_sentry_mode" : "not_being_utilized_sentry_mode"),
            Tooltip = Loc.GetString(locKey),
            Event = new StationAiTurretArmamentEvent { Armament = armament },
        });
    }
}
