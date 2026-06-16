using Content.Server.TurretController;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Turrets;
using Content.Shared.TurretController;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ações da IA de estação sobre torretas pelo MENU RADIAL (alt-clique). Funciona tanto no PAINEL de
/// controle (<see cref="DeployableTurretControllerComponent"/>) quanto na TORRETA em si
/// (<see cref="DeployableTurretComponent"/>) — neste caso a ação é delegada ao painel ao qual a
/// torreta está ligada (<see cref="DeployableTurretComponent.AiController"/>). Define o armamento de
/// todas as torretas ligadas (desligar/atordoar/letal) e tranca/destranca o painel. A IA fura a
/// checagem de acesso. Letal (hostil) só sob lawset hostil — gate reusa
/// <see cref="StationAiBulkDoorSystem.IsUserUnderHostileLaw"/>.
/// </summary>
public sealed partial class StationAiTurretSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationAiBulkDoorSystem _hostile = default!;
    [Dependency] private DeployableTurretControllerSystem _turretController = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    /// <summary>Armamento a partir do qual conta como "hostil" (letal) — só sob lawset hostil.</summary>
    private const int LethalArmament = 1;

    public override void Initialize()
    {
        base.Initialize();

        // Clicando no PAINEL de controle.
        SubscribeLocalEvent<DeployableTurretControllerComponent, StationAiTurretArmamentEvent>(OnArmament);
        SubscribeLocalEvent<DeployableTurretControllerComponent, StationAiTurretLockEvent>(OnLock);

        // Clicando na TORRETA em si: delega ao painel ligado.
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

    private void OnTurretArmament(EntityUid uid, DeployableTurretComponent comp, StationAiTurretArmamentEvent args)
    {
        if (comp.AiController is not { } controller || !TryComp<DeployableTurretControllerComponent>(controller, out var ctrl))
            return;

        // Popup aparece NA torreta clicada (onde a IA olhou), mas a ação age no painel/grupo todo.
        HandleArmament((controller, ctrl), args, popupTarget: uid);
    }

    private void OnTurretLock(EntityUid uid, DeployableTurretComponent comp, StationAiTurretLockEvent args)
    {
        if (comp.AiController is not { } controller)
            return;

        HandleLock(controller, args, popupTarget: uid);
    }

    private void HandleArmament(Entity<DeployableTurretControllerComponent> ent, StationAiTurretArmamentEvent args, EntityUid popupTarget)
    {
        // Armamento letal (hostil) só sob lawset hostil. O cliente já esconde, o servidor reconfirma.
        if (args.Armament >= LethalArmament && !_hostile.IsUserUnderHostileLaw(args.User))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-turret-denied"), popupTarget, args.User, PopupType.MediumCaution);
            return;
        }

        _turretController.SetArmamentFromAi(ent, args.Armament, args.User);

        _adminLogger.Add(LogType.ItemConfigure, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} definiu o armamento das torretas de {ToPrettyString(ent.Owner):target} para {args.Armament} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-turret-set"), popupTarget, args.User, PopupType.Medium);
    }

    private void HandleLock(EntityUid controller, StationAiTurretLockEvent args, EntityUid popupTarget)
    {
        if (!TryComp<LockComponent>(controller, out var lockComp))
            return;

        // A IA fura o ID e a exigência de painel: chama Lock/Unlock crus.
        if (args.Lock)
            _lock.Lock(controller, args.User, lockComp);
        else
            _lock.Unlock(controller, args.User, lockComp);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Lock ? "trancou" : "destrancou")} o painel de torretas {ToPrettyString(controller):target} pela IA de estação.");
    }
}
