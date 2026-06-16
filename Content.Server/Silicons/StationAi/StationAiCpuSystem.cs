using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Economia de CPU da IA Malf. A CPU existe enquanto a IA está sob lei hostil (segue o
/// ciclo de vida do <see cref="StationAiHostileLawComponent"/>). Regenera por tick, com a
/// taxa aumentada por cada APC hackeada (<see cref="StationAiCpuComponent.HackedApcCount"/>),
/// e é gasta pelas ações do radial via <see cref="TryConsume"/>.
/// </summary>
public sealed partial class StationAiCpuSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // A CPU vive exatamente enquanto a marca de lei hostil existir.
        SubscribeLocalEvent<StationAiHostileLawComponent, ComponentInit>(OnHostileInit);
        SubscribeLocalEvent<StationAiHostileLawComponent, ComponentShutdown>(OnHostileShutdown);

        // Examinar o core/olho da IA mostra a CPU (só a própria IA enxerga o valor).
        SubscribeLocalEvent<StationAiCpuComponent, ExaminedEvent>(OnExamined);
    }

    private void OnHostileInit(Entity<StationAiHostileLawComponent> ent, ref ComponentInit args)
    {
        var cpu = EnsureComp<StationAiCpuComponent>(ent.Owner);
        // ShowAlert silenciosamente não faz nada sem AlertsComponent; garante que exista.
        EnsureComp<AlertsComponent>(ent.Owner);
        RefreshCpuAlert((ent.Owner, cpu));
    }

    private void OnHostileShutdown(Entity<StationAiHostileLawComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<StationAiCpuComponent>(ent.Owner, out var cpu))
            _alerts.ClearAlert(ent.Owner, cpu.CpuAlert);

        RemComp<StationAiCpuComponent>(ent.Owner);
    }

    private void OnExamined(Entity<StationAiCpuComponent> ent, ref ExaminedEvent args)
    {
        // Só a própria IA enxerga sua CPU.
        if (args.Examiner != args.Examined || !args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("station-ai-cpu-examine",
            ("cpu", (int) ent.Comp.Cpu), ("max", (int) ent.Comp.MaxCpu)));
    }

    /// <summary>
    /// Tenta gastar <paramref name="cost"/> de CPU da IA <paramref name="ai"/>.
    /// Custo &lt;= 0 ou IA sem componente de CPU (IA leal) sempre passa.
    /// Sem saldo: mostra popup e retorna false.
    /// </summary>
    public bool TryConsume(EntityUid ai, float cost)
    {
        if (cost <= 0f)
            return true;

        if (!TryComp<StationAiCpuComponent>(ai, out var cpu))
            return true; // IA não-Malf não tem orçamento.

        if (cpu.Cpu < cost)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-cpu-insufficient", ("cost", (int) cost)),
                ai, ai, PopupType.MediumCaution);
            return false;
        }

        cpu.Cpu -= cost;
        Dirty(ai, cpu);
        RefreshCpuAlert((ai, cpu));
        return true;
    }

    /// <summary>Recalcula o alert de HUD a partir da % atual.</summary>
    private void RefreshCpuAlert(Entity<StationAiCpuComponent> ent)
    {
        var max = ent.Comp.MaxCpu <= 0f ? 1f : ent.Comp.MaxCpu;
        var ratio = Math.Clamp(ent.Comp.Cpu / max, 0f, 1f);
        var maxSeverity = ent.Comp.AlertLevels - 1;
        var severity = (short) Math.Clamp((int) (ratio * maxSeverity), 0, maxSeverity);
        _alerts.ShowAlert(ent.Owner, ent.Comp.CpuAlert, severity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationAiCpuComponent>();
        while (query.MoveNext(out var uid, out var cpu))
        {
            if (cpu.Cpu >= cpu.MaxCpu)
                continue;

            var before = (int) cpu.Cpu;
            var rate = cpu.BaseRegen + cpu.RegenPerApc * cpu.HackedApcCount;
            cpu.Cpu = Math.Min(cpu.MaxCpu, cpu.Cpu + rate * frameTime);

            // Throttle: só sincroniza e atualiza o alert quando o valor inteiro muda.
            if ((int) cpu.Cpu != before)
            {
                Dirty(uid, cpu);
                RefreshCpuAlert((uid, cpu));
            }
        }
    }
}
