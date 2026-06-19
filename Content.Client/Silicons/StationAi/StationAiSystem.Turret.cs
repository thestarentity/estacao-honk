using Content.Shared.Lock;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Turrets;
using Content.Shared.TurretController;
using Content.Shared.Weapons.Ranged.Components;
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
    /// Alt-clique na TORRETA: radial que controla SÓ esta torreta (ligar/desligar, modo de tiro e
    /// trancar/destrancar a própria torreta). O "estado de armamento" é derivado da torreta: desligada
    /// conta como -1; ligada usa o índice do modo de tiro atual
    /// (<see cref="BatteryWeaponFireModesComponent.CurrentFireMode"/>). Trava lê o LockComponent da torreta.
    /// </summary>
    private void OnTurretEntityGetRadial(Entity<DeployableTurretComponent> ent, ref GetStationAiRadialEvent args)
    {
        var state = -1;
        if (ent.Comp.Enabled && TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            state = fireModes.CurrentFireMode;

        BuildTurretRadial(ent.Owner, state, singleTurret: true, ref args);
    }

    /// <summary>
    /// Alt-clique no PAINEL de controle: radial que controla o GRUPO inteiro de torretas. O estado e a
    /// trava vêm do próprio painel.
    /// </summary>
    private void OnTurretGetRadial(Entity<DeployableTurretControllerComponent> ent, ref GetStationAiRadialEvent args)
    {
        BuildTurretRadial(ent.Owner, ent.Comp.ArmamentState, singleTurret: false, ref args);
    }

    /// <summary>
    /// Monta os botões do radial de torretas. <paramref name="lockOwner"/> é a entidade cujo
    /// <see cref="LockComponent"/> será trancado/destrancado (o painel no grupo, ou a própria torreta).
    /// <paramref name="singleTurret"/> só troca os textos (singular x grupo).
    /// </summary>
    private void BuildTurretRadial(EntityUid lockOwner, int state, bool singleTurret, ref GetStationAiRadialEvent args)
    {
        // Prefixo das chaves de texto: "ai-turret-single-" (uma torreta) ou "ai-turret-" (grupo do painel).
        var prefix = singleTurret ? "ai-turret-single-" : "ai-turret-";

        // Desligar (-1): usa o GENERAL TOGGLE, refletindo o estado (off quando é o modo ativo) — assim
        // o ícone muda ao ser clicado, mas continua sendo o toggle genérico (pedido do usuário).
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, state == -1 ? "turn_off" : "turn_on"),
            Tooltip = Loc.GetString(prefix + "off"),
            Event = new StationAiTurretArmamentEvent { Armament = -1 },
        });

        // Modos 0 (atordoar) e 1 (letal): ícone do modo ATIVO = "current_sentry_mode";
        // disponíveis mas inativos usam "not_being_utilized_sentry_mode".
        AddTurretArmament(args.Actions, state, 0, prefix + "stun");

        // Hostil (letal) só sob lei hostil (o servidor reconfirma).
        if (LocalAiIsHostile())
            AddTurretArmament(args.Actions, state, 1, prefix + "lethal");

        // Trancar/Destrancar (LockComponent) — qualquer lei. A IA fura o ID.
        if (TryComp<LockComponent>(lockOwner, out var lockComp))
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, lockComp.Locked ? "general_unlock" : "general_lock"),
                Tooltip = Loc.GetString(prefix + (lockComp.Locked ? "unlock" : "lock")),
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
