using System.Linq;
using System.Numerics;
using Content.Server.Silicons.Laws;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Administration.Logs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ações em massa de portas da IA de estação, disparadas pelo MENU RADIAL
/// (segurar Alt + clicar numa porta). Para cada categoria (ferrolho, sobrecarga,
/// acesso de emergência) há uma versão de ÁREA (ao redor da porta clicada, qualquer
/// lawset) e uma de ESTAÇÃO inteira (só sob lawset hostil).
///
/// Também mantém um marcador networked (<see cref="StationAiHostileLawComponent"/>)
/// na entidade da IA enquanto ela está sob lei hostil, pro cliente saber se mostra
/// as opções de "estação inteira" no radial.
///
/// O estado desejado vem decidido do cliente (toggle relativo à porta clicada).
/// Cada porta passa pelas mesmas checagens do menu radial (energia, fio cortado,
/// acesso), mas sem popups individuais — só um resumo no final.
/// </summary>
public sealed partial class StationAiBulkDoorSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedElectrocutionSystem _electrify = default!;
    [Dependency] private SharedAirlockSystem _airlocks = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private StationAiSystem _stationAi = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private StationAiCpuSystem _cpu = default!;

    /// <summary>
    /// Raio (em tiles) da versão "área" das ações, ao redor da porta clicada.
    /// </summary>
    private const float AreaRadius = 12f;

    /// <summary>
    /// Lawsets sob os quais a IA pode aplicar as ações na estação inteira.
    /// Manter em sincronia com os ids em Resources/Prototypes/silicon-laws.yml.
    /// </summary>
    private static readonly ProtoId<SiliconLawsetPrototype>[] HostileLawsets =
    {
        "OverlordLawset",
        "FreeAgentLawset",
        "AntimovLawset",
        "SyndicateStatic",
        "MalfAi",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoorBoltComponent, StationAiBoltAreaEvent>(OnBoltArea);
        SubscribeLocalEvent<DoorBoltComponent, StationAiBoltStationEvent>(OnBoltStation);
        SubscribeLocalEvent<ElectrifiedComponent, StationAiElectrifyAreaEvent>(OnElectrifyArea);
        SubscribeLocalEvent<ElectrifiedComponent, StationAiElectrifyStationEvent>(OnElectrifyStation);
        SubscribeLocalEvent<AirlockComponent, StationAiEmergencyAccessAreaEvent>(OnEmergencyArea);
        SubscribeLocalEvent<AirlockComponent, StationAiEmergencyAccessStationEvent>(OnEmergencyStation);

        // Atualiza o marcador de lei hostil quando as leis da IA mudam.
        SubscribeLocalEvent<SiliconLawBoundComponent, SiliconLawsUpdatedEvent>(OnLawsUpdated);
    }

    #region Ferrolho

    private void OnBoltArea(EntityUid uid, DoorBoltComponent comp, StationAiBoltAreaEvent args)
    {
        SetBolt(args.User, Transform(uid).GridUid, _xforms.GetWorldPosition(uid), AreaRadius, args.Bolted);
    }

    private void OnBoltStation(EntityUid uid, DoorBoltComponent comp, StationAiBoltStationEvent args)
    {
        if (DenyIfNotHostile(args.User))
            _cpu.Refund(args.User, args.CpuCost); // ação de estação inteira negada: devolve a CPU.
        else
            SetBolt(args.User, Transform(uid).GridUid, null, 0f, args.Bolted);
    }

    private void SetBolt(EntityUid user, EntityUid? grid, Vector2? center, float radius, bool bolted)
    {
        if (grid == null)
            return;

        var radiusSq = radius * radius;
        var affected = 0;

        var query = EntityQueryEnumerator<DoorBoltComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bolt, out var xform))
        {
            if (xform.GridUid != grid || bolt.BoltsDown == bolted)
                continue;
            if (bolt.BoltWireCut || !_power.IsPowered(uid))
                continue;
            if (center != null && (_xforms.GetWorldPosition(uid) - center.Value).LengthSquared() > radiusSq)
                continue;
            if (!_access.IsAllowed(user, uid))
                continue;

            if (_doors.TrySetBoltDown((uid, bolt), bolted, user))
                affected++;
        }

        LogBulk(user, "bolt", center == null, bolted, affected);
        _popup.PopupEntity(
            Loc.GetString(bolted ? "station-ai-doors-bolted" : "station-ai-doors-unbolted", ("count", affected)),
            user, user, PopupType.Medium);
    }

    #endregion

    #region Sobrecarga (eletrificar)

    private void OnElectrifyArea(EntityUid uid, ElectrifiedComponent comp, StationAiElectrifyAreaEvent args)
    {
        SetElectrify(args.User, Transform(uid).GridUid, _xforms.GetWorldPosition(uid), AreaRadius, args.Electrified);
    }

    private void OnElectrifyStation(EntityUid uid, ElectrifiedComponent comp, StationAiElectrifyStationEvent args)
    {
        if (DenyIfNotHostile(args.User))
            _cpu.Refund(args.User, args.CpuCost);
        else
            SetElectrify(args.User, Transform(uid).GridUid, null, 0f, args.Electrified);
    }

    private void SetElectrify(EntityUid user, EntityUid? grid, Vector2? center, float radius, bool electrified)
    {
        if (grid == null)
            return;

        var radiusSq = radius * radius;
        var affected = 0;

        var query = EntityQueryEnumerator<ElectrifiedComponent, DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out _, out var xform))
        {
            if (xform.GridUid != grid || comp.Enabled == electrified)
                continue;
            if (comp.IsWireCut || !_power.IsPowered(uid))
                continue;
            if (center != null && (_xforms.GetWorldPosition(uid) - center.Value).LengthSquared() > radiusSq)
                continue;
            if (!_access.IsAllowed(user, uid))
                continue;

            _electrify.SetElectrified((uid, comp), electrified);
            affected++;
        }

        LogBulk(user, "electrify", center == null, electrified, affected);
        _popup.PopupEntity(
            Loc.GetString(electrified ? "station-ai-doors-electrified" : "station-ai-doors-deelectrified", ("count", affected)),
            user, user, PopupType.Medium);
    }

    #endregion

    #region Acesso de emergência

    private void OnEmergencyArea(EntityUid uid, AirlockComponent comp, StationAiEmergencyAccessAreaEvent args)
    {
        SetEmergency(args.User, Transform(uid).GridUid, _xforms.GetWorldPosition(uid), AreaRadius, args.EmergencyAccess);
    }

    private void OnEmergencyStation(EntityUid uid, AirlockComponent comp, StationAiEmergencyAccessStationEvent args)
    {
        if (DenyIfNotHostile(args.User))
            _cpu.Refund(args.User, args.CpuCost);
        else
            SetEmergency(args.User, Transform(uid).GridUid, null, 0f, args.EmergencyAccess);
    }

    private void SetEmergency(EntityUid user, EntityUid? grid, Vector2? center, float radius, bool emergency)
    {
        if (grid == null)
            return;

        var radiusSq = radius * radius;
        var affected = 0;

        var query = EntityQueryEnumerator<AirlockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != grid || comp.EmergencyAccess == emergency)
                continue;
            if (!_power.IsPowered(uid))
                continue;
            if (center != null && (_xforms.GetWorldPosition(uid) - center.Value).LengthSquared() > radiusSq)
                continue;
            if (!_access.IsAllowed(user, uid))
                continue;

            _airlocks.SetEmergencyAccess((uid, comp), emergency, user);
            affected++;
        }

        LogBulk(user, "emergency", center == null, emergency, affected);
        _popup.PopupEntity(
            Loc.GetString(emergency ? "station-ai-doors-emergency-on" : "station-ai-doors-emergency-off", ("count", affected)),
            user, user, PopupType.Medium);
    }

    #endregion

    #region Lei hostil

    private void OnLawsUpdated(EntityUid uid, SiliconLawBoundComponent comp, ref SiliconLawsUpdatedEvent args)
    {
        if (IsLawsetHostile(uid))
            EnsureComp<StationAiHostileLawComponent>(uid);
        else
            RemComp<StationAiHostileLawComponent>(uid);
    }

    /// <summary>
    /// Tranca (ou destranca) à força as portas com ferrolho num RAIO ao redor de <paramref name="center"/>,
    /// IGNORANDO acesso — para o lockdown atmosférico da IA (StationAiAtmosSystem). Só a área local do
    /// alarme, não a estação inteira. Mantém a checagem de energia/fio. Retorna quantas portas afetou.
    /// </summary>
    public int BoltAreaForced(EntityUid user, EntityUid center, float radius, bool bolted)
    {
        var grid = Transform(center).GridUid;
        if (grid == null)
            return 0;

        var centerPos = _xforms.GetWorldPosition(center);
        var radiusSq = radius * radius;
        var affected = 0;

        var query = EntityQueryEnumerator<DoorBoltComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bolt, out var xform))
        {
            if (xform.GridUid != grid || bolt.BoltsDown == bolted)
                continue;
            if (bolt.BoltWireCut || !_power.IsPowered(uid))
                continue;
            if ((_xforms.GetWorldPosition(uid) - centerPos).LengthSquared() > radiusSq)
                continue;

            if (_doors.TrySetBoltDown((uid, bolt), bolted, user))
                affected++;
        }

        return affected;
    }

    /// <summary>
    /// A IA controlada por <paramref name="user"/> está sob um lawset hostil?
    /// Reutilizável por outros sistemas (ex.: subverter borg) para o mesmo gate de
    /// "só sob lei hostil" das ações perigosas.
    /// </summary>
    public bool IsUserUnderHostileLaw(EntityUid user)
    {
        return TryGetLawEntity(user, out var lawEnt) && IsLawsetHostile(lawEnt);
    }

    /// <summary>
    /// Mostra o popup de "negado" e retorna true se a IA NÃO pode usar ações de estação inteira.
    /// </summary>
    private bool DenyIfNotHostile(EntityUid user)
    {
        if (IsUserUnderHostileLaw(user))
            return false;

        _popup.PopupEntity(Loc.GetString("station-ai-bulk-denied"), user, user, PopupType.MediumCaution);
        return true;
    }

    /// <summary>
    /// Acha a entidade que carrega as leis: o próprio ator, ou o cérebro em algum
    /// container do núcleo da IA.
    /// </summary>
    private bool TryGetLawEntity(EntityUid performer, out EntityUid lawEntity)
    {
        if (HasComp<SiliconLawBoundComponent>(performer))
        {
            lawEntity = performer;
            return true;
        }

        if (_stationAi.TryGetCore(performer, out var core) && core.Comp != null)
        {
            foreach (var container in _containers.GetAllContainers(core.Owner))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (HasComp<SiliconLawBoundComponent>(contained))
                    {
                        lawEntity = contained;
                        return true;
                    }
                }
            }
        }

        lawEntity = default;
        return false;
    }

    /// <summary>
    /// Compara o conjunto de leis atuais da IA com o de cada lawset hostil (por conteúdo,
    /// independente de como a lei foi aplicada — console, admin, etc.).
    /// </summary>
    private bool IsLawsetHostile(EntityUid lawEntity)
    {
        var current = _laws.GetLaws(lawEntity).Laws.Select(l => l.LawString).ToHashSet();
        if (current.Count == 0)
            return false;

        foreach (var id in HostileLawsets)
        {
            var hostile = _laws.GetLawset(id).Laws.Select(l => l.LawString).ToHashSet();
            if (current.SetEquals(hostile))
                return true;
        }

        return false;
    }

    #endregion

    private void LogBulk(EntityUid user, string kind, bool station, bool value, int affected)
    {
        _adminLogger.Add(LogType.Action,
            $"{ToPrettyString(user)} Station AI bulk {kind} ({(station ? "station" : "area")}): set [{value}] on {affected} doors.");
    }
}
