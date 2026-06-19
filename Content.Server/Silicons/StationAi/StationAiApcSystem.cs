using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Administration.Logs;
using Content.Shared.APC;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Permite à IA de estação cortar/restaurar a energia de uma área pelo disjuntor da APC,
/// usando o menu radial (alt-clique). A IA controla APCs livremente, sem checagem de acesso
/// (como no SS13) — o whitelist do menu radial já garante que só uma IA válida chega aqui.
/// Mantém <see cref="StationAiApcControllableComponent.PowerOn"/> em sincronia para o cliente
/// rotular o botão corretamente.
///
/// Ação de hackear (IA Malf): transforma a APC em fonte de CPU e exibe um tell visual.
/// O toggle de energia fica bloqueado até que a APC tenha sido hackeada.
/// </summary>
public sealed partial class StationAiApcSystem : EntitySystem
{
    [Dependency] private ApcSystem _apc = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiApcControllableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationAiApcControllableComponent, StationAiApcToggleEvent>(OnToggle);
        SubscribeLocalEvent<StationAiApcControllableComponent, ApcMainBreakerChangedEvent>(OnBreakerChanged);
        SubscribeLocalEvent<StationAiApcControllableComponent, StationAiApcHackEvent>(OnHack);
        SubscribeLocalEvent<StationAiApcControllableComponent, ExaminedEvent>(OnApcExamined);
        SubscribeLocalEvent<StationAiApcControllableComponent, EntityTerminatingEvent>(OnApcTerminating);
    }

    private void OnMapInit(EntityUid uid, StationAiApcControllableComponent comp, MapInitEvent args)
    {
        if (TryComp(uid, out ApcComponent? apc))
            SetPowerOn((uid, comp), apc.MainBreakerEnabled);
    }

    private void OnToggle(EntityUid uid, StationAiApcControllableComponent comp, StationAiApcToggleEvent args)
    {
        if (!comp.Hacked)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-apc-not-hacked"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        // Toggle puro do estado real; o espelhamento de PowerOn vem do ApcMainBreakerChangedEvent.
        _apc.ApcToggleBreaker(uid, user: args.User);
    }

    private void OnBreakerChanged(EntityUid uid, StationAiApcControllableComponent comp, ref ApcMainBreakerChangedEvent args)
    {
        SetPowerOn((uid, comp), args.On);
    }

    private void OnHack(EntityUid uid, StationAiApcControllableComponent comp, StationAiApcHackEvent args)
    {
        if (comp.Hacked)
            return;

        comp.Hacked = true;
        comp.HackedBy = args.User; // server-only; não precisa de Dirty
        Dirty(uid, comp);          // sincroniza Hacked para o cliente

        // Aumenta a taxa de CPU da IA que hackeou.
        if (TryComp<StationAiCpuComponent>(args.User, out var cpu))
        {
            cpu.HackedApcCount++;
            Dirty(args.User, cpu);
        }

        // Tell visual.
        _appearance.SetData(uid, StationAiApcVisuals.Hacked, true);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} hackeou a APC {ToPrettyString(uid):target} (fonte de CPU da IA Malf).");
        _popup.PopupEntity(Loc.GetString("station-ai-apc-hacked"), uid, args.User, PopupType.Medium);
    }

    private void OnApcExamined(EntityUid uid, StationAiApcControllableComponent comp, ref ExaminedEvent args)
    {
        if (comp.Hacked && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("station-ai-apc-compromised"));

        if (comp.Occupied && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("station-ai-apc-anomalous"));
    }

    private void OnApcTerminating(EntityUid uid, StationAiApcControllableComponent comp, ref EntityTerminatingEvent args)
    {
        if (!comp.Hacked || comp.HackedBy == null)
            return;

        // A APC sumiu: a IA perde essa fonte de taxa (mas mantém a CPU já acumulada).
        if (TryComp<StationAiCpuComponent>(comp.HackedBy.Value, out var cpu) && cpu.HackedApcCount > 0)
        {
            cpu.HackedApcCount--;
            Dirty(comp.HackedBy.Value, cpu);
        }
    }

    private void SetPowerOn(Entity<StationAiApcControllableComponent> ent, bool on)
    {
        if (ent.Comp.PowerOn == on)
            return;

        ent.Comp.PowerOn = on;
        Dirty(ent);
    }
}
