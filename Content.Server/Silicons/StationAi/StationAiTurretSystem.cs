using Content.Server.TurretController;
using Content.Server.Turrets;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Turrets;
using Content.Shared.TurretController;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ações da IA de estação sobre torretas pelo MENU RADIAL (alt-clique). Funciona em dois alvos com
/// ESCOPOS DIFERENTES:
/// <list type="bullet">
/// <item>No PAINEL de controle (<see cref="DeployableTurretControllerComponent"/>): age sobre o GRUPO
/// inteiro de torretas ligadas — define o armamento de todas e tranca/destranca o painel.</item>
/// <item>Na TORRETA em si (<see cref="DeployableTurretComponent"/>): age SÓ naquela torreta — define o
/// armamento dela e tranca/destranca o <see cref="LockComponent"/> dela própria.</item>
/// </list>
/// A IA fura a checagem de acesso. Letal (hostil) só sob lawset hostil — gate reusa
/// <see cref="StationAiBulkDoorSystem.IsUserUnderHostileLaw"/>.
/// </summary>
public sealed partial class StationAiTurretSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationAiBulkDoorSystem _hostile = default!;
    [Dependency] private DeployableTurretControllerSystem _turretController = default!;
    [Dependency] private DeployableTurretSystem _turret = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private StationAiCpuSystem _cpu = default!;

    /// <summary>Armamento a partir do qual conta como "hostil" (letal) — só sob lawset hostil.</summary>
    private const int LethalArmament = 1;

    public override void Initialize()
    {
        base.Initialize();

        // Clicando no PAINEL de controle.
        SubscribeLocalEvent<DeployableTurretControllerComponent, StationAiTurretArmamentEvent>(OnArmament);
        SubscribeLocalEvent<DeployableTurretControllerComponent, StationAiTurretLockEvent>(OnLock);

        // Clicando na TORRETA em si: age SÓ naquela torreta.
        SubscribeLocalEvent<DeployableTurretComponent, StationAiTurretArmamentEvent>(OnTurretArmament);
        SubscribeLocalEvent<DeployableTurretComponent, StationAiTurretLockEvent>(OnTurretLock);
    }

    private void OnArmament(EntityUid uid, DeployableTurretControllerComponent comp, StationAiTurretArmamentEvent args)
    {
        HandleArmament((uid, comp), args, popupTarget: uid);
    }

    private void OnLock(EntityUid uid, DeployableTurretControllerComponent comp, StationAiTurretLockEvent args)
    {
        HandleLock(uid, args, popupTarget: uid);
    }

    /// <summary>Armamento clicado NA torreta: age SÓ naquela torreta (não no grupo do painel).</summary>
    private void OnTurretArmament(EntityUid uid, DeployableTurretComponent comp, StationAiTurretArmamentEvent args)
    {
        // Armamento letal (hostil) só sob lawset hostil. O cliente já esconde, o servidor reconfirma.
        if (args.Armament >= LethalArmament && !_hostile.IsUserUnderHostileLaw(args.User))
        {
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-turret-denied"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        _turret.SetArmament((uid, comp), args.Armament);

        _adminLogger.Add(LogType.ItemConfigure, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} definiu o armamento da torreta {ToPrettyString(uid):target} para {args.Armament} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-turret-single-set"), uid, args.User, PopupType.Medium);
    }

    /// <summary>Trancar/destrancar clicado NA torreta: age no <see cref="LockComponent"/> dela própria.</summary>
    private void OnTurretLock(EntityUid uid, DeployableTurretComponent comp, StationAiTurretLockEvent args)
    {
        HandleLock(uid, args, popupTarget: uid);
    }

    private void HandleArmament(Entity<DeployableTurretControllerComponent> ent, StationAiTurretArmamentEvent args, EntityUid popupTarget)
    {
        // Armamento letal (hostil) só sob lawset hostil. O cliente já esconde, o servidor reconfirma.
        if (args.Armament >= LethalArmament && !_hostile.IsUserUnderHostileLaw(args.User))
        {
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-turret-denied"), popupTarget, args.User, PopupType.MediumCaution);
            return;
        }

        _turretController.SetArmamentFromAi(ent, args.Armament, args.User);

        _adminLogger.Add(LogType.ItemConfigure, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} definiu o armamento das torretas de {ToPrettyString(ent.Owner):target} para {args.Armament} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-turret-set"), popupTarget, args.User, PopupType.Medium);
    }

    /// <summary>
    /// Tranca/destranca o <see cref="LockComponent"/> de <paramref name="target"/> — pode ser o painel
    /// (grupo) ou uma torreta individual. A IA fura o ID e a exigência de painel: chama Lock/Unlock crus.
    /// </summary>
    private void HandleLock(EntityUid target, StationAiTurretLockEvent args, EntityUid popupTarget)
    {
        if (!TryComp<LockComponent>(target, out var lockComp))
            return;

        if (args.Lock)
            _lock.Lock(target, args.User, lockComp);
        else
            _lock.Unlock(target, args.User, lockComp);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Lock ? "trancou" : "destrancou")} {ToPrettyString(target):target} pela IA de estação.");
    }
}
