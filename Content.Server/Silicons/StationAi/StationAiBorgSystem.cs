using System.Linq;
using Content.Server.Mind;
using Content.Server.Silicons.Borgs;
using Content.Server.Silicons.Laws;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Lock;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Movement.Systems;
using Content.Shared.Trigger;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ações da IA de estação sobre borgs, disparadas pelo MENU RADIAL (segurar Alt + clicar no borg).
/// Por enquanto: <b>Subverter</b> — disponível só sob lawset hostil. Reaproveita todo o pipeline de
/// emag (<see cref="GotEmaggedEvent"/>): o borg ganha a lei "obedeça à IA" + o papel de silício
/// subvertido, e fica imune a tempestade iônica. O gate de "lei hostil" reusa a checagem já mantida
/// por <see cref="StationAiBulkDoorSystem"/>.
/// </summary>
public sealed partial class StationAiBorgSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private StationAiBulkDoorSystem _hostile = default!;
    [Dependency] private BorgSystem _borg = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private StationAiSystem _stationAi = default!;
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private StationAiCpuSystem _cpu = default!;

    /// <summary>
    /// Grupo de acesso concedido ao borg enquanto a IA o pilota (abre tudo, como a IA).
    /// </summary>
    private static readonly ProtoId<AccessGroupPrototype> AllAccessGroup = "AllAccess";

    /// <summary>
    /// Janela (em segundos) para confirmar a detonação após o primeiro clique.
    /// </summary>
    private const double DetonateConfirmWindow = 5.0;

    /// <summary>
    /// Cooldown (em segundos) entre a IA largar um borg e poder assumir outro (Fase 5A).
    /// </summary>
    private const double ControlCooldown = 30.0;

    /// <summary>
    /// Ação "Voltar ao núcleo" concedida ao borg enquanto a IA o pilota.
    /// </summary>
    private static readonly EntProtoId ActionLeaveBorg = "ActionStationAiLeaveBorg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgChassisComponent, StationAiSubvertBorgEvent>(OnSubvert);
        SubscribeLocalEvent<BorgChassisComponent, StationAiDisableBorgEvent>(OnDisable);
        SubscribeLocalEvent<BorgChassisComponent, StationAiDetonateBorgEvent>(OnDetonate);
        SubscribeLocalEvent<BorgChassisComponent, StationAiTogglePanelLockEvent>(OnTogglePanelLock);
        SubscribeLocalEvent<BorgChassisComponent, StationAiToggleImmobilizeEvent>(OnToggleImmobilize);
        SubscribeLocalEvent<BorgChassisComponent, StationAiToggleBorgLockEvent>(OnToggleBorgLock);
        SubscribeLocalEvent<BorgChassisComponent, StationAiControlBorgEvent>(OnControlBorg);

        // Fim do controle: ação de voltar, morte do borg ou deleção do borg.
        SubscribeLocalEvent<StationAiPilotedBorgComponent, StationAiLeaveBorgEvent>(OnLeaveBorg);
        SubscribeLocalEvent<StationAiPilotedBorgComponent, MobStateChangedEvent>(OnPilotedBorgMobState);
        // Borg prestes a explodir/disparar (ex.: detonação da IA): devolve a IA ANTES da explosão, com
        // o borg ainda vivo (caminho limpo, igual ao crítico). A explosão por dano massivo pode pular o
        // estado crítico e deletar o borg no mesmo tick, deixando a IA presa no cérebro; o trigger é o
        // momento certo de sair.
        SubscribeLocalEvent<StationAiPilotedBorgComponent, TriggerEvent>(OnPilotedBorgTrigger);
        // before MindSystem: ao deletar o borg, precisamos tirar a mente da IA (UnVisit) ANTES do
        // OnMindContainerTerminating do motor — senão ele trata a mente VISITANTE como se fosse do
        // borg, faz TransferTo(null) e a IA vira fantasma em vez de voltar ao núcleo.
        SubscribeLocalEvent<StationAiPilotedBorgComponent, EntityTerminatingEvent>(OnPilotedBorgTerminating,
            before: new[] { typeof(MindSystem) });
    }

    #region Fase 5A — IA controla borg vazio

    private void OnControlBorg(EntityUid uid, BorgChassisComponent comp, StationAiControlBorgEvent args)
    {
        // Fase 5A: só borg VAZIO (sem jogador). Borg com jogador é a Fase 5B (ainda não feita).
        if (TryComp<MindContainerComponent>(uid, out var container) && container.HasMind)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-borg-control-occupied"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Já está sendo pilotado por uma IA?
        if (HasComp<VisitingMindComponent>(uid) || HasComp<StationAiPilotedBorgComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("station-ai-borg-control-busy"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Acha a mente da IA (dona do cérebro, que é o args.User = StationAiHeld).
        if (!_mind.TryGetMind(args.User, out var mindId, out var mind))
            return;

        // A mente já está visitando outra coisa? (proteção; o Visit também recusa)
        if (mind.VisitingEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-borg-control-busy"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Cooldown (fica no cérebro da IA p/ persistir entre controles).
        var now = _timing.CurTime;
        var cd = EnsureComp<StationAiBorgControlComponent>(args.User);
        if (now < cd.NextControl)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-borg-control-cooldown"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Liga o chassi se estiver desligado: um borg vazio fica inerte e o Visit NÃO dispara o
        // MindAddedMessage que normalmente ativa o borg. Sem isso a IA não conseguiria se mover.
        var weActivated = false;
        if (!comp.Active)
        {
            _borg.SetActive((uid, comp), true);
            weActivated = true;
        }

        // A IA passa a pilotar o borg; a mente continua DONA do cérebro no núcleo (núcleo fica exposto).
        _mind.Visit(mindId, uid, mind);

        // Concede a ação de voltar ao núcleo (InstantAction funciona pilotando o borg).
        EntityUid? leaveAction = null;
        _actions.AddAction(uid, ref leaveAction, ActionLeaveBorg);

        var piloted = EnsureComp<StationAiPilotedBorgComponent>(uid);
        piloted.MindId = mindId;
        piloted.LeaveAction = leaveAction;
        piloted.WeActivated = weActivated;

        // Marcador shared: libera o menu radial (acesso limitado) enquanto a IA pilota o borg.
        EnsureComp<StationAiPilotingComponent>(uid);

        // Acesso geral enquanto pilota: habilita o acesso (borg vazio vem desligado) e dá AllAccess.
        // Isso abre portas/etc como a IA E faz a tag "Borg" voltar a existir — então as torretas da IA
        // (que exemptam Borg/BasicSilicon) deixam de mirar o borg controlado. Restaurado ao largar.
        if (TryComp<AccessComponent>(uid, out var access))
        {
            piloted.SavedAccessEnabled = access.Enabled;
            piloted.SavedAccessTags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags);
            _access.SetAccessEnabled(uid, true, access);
            _access.TryAddGroups(uid, new[] { AllAccessGroup }, access);
        }

        // Item 8: enquanto pilotado, o borg opera sob as leis ATUAIS da IA. Salva as leis originais do
        // borg para restaurar ao largar; aplica uma CÓPIA independente (ShallowClone) das leis da IA
        // (=args.User, o cérebro) para o borg e a IA não compartilharem a mesma lista. Silencioso
        // (notify:false): a IA já conhece as próprias leis — o aviso a cada entrada/saída seria ruído.
        piloted.SavedLaws = _laws.GetLaws(uid).Laws;
        var aiLaws = _laws.GetLaws(args.User).Laws.Select(law => law.ShallowClone()).ToList();
        _laws.SetLaws(aiLaws, uid, notify: false);

        cd.NextControl = now + TimeSpan.FromSeconds(ControlCooldown);

        _adminLogger.Add(LogType.Mind, LogImpact.High,
            $"{ToPrettyString(args.User):user} assumiu o controle do borg {ToPrettyString(uid):target} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-borg-control-success", ("name", Name(uid))), uid, uid, PopupType.Medium);
    }

    private void OnLeaveBorg(EntityUid uid, StationAiPilotedBorgComponent comp, StationAiLeaveBorgEvent args)
    {
        args.Handled = true;
        StopPiloting(uid, comp);
    }

    private void OnPilotedBorgTrigger(EntityUid uid, StationAiPilotedBorgComponent comp, ref TriggerEvent args)
    {
        // NÃO marca Handled: deixa a explosão acontecer normalmente — só tiramos a IA antes.
        StopPiloting(uid, comp);
    }

    private void OnPilotedBorgMobState(EntityUid uid, StationAiPilotedBorgComponent comp, MobStateChangedEvent args)
    {
        // Borg incapacitado (crítico OU morto) → devolve a IA ao núcleo ENQUANTO o borg ainda existe.
        // Crítico (100 de dano) acontece antes da destruição (300), então o UnVisit roda nas MESMAS
        // condições limpas da ação "Voltar ao núcleo" (que funciona) — em vez de tentar retornar com o
        // borg já em deleção, o que deixava a IA presa no cérebro sem o olho funcionando.
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            StopPiloting(uid, comp);
    }

    private void OnPilotedBorgTerminating(EntityUid uid, StationAiPilotedBorgComponent comp, ref EntityTerminatingEvent args)
    {
        // Borg sendo deletado (detonado/destruído) pilotando → devolve a IA ao núcleo antes de sumir.
        StopPiloting(uid, comp, terminating: true);
    }

    /// <summary>
    /// Encerra o controle do borg pela IA: remove a ação de voltar, devolve a mente ao núcleo
    /// (UnVisit — se o núcleo foi destruído nesse meio-tempo, a IA vira fantasma) e desliga o chassi
    /// de volta se fomos nós que o ligamos.
    /// </summary>
    private void StopPiloting(EntityUid borg, StationAiPilotedBorgComponent comp, bool terminating = false)
    {
        if (!terminating && comp.LeaveAction != null)
            _actions.RemoveAction(comp.LeaveAction.Value);

        _mind.UnVisit(comp.MindId);

        // Reanexa o olho ao núcleo: se o retorno aconteceu durante a morte/destruição do borg, o relay
        // de movimento e a câmera podiam ter ficado quebrados (IA "presa no cérebro"). Reanexar conserta.
        if (TryComp<MindComponent>(comp.MindId, out var mindComp)
            && mindComp.OwnedEntity is { } brain
            && _stationAi.TryGetCore(brain, out var core)
            && core.Comp != null)
        {
            _stationAi.RefreshAiEye(core);
        }

        // Restaura o acesso original do borg (tira o AllAccess; volta a desligado se era um borg vazio).
        if (comp.SavedAccessTags != null && TryComp<AccessComponent>(borg, out var access))
        {
            _access.TrySetTags(borg, comp.SavedAccessTags, access);
            _access.SetAccessEnabled(borg, comp.SavedAccessEnabled, access);
        }

        // Item 8: restaura as leis originais do borg (silencioso).
        if (comp.SavedLaws != null)
            _laws.SetLaws(comp.SavedLaws, borg, notify: false);

        if (!terminating && comp.WeActivated && TryComp<BorgChassisComponent>(borg, out var chassis))
            _borg.SetActive((borg, chassis), false);

        RemComp<StationAiPilotingComponent>(borg);
        RemComp<StationAiPilotedBorgComponent>(borg);
    }

    #endregion

    private void OnToggleBorgLock(EntityUid uid, BorgChassisComponent comp, StationAiToggleBorgLockEvent args)
    {
        // Disponível sob QUALQUER lei. A IA fura o ID e a exigência de painel: chama Lock/Unlock cru
        // (versões sem checagem de acesso), trancando/destrancando o LockComponent do borg.
        if (!TryComp<LockComponent>(uid, out var lockComp))
            return;

        if (args.Lock)
            _lock.Lock(uid, args.User, lockComp);
        else
            _lock.Unlock(uid, args.User, lockComp);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Lock ? "trancou" : "destrancou")} o borg {ToPrettyString(uid):target} (lock) pela IA de estação.");
    }

    private void OnToggleImmobilize(EntityUid uid, BorgChassisComponent comp, StationAiToggleImmobilizeEvent args)
    {
        // Disponível sob QUALQUER lei (toggle reversível). O borg-jogador não consegue remover o marcador.
        if (args.Immobilize)
        {
            EnsureComp<StationAiBorgImmobilizedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-immobilize-on", ("name", Name(uid))), uid, args.User, PopupType.Medium);
        }
        else
        {
            RemComp<StationAiBorgImmobilizedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-immobilize-off", ("name", Name(uid))), uid, args.User, PopupType.Medium);
        }

        // Reaplica os modificadores → a velocidade passa a contar (ou não) o zero do marcador.
        _movement.RefreshMovementSpeedModifiers(uid);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Immobilize ? "imobilizou" : "liberou")} o borg {ToPrettyString(uid):target} pela IA de estação.");
    }

    private void OnTogglePanelLock(EntityUid uid, BorgChassisComponent comp, StationAiTogglePanelLockEvent args)
    {
        // Disponível sob QUALQUER lei (uso defensivo de IA leal ou ofensivo de IA malf).
        if (args.Lock)
        {
            // Fecha o painel antes de trancar (senão ficaria aberto e travado). Fecha enquanto o
            // marcador ainda não existe, então o AttemptChangePanelEvent não é cancelado.
            if (TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open)
                _wires.TogglePanel(uid, panel, false, args.User);

            EnsureComp<StationAiBorgPanelLockComponent>(uid);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-panel-lock-on", ("name", Name(uid))), uid, args.User, PopupType.Medium);
        }
        else
        {
            RemComp<StationAiBorgPanelLockComponent>(uid);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-panel-lock-off", ("name", Name(uid))), uid, args.User, PopupType.Medium);
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(args.Lock ? "trancou" : "destrancou")} o painel do borg {ToPrettyString(uid):target} pela IA de estação.");
    }

    private void OnDisable(EntityUid uid, BorgChassisComponent comp, StationAiDisableBorgEvent args)
    {
        if (!_hostile.IsUserUnderHostileLaw(args.User))
        {
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-action-denied"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<BorgTransponderComponent>(uid, out var transponder))
        {
            _cpu.Refund(args.User, args.CpuCost);
            return;
        }

        // Reaproveita o "disable" do console de robótica (ejeta o cérebro após um atraso).
        _borg.Disable((uid, transponder, comp));

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} desligou o borg {ToPrettyString(uid):target} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-borg-disable-success", ("name", Name(uid))), uid, args.User, PopupType.Medium);
    }

    private void OnDetonate(EntityUid uid, BorgChassisComponent comp, StationAiDetonateBorgEvent args)
    {
        if (!_hostile.IsUserUnderHostileLaw(args.User))
        {
            // Ação recusada: devolve a CPU cobrada antecipadamente em OnRadialMessage.
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-action-denied"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        var now = _timing.CurTime;

        // Confirmação por duplo-clique: o primeiro clique arma; o segundo (mesmo ator, dentro da
        // janela) detona. Evita detonar por engano numa ação irreversível. O clique que ARMA não
        // deve custar CPU (a detonação ainda não aconteceu): estorna o custo cobrado neste clique,
        // então só a confirmação paga os 50 — antes, armar+confirmar cobrava 100, e armar e desistir
        // perdia 50 à toa.
        if (!TryComp<StationAiDetonateArmedComponent>(uid, out var armed) || armed.Armer != args.User || now > armed.Until)
        {
            armed = EnsureComp<StationAiDetonateArmedComponent>(uid);
            armed.Armer = args.User;
            armed.Until = now + TimeSpan.FromSeconds(DetonateConfirmWindow);
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-borg-detonate-arm", ("name", Name(uid))), uid, args.User, PopupType.LargeCaution);
            return;
        }

        RemComp<StationAiDetonateArmedComponent>(uid);

        _adminLogger.Add(LogType.Action, LogImpact.Extreme,
            $"{ToPrettyString(args.User):user} detonou o borg {ToPrettyString(uid):target} pela IA de estação.");
        // Reaproveita o "destroy" do console de robótica (explode o borg).
        _borg.Destroy(uid);
    }

    private void OnSubvert(EntityUid uid, BorgChassisComponent comp, StationAiSubvertBorgEvent args)
    {
        // Só sob lawset hostil. O cliente já esconde o botão, mas o servidor reconfirma (não confiar no cliente).
        if (!_hostile.IsUserUnderHostileLaw(args.User))
        {
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-subvert-denied"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Já subvertido/emagado? não empilhar leis "obedeça".
        if (_emag.CheckFlag(uid, EmagType.Interaction))
        {
            _cpu.Refund(args.User, args.CpuCost);
            _popup.PopupEntity(Loc.GetString("station-ai-subvert-already"), uid, args.User, PopupType.Medium);
            return;
        }

        // Reaproveita o pipeline de emag: adiciona a lei de obediência, o papel de silício
        // subvertido, o som e o atordoamento. O painel de fios não é exigido para a IA
        // (ver SharedSiliconLawSystem.OnGotEmagged).
        var ev = new GotEmaggedEvent(args.User, EmagType.Interaction);
        RaiseLocalEvent(uid, ref ev);
        if (!ev.Handled)
            return;

        // Marca o borg como emagado: imunidade a tempestade iônica e impede re-subversão.
        var emagged = EnsureComp<EmaggedComponent>(uid);
        emagged.EmagType |= EmagType.Interaction;
        Dirty(uid, emagged);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} subverteu o borg {ToPrettyString(uid):target} pela IA de estação.");
        _popup.PopupEntity(Loc.GetString("station-ai-subvert-success", ("name", Name(uid))), uid, args.User, PopupType.Medium);
    }
}
