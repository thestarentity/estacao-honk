using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Doors.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ações de ESTRUTURAS da IA de estação pelo MENU RADIAL (alt-clique numa comporta/persiana —
/// blast door / shutter da base <c>BaseShutter</c>). Abre/fecha a comporta direto, igual ao
/// firelock, disponível sob qualquer lei (não é letal). A IA fura o controle de acesso da
/// comporta passando <c>user: null</c> ao sistema de portas — que pula a checagem de acesso
/// (SharedDoorSystem.HasAccess: "if there is no user we skip the access checks").
/// </summary>
public sealed partial class StationAiStructureSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiBlastDoorControllableComponent, StationAiBlastDoorEvent>(OnBlastDoor);
    }

    private void OnBlastDoor(EntityUid uid, StationAiBlastDoorControllableComponent comp, StationAiBlastDoorEvent args)
    {
        // user: null fura o AccessReader da comporta (a IA comanda como no SS13). O log usa a IA real.
        if (args.Close)
            _door.TryClose(uid, user: null);
        else
            _door.TryOpen(uid, user: null);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Close ? "fechou" : "abriu")} a comporta {ToPrettyString(uid):target} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString(args.Close ? "station-ai-blastdoor-closed" : "station-ai-blastdoor-opened"), uid, args.User, PopupType.Medium);
    }
}
