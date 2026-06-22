using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Economia de CPU ("processing power") da IA de estação. TODA IA tem CPU (vem no protótipo
/// AiHeld): a IA LEAL ganha um valor FIXO por segundo (teto menor, ações mais caras) e a IA
/// MALF (sob <see cref="StationAiHostileLawComponent"/>) tem ganho que escala por APC hackeada.
/// A CPU regenera por tick e é gasta pelas ações do radial via <see cref="TryConsume"/>.
/// </summary>
public sealed partial class StationAiCpuSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Toda IA de estação tem CPU (vem no protótipo AiHeld, adicionado ao entrar no core).
        SubscribeLocalEvent<StationAiCpuComponent, ComponentStartup>(OnCpuStartup);

        // Virar / deixar de ser Malf (lei hostil) só RECONFIGURA a economia — não cria/remove a CPU.
        SubscribeLocalEvent<StationAiHostileLawComponent, ComponentInit>(OnHostileInit);
        SubscribeLocalEvent<StationAiHostileLawComponent, ComponentShutdown>(OnHostileShutdown);

        // Examinar a CPU. Ela mora no cérebro (held); o jogador examina o CORE, então relê via core.
        SubscribeLocalEvent<StationAiCpuComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StationAiCoreComponent, ExaminedEvent>(OnCoreExamined);
    }

    private void OnCpuStartup(Entity<StationAiCpuComponent> ent, ref ComponentStartup args)
    {
        // ShowAlert silenciosamente não faz nada sem AlertsComponent; garante que exista.
        EnsureComp<AlertsComponent>(ent.Owner);

        // Config conforme o estado atual: Malf se já estiver sob lei hostil, senão LEAL.
        // (Cobre as duas ordens possíveis de adição dos componentes ao entrar no core.)
        if (HasComp<StationAiHostileLawComponent>(ent.Owner))
            ConfigureMalf(ent.Comp);
        else
            ConfigureLoyal(ent.Comp);

        // Recupera a contagem de APCs hackeadas a partir da VERDADE (as próprias APCs). Este componente
        // mora no protótipo do cérebro e RE-INICIALIZA quando a IA volta de um shunt (ComponentStartup
        // dispara de novo), zerando o contador — sem isso, as APCs já hackeadas parariam de gerar CPU.
        RecomputeHackedApcCount(ent);

        RefreshCpuAlert(ent);
    }

    private void OnHostileInit(Entity<StationAiHostileLawComponent> ent, ref ComponentInit args)
    {
        // Fallback: garante a CPU mesmo que o protótipo não a tenha. Aplica a config MALF.
        var cpu = EnsureComp<StationAiCpuComponent>(ent.Owner);
        EnsureComp<AlertsComponent>(ent.Owner);
        ConfigureMalf(cpu);
        cpu.Cpu = 0f; // ao virar Malf, começa do zero: o saldo é conquistado hackeando APCs.
        Dirty(ent.Owner, cpu);
        RefreshCpuAlert((ent.Owner, cpu));
    }

    private void OnHostileShutdown(Entity<StationAiHostileLawComponent> ent, ref ComponentShutdown args)
    {
        // Deixou de ser Malf: NÃO remove a CPU (a IA leal também tem). Volta à config leal.
        if (!TryComp<StationAiCpuComponent>(ent.Owner, out var cpu))
            return;

        ConfigureLoyal(cpu);
        cpu.Cpu = Math.Min(cpu.Cpu, cpu.MaxCpu);
        Dirty(ent.Owner, cpu);
        RefreshCpuAlert((ent.Owner, cpu));
    }

    /// <summary>Config da IA Malf: ganho escala com APCs hackeadas, custo normal (1x). Nerf 2026-06-16.</summary>
    private void ConfigureMalf(StationAiCpuComponent cpu)
    {
        cpu.MaxCpu = 180f;
        cpu.BaseRegen = 0.05f;
        cpu.RegenPerApc = 0.12f;
        cpu.CostMultiplier = 1f;
    }

    /// <summary>Config da IA leal: ganho FIXO bom (não hackeia APC), teto menor, ações mais caras.</summary>
    private void ConfigureLoyal(StationAiCpuComponent cpu)
    {
        cpu.MaxCpu = 100f;
        cpu.BaseRegen = 0.5f; // aumento minimal (era 0.3): geração da IA leal estava lenta demais
        cpu.RegenPerApc = 0f;
        cpu.CostMultiplier = 1.5f;
        cpu.HackedApcCount = 0;
    }

    /// <summary>
    /// Reconta as APCs hackeadas por esta IA varrendo as próprias APCs (fonte da verdade: o campo
    /// HackedBy de cada APC aponta para o cérebro, cujo UID é estável). Usado para recuperar a contagem
    /// quando o StationAiCpuComponent re-inicializa no ciclo de shunt — assim as APCs já hackeadas
    /// continuam gerando CPU depois que a IA volta ao núcleo.
    /// </summary>
    private void RecomputeHackedApcCount(Entity<StationAiCpuComponent> ent)
    {
        var count = 0;
        var query = EntityQueryEnumerator<StationAiApcControllableComponent>();
        while (query.MoveNext(out _, out var apc))
        {
            if (apc.Hacked && apc.HackedBy == ent.Owner)
                count++;
        }

        ent.Comp.HackedApcCount = count;
    }

    private void OnExamined(Entity<StationAiCpuComponent> ent, ref ExaminedEvent args)
    {
        // Só a própria IA enxerga sua CPU (caso o cérebro seja examinado diretamente).
        if (args.Examiner != args.Examined || !args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("station-ai-cpu-examine",
            ("cpu", (int) ent.Comp.Cpu), ("max", (int) ent.Comp.MaxCpu)));
    }

    /// <summary>
    /// A CPU mora no cérebro (held), mas o jogador examina o CORE. Relê a CPU do cérebro contido,
    /// e só mostra se quem examina é a própria IA (o cérebro).
    /// </summary>
    private void OnCoreExamined(Entity<StationAiCoreComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_container.TryGetContainer(ent.Owner, StationAiCoreComponent.Container, out var container))
            return;

        foreach (var held in container.ContainedEntities)
        {
            if (held != args.Examiner || !TryComp<StationAiCpuComponent>(held, out var cpu))
                continue;

            args.PushMarkup(Loc.GetString("station-ai-cpu-examine",
                ("cpu", (int) cpu.Cpu), ("max", (int) cpu.MaxCpu)));
            break;
        }
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
            return true; // sem componente (fallback): ação sai de graça.

        // A IA leal paga mais caro (CostMultiplier). A Malf paga 1x.
        var realCost = cost * cpu.CostMultiplier;

        if (cpu.Cpu < realCost)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-cpu-insufficient", ("cost", (int) realCost)),
                ai, ai, PopupType.MediumCaution);
            return false;
        }

        cpu.Cpu -= realCost;
        Dirty(ai, cpu);
        RefreshCpuAlert((ai, cpu));
        return true;
    }

    /// <summary>
    /// Devolve <paramref name="cost"/> de CPU à IA <paramref name="ai"/> (espelha o
    /// <see cref="CostMultiplier"/> do <see cref="TryConsume"/>). Usado quando a ação do radial foi
    /// cobrada antecipadamente em OnRadialMessage mas o handler a RECUSOU — assim a IA não perde CPU
    /// por uma ação que nunca aconteceu. Clampa no teto. Custo &lt;= 0 ou IA sem CPU: no-op.
    /// </summary>
    public void Refund(EntityUid ai, float cost)
    {
        if (cost <= 0f)
            return;

        if (!TryComp<StationAiCpuComponent>(ai, out var cpu))
            return;

        cpu.Cpu = Math.Min(cpu.MaxCpu, cpu.Cpu + cost * cpu.CostMultiplier);
        Dirty(ai, cpu);
        RefreshCpuAlert((ai, cpu));
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
