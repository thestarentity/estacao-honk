using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Core shunting da IA Malf (Fase 3, Bloco 2). Move o cérebro da IA do container do núcleo
/// (station_ai_mind_slot) para um container na APC hackeada (station_ai_shunt_slot). Sair do
/// container do núcleo já dispara OnAiRemove (olho some, núcleo vazio); voltar dispara OnAiInsert.
/// Enquanto shuntada a IA é dormente (StationAiShuntedComponent bloqueia o radial em
/// SharedStationAiSystem.OnRadialMessage). Sobrevive à destruição do núcleo; morre com a APC.
/// </summary>
public sealed partial class StationAiShuntSystem : EntitySystem
{
    public const string ShuntContainer = "station_ai_shunt_slot";

    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    /// <summary>Protótipo de ação concedida ao cérebro para voltar ao núcleo. Definido no YAML da Task 6.</summary>
    private static readonly EntProtoId ReturnActionProto = "ActionStationAiReturnFromShunt";

    public override void Initialize()
    {
        base.Initialize();

        // Ação no radial da APC: shuntar.
        SubscribeLocalEvent<StationAiApcControllableComponent, StationAiApcShuntEvent>(OnShuntRequest);
        // Ação instantânea concedida ao cérebro: voltar ao núcleo.
        SubscribeLocalEvent<StationAiShuntedComponent, StationAiReturnFromShuntEvent>(OnReturnRequest);
    }

    private void OnShuntRequest(EntityUid apc, StationAiApcControllableComponent comp, StationAiApcShuntEvent args)
    {
        var brain = args.User; // a entidade-cérebro (= ev.Actor, dona de leis/CPU)
        TryShunt(brain, apc);
    }

    private void OnReturnRequest(EntityUid brain, StationAiShuntedComponent comp, StationAiReturnFromShuntEvent args)
    {
        if (comp.CoreLost)
            return; // núcleo destruído: não há para onde voltar.
        ReturnToCore(brain);
    }

    /// <summary>
    /// Move o cérebro para dentro da APC. Pré: APC hackeada e não ocupada; saldo de CPU.
    /// O custo de CPU já foi cobrado em OnRadialMessage (CpuCost=50) ANTES deste handler —
    /// por isso aqui NÃO chamamos TryConsume de novo. Validamos só os pré-requisitos de estado.
    /// </summary>
    public bool TryShunt(EntityUid brain, EntityUid apc)
    {
        if (!TryComp<StationAiApcControllableComponent>(apc, out var apcComp))
            return false;

        if (!apcComp.Hacked)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-shunt-apc-not-hacked"), apc, brain, PopupType.MediumCaution);
            return false;
        }

        if (apcComp.Occupied || HasComp<StationAiShuntedComponent>(brain))
            return false;

        // Tira o cérebro do container do núcleo (dispara OnAiRemove: olho some, núcleo "vazio").
        // TryGetCore só retorna true quando o cérebro está DENTRO do núcleo (container station_ai_mind_slot).
        if (!_stationAi.TryGetCore(brain, out var core) || core.Comp == null)
            return false;

        // Grava o núcleo de origem antes de mover o cérebro (após o Insert, TryGetCore retorna false).
        var ret = EnsureComp<StationAiShuntReturnComponent>(brain);
        ret.Core = core.Owner;

        var shuntSlot = _container.EnsureContainer<ContainerSlot>(apc, ShuntContainer);
        if (!_container.Insert(brain, shuntSlot)) // Insert remove do container antigo automaticamente
        {
            RemComp<StationAiShuntReturnComponent>(brain);
            return false;
        }

        var shunted = EnsureComp<StationAiShuntedComponent>(brain);
        shunted.HostApc = apc;
        Dirty(brain, shunted);

        apcComp.Occupied = true;
        Dirty(apc, apcComp);
        _stationAi.SetApcOccupiedVisual(apc, true);

        // Concede a ação de voltar ao núcleo e guarda a entity para poder remover depois.
        EntityUid? returnAction = null;
        _actions.AddAction(brain, ref returnAction, ReturnActionProto);
        ret.ReturnAction = returnAction;

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(brain):user} shuntou para a APC {ToPrettyString(apc):target}.");
        _popup.PopupEntity(Loc.GetString("station-ai-shunt-done"), brain, brain, PopupType.Medium);
        return true;
    }

    /// <summary>Volta o cérebro ao container do núcleo (dispara OnAiInsert: olho religado).</summary>
    public void ReturnToCore(EntityUid brain)
    {
        if (!TryComp<StationAiShuntedComponent>(brain, out var shunted) || shunted.HostApc == null)
            return;

        var apc = shunted.HostApc.Value;

        if (shunted.CoreLost || !TryComp<StationAiShuntReturnComponent>(brain, out var ret) || ret.Core == null)
            return;

        var coreContainer = _container.EnsureContainer<ContainerSlot>(ret.Core.Value, StationAiCoreComponent.Container);
        if (!_container.Insert(brain, coreContainer))
            return;

        CleanupShunt(brain, apc);
        _popup.PopupEntity(Loc.GetString("station-ai-shunt-return"), brain, brain, PopupType.Medium);
    }

    /// <summary>Remove o estado de shunt do cérebro e da APC. NÃO move o cérebro (quem move é o chamador).</summary>
    public void CleanupShunt(EntityUid brain, EntityUid apc)
    {
        if (TryComp<StationAiApcControllableComponent>(apc, out var apcComp))
        {
            apcComp.Occupied = false;
            Dirty(apc, apcComp);
            _stationAi.SetApcOccupiedVisual(apc, false);
        }

        // Remove a ação de voltar pelo entity UID guardado no componente (padrão do BorgSystem).
        if (TryComp<StationAiShuntReturnComponent>(brain, out var ret) && ret.ReturnAction != null)
            _actions.RemoveAction(ret.ReturnAction.Value);

        RemCompDeferred<StationAiShuntedComponent>(brain);
        RemCompDeferred<StationAiShuntReturnComponent>(brain);
    }
}

/// <summary>Guarda, no cérebro shuntado, qual era o núcleo de origem (para voltar). Server-only.</summary>
[RegisterComponent]
public sealed partial class StationAiShuntReturnComponent : Component
{
    /// <summary>O núcleo de origem, gravado no momento do shunt.</summary>
    [DataField]
    public EntityUid? Core;

    /// <summary>A entidade da ação "Voltar ao núcleo", para poder remover depois.</summary>
    [DataField]
    public EntityUid? ReturnAction;
}
