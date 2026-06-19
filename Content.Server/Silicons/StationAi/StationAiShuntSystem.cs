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
        // Destruição do núcleo de origem: IA fica presa na APC (CoreLost).
        SubscribeLocalEvent<StationAiCoreComponent, EntityTerminatingEvent>(OnCoreTerminating);
        // Destruição da APC enquanto ocupada: IA morre. NÃO assinamos EntityTerminatingEvent da APC
        // aqui — o StationAiApcSystem já assina esse par (comp+evento) e o Robust proíbe duplicata.
        // O handler de lá chama HandleHostApcTerminating diretamente.
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
        // Se chegou aqui a CPU já foi cobrada, então avisa quando não dá pra shuntar (senão some sem explicação).
        if (!_stationAi.TryGetCore(brain, out var core))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-shunt-failed"), apc, brain, PopupType.MediumCaution);
            return false;
        }

        // Grava o núcleo de origem antes de mover o cérebro (após o Insert, TryGetCore retorna false).
        var ret = EnsureComp<StationAiShuntReturnComponent>(brain);
        ret.Core = core.Owner;

        var shuntSlot = _container.EnsureContainer<ContainerSlot>(apc, ShuntContainer);
        if (!_container.Insert(brain, shuntSlot)) // Insert remove do container antigo automaticamente
        {
            RemComp<StationAiShuntReturnComponent>(brain);
            _popup.PopupEntity(Loc.GetString("station-ai-shunt-failed"), apc, brain, PopupType.MediumCaution);
            return false;
        }

        var shunted = AddComp<StationAiShuntedComponent>(brain);
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

    /// <summary>
    /// Núcleo de origem destruído enquanto a IA está shuntada: marca CoreLost e remove a ação de retorno.
    /// A IA permanece na APC — não há mais para onde voltar.
    /// </summary>
    private void OnCoreTerminating(Entity<StationAiCoreComponent> core, ref EntityTerminatingEvent args)
    {
        var query = EntityQueryEnumerator<StationAiShuntedComponent, StationAiShuntReturnComponent>();
        while (query.MoveNext(out var brain, out var shunted, out var ret))
        {
            if (ret.Core != core.Owner || shunted.CoreLost)
                continue;

            shunted.CoreLost = true;
            Dirty(brain, shunted);

            // Remove a ação de voltar pelo UID guardado no componente (mesmo padrão de CleanupShunt).
            if (ret.ReturnAction != null)
            {
                _actions.RemoveAction(ret.ReturnAction.Value);
                ret.ReturnAction = null;
            }

            _popup.PopupEntity(Loc.GetString("station-ai-shunt-core-lost"), brain, brain, PopupType.LargeCaution);
        }
    }

    /// <summary>
    /// APC destruída enquanto ocupada pela IA: limpa o estado de shunt e deleta o cérebro.
    /// Deletar a entidade-cérebro dispara o fluxo padrão de morte/fantasma do engine.
    /// </summary>
    public void HandleHostApcTerminating(Entity<StationAiApcControllableComponent> apc)
    {
        if (!apc.Comp.Occupied)
            return;

        var query = EntityQueryEnumerator<StationAiShuntedComponent>();
        while (query.MoveNext(out var brain, out var shunted))
        {
            if (shunted.HostApc != apc.Owner)
                continue;

            // Limpa flags da APC e remove componentes de shunt antes de deletar o cérebro.
            CleanupShunt(brain, apc.Owner);
            // O cérebro é filho da APC (mora no container dela), então o engine já o termina em cascata
            // nesta mesma passada — é isso que dispara o fantasma (MindContainer terminando). O QueueDel
            // abaixo é só uma rede de segurança caso o cérebro não esteja mais no container.
            QueueDel(brain);
        }
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
    public EntityUid? ReturnAction;
}
