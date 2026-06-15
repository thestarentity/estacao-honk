# IA Malf — Fase 3 Bloco 1: Economia de CPU + Hackear APC — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar à IA Malf uma economia de CPU (acumula no tempo; APCs hackeadas são a taxa de ganho) em que toda ação do radial custa CPU, com hackear APC como fonte visível e um indicador de CPU para a IA.

**Architecture:** Um componente `StationAiCpuComponent` mora na entidade-cérebro da IA (a mesma que tem `SiliconLawBoundComponent`/`StationAiHostileLawComponent` e é o `args.User` das ações e o `LocalEntity` no cliente). Um `StationAiCpuSystem` (servidor) regenera a CPU por tick e expõe `TryConsume`. Todas as ações já passam por um único ponto, `OnRadialMessage` em `SharedStationAiSystem.Held.cs`; lá um gancho virtual debita o custo (declarado em `BaseStationAiAction.CpuCost`) de forma autoritativa no servidor. Hackear APC reaproveita `StationAiApcControllableComponent`/`StationAiApcSystem`.

**Tech Stack:** C# (RobustToolbox ECS), prototypes YAML, Fluent (FTL en-US + pt-BR), RSI sprites.

**Verificação (importante — padrão deste fork):** Este repositório valida sistemas de gameplay da IA por **build limpo + teste em jogo no staging (porta 1213)**, igual a todas as fases anteriores da IA Malf (ver memória `ss14-malf-ai-design` / `ss14-staging-server`). O projeto NÃO usa testes unitários para esses sistemas ECS. Portanto cada tarefa termina com um build de verificação, e a Tarefa 10 faz a validação em jogo contra os critérios de aceite. Build de verificação por tarefa:

```
cd ~/estacao-honk/space-station-14
dotnet build Content.Client/Content.Client.csproj -c Debug
```
(compila Content.Shared e Content.Server como dependências; para pegar os analisadores RA0049/RA0051, rode também `dotnet build Content.Server/Content.Server.csproj -c Release` quando a tarefa mexer em sistema com `[Dependency]`.)

**Pegadinhas conhecidas (todas já documentadas):**
- RA0049/RA0051 (Release): todo sistema com `[Dependency]` precisa ser `partial` e os campos NÃO podem ser `readonly`.
- Throttle do `Dirty` da CPU: só sincronizar/atualizar alert quando o valor inteiro de CPU muda, nunca todo tick.
- Resources é symlink compartilhado prod↔repo: YAML novo referenciando componente novo quebra a DLL antiga no boot → no deploy, subir a DLL nova junto (tratado no deploy, fora deste plano).

---

## Mapa de arquivos

**Criar:**
- `Content.Shared/Silicons/StationAi/StationAiCpuComponent.cs` — estado da CPU (networked).
- `Content.Server/Silicons/StationAi/StationAiCpuSystem.cs` — regen, `TryConsume`, ciclo de vida, alert, examine do core.
- `Resources/Prototypes/Alerts/station_ai_cpu.yml` — alert de CPU (5 níveis de severidade).
- `Resources/Textures/Interface/Alerts/station_ai_cpu.rsi/meta.json` (+ PNGs placeholder) — sprite do alert (o usuário redesenha depois).

**Modificar:**
- `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Held.cs` — `CpuCost` em `BaseStationAiAction` + gancho `TrySpendActionCpu` no `OnRadialMessage`.
- `Content.Server/Silicons/StationAi/StationAiSystem.cs` — override de `TrySpendActionCpu` chamando o `StationAiCpuSystem`.
- `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Airlock.cs` — `CpuCost` nos eventos de porta única (Bolt/Emergency/Electrified = 3).
- `Content.Shared/Silicons/StationAi/StationAiBulkDoorEvents.cs` — `CpuCost` (área = 3, estação = 75).
- `Content.Shared/Silicons/StationAi/StationAiBorgEvents.cs` — `CpuCost` (subverter 30, desligar 10, imobilizar 10, detonar 50).
- `Content.Shared/Silicons/StationAi/StationAiTurretEvents.cs` — `CpuCost` (armamento letal 10).
- `Content.Shared/Silicons/StationAi/StationAiAtmosEvents.cs` — `CpuCost` (pânico 75).
- `Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs` — campo `Hacked` (networked) + `HackedBy` (server-only).
- `Content.Shared/Silicons/StationAi/StationAiApcEvents.cs` — novo `StationAiApcHackEvent`.
- `Content.Server/Silicons/StationAi/StationAiApcSystem.cs` — hackear (marca, +taxa, appearance, log), gate do toggle, examine, decremento ao destruir.
- `Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs` — radial: "Hackear" antes, toggle depois.
- `Content.Client/Silicons/StationAi/StationAiSystem.cs` — visualizer de Appearance da APC hackeada (tell visual).
- `Content.Client/Silicons/StationAi/StationAiBoundUserInterface.cs` — custo no tooltip + cinza quando sem saldo.
- `Resources/Locale/en-US/silicons/station-ai.ftl` (+ equivalente pt-BR) — strings novas.

---

## Task 1: Componente de CPU (Shared)

**Files:**
- Create: `Content.Shared/Silicons/StationAi/StationAiCpuComponent.cs`

- [ ] **Step 1: Criar o componente**

```csharp
using Robust.Shared.GameStates;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Economia de CPU ("processing power") da IA Malf. Mora na entidade-cérebro da IA
/// (a mesma que carrega <see cref="StationAiHostileLawComponent"/> e as leis). A CPU
/// sobe sozinha por tick; cada APC hackeada aumenta a taxa de ganho. Toda ação do
/// radial debita o custo declarado em <c>BaseStationAiAction.CpuCost</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiCpuComponent : Component
{
    /// <summary>CPU disponível atual. Networked p/ o cliente cinzar ações e mostrar o alert.</summary>
    [DataField, AutoNetworkedField]
    public float Cpu;

    /// <summary>Teto de CPU. Evita banking infinito.</summary>
    [DataField, AutoNetworkedField]
    public float MaxCpu = 200f;

    /// <summary>Ganho base de CPU por segundo, sem nenhuma APC hackeada.</summary>
    [DataField]
    public float BaseRegen = 0.1f;

    /// <summary>Ganho adicional de CPU por segundo, por APC hackeada.</summary>
    [DataField]
    public float RegenPerApc = 0.2f;

    /// <summary>Quantas APCs hackeadas alimentam a taxa. Mantido pelo StationAiApcSystem.</summary>
    [DataField]
    public int HackedApcCount;

    /// <summary>Alert de HUD que mostra a % de CPU.</summary>
    [DataField]
    public ProtoId<AlertPrototype> CpuAlert = "StationAiCpu";

    /// <summary>Nº de níveis de severidade do alert (0..Levels-1). Casado com o YAML do alert.</summary>
    [DataField]
    public int AlertLevels = 5;
}
```

- [ ] **Step 2: Build de verificação**

Run: `dotnet build Content.Client/Content.Client.csproj -c Debug`
Expected: BUILD SUCCEEDED (componente novo compila; ainda sem uso).

- [ ] **Step 3: Commit**

```bash
git add Content.Shared/Silicons/StationAi/StationAiCpuComponent.cs
git commit -m "feat(malf): componente de CPU da IA Malf"
```

---

## Task 2: Alert de CPU + RSI placeholder

**Files:**
- Create: `Resources/Prototypes/Alerts/station_ai_cpu.yml`
- Create: `Resources/Textures/Interface/Alerts/station_ai_cpu.rsi/meta.json`
- Create: `Resources/Textures/Interface/Alerts/station_ai_cpu.rsi/cpu0.png` … `cpu4.png` (placeholder)

- [ ] **Step 1: Criar o prototype do alert (5 níveis)**

```yaml
# Alert de "processing power" (CPU) da IA Malf. 5 níveis: cpu0 (vazio) → cpu4 (cheio).
# O sprite é placeholder; o usuário fornece a arte final em station_ai_cpu.rsi.
- type: alert
  id: StationAiCpu
  category: Health
  icons:
  - sprite: /Textures/Interface/Alerts/station_ai_cpu.rsi
    state: cpu0
  - sprite: /Textures/Interface/Alerts/station_ai_cpu.rsi
    state: cpu1
  - sprite: /Textures/Interface/Alerts/station_ai_cpu.rsi
    state: cpu2
  - sprite: /Textures/Interface/Alerts/station_ai_cpu.rsi
    state: cpu3
  - sprite: /Textures/Interface/Alerts/station_ai_cpu.rsi
    state: cpu4
  minSeverity: 0
  maxSeverity: 4
  name: alerts-station-ai-cpu-name
  description: alerts-station-ai-cpu-desc
```

- [ ] **Step 2: Criar o meta.json do RSI**

```json
{
  "version": 1,
  "license": "CC-BY-SA-3.0",
  "copyright": "Placeholder para Estacao Honk; arte final a fornecer pelo usuario.",
  "size": { "x": 32, "y": 32 },
  "states": [
    { "name": "cpu0" },
    { "name": "cpu1" },
    { "name": "cpu2" },
    { "name": "cpu3" },
    { "name": "cpu4" }
  ]
}
```

- [ ] **Step 3: Gerar os PNGs placeholder (copiando um sprite existente p/ o jogo bootar)**

Run:
```bash
cd ~/estacao-honk/space-station-14/Resources/Textures/Interface/Alerts
src=essence_counter.rsi/essence0.png
for n in 0 1 2 3 4; do cp "$src" station_ai_cpu.rsi/cpu$n.png; done
ls station_ai_cpu.rsi/
```
Expected: lista `cpu0.png cpu1.png cpu2.png cpu3.png cpu4.png meta.json`.
(São idênticos de propósito — placeholder. O usuário redesenha 5 frames de "medidor" depois.)

- [ ] **Step 4: Adicionar as FTL do alert**

Em `Resources/Locale/en-US/silicons/station-ai.ftl`, adicionar:
```
alerts-station-ai-cpu-name = Processing power
alerts-station-ai-cpu-desc = Your available CPU. Hacking APCs raises how fast it regenerates. Heavy actions spend it.
```
Em `Resources/Locale/pt-BR/silicons/station-ai.ftl`, adicionar:
```
alerts-station-ai-cpu-name = Poder de processamento
alerts-station-ai-cpu-desc = Sua CPU disponível. Hackear APCs aumenta a velocidade de regeneração. Ações pesadas gastam CPU.
```
(Se o arquivo pt-BR não existir nesse caminho, criar com só essas duas linhas; a localização pt-BR é o `DefaultCulture` do fork — ver `feedback-dll-rebuild`.)

- [ ] **Step 5: Build de verificação**

Run: `dotnet build Content.Client/Content.Client.csproj -c Debug`
Expected: BUILD SUCCEEDED.

- [ ] **Step 6: Commit**

```bash
git add Resources/Prototypes/Alerts/station_ai_cpu.yml Resources/Textures/Interface/Alerts/station_ai_cpu.rsi Resources/Locale/en-US/silicons/station-ai.ftl Resources/Locale/pt-BR/silicons/station-ai.ftl
git commit -m "feat(malf): alert de CPU da IA Malf (placeholder de sprite)"
```

---

## Task 3: Sistema de CPU (servidor) — ciclo de vida, regen, gasto

**Files:**
- Create: `Content.Server/Silicons/StationAi/StationAiCpuSystem.cs`

- [ ] **Step 1: Criar o sistema**

```csharp
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
        UpdateAlert((ent.Owner, cpu));
    }

    private void OnHostileShutdown(Entity<StationAiHostileLawComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<StationAiCpuComponent>(ent.Owner, out var cpu))
            _alerts.ClearAlert(ent.Owner, cpu.CpuAlert);

        RemComp<StationAiCpuComponent>(ent.Owner);
    }

    private void OnExamined(Entity<StationAiCpuComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
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
        UpdateAlert((ai, cpu));
        return true;
    }

    /// <summary>Recalcula o alert de HUD a partir da % atual.</summary>
    private void UpdateAlert(Entity<StationAiCpuComponent> ent)
    {
        var max = ent.Comp.MaxCpu <= 0f ? 1f : ent.Comp.MaxCpu;
        var ratio = Math.Clamp(ent.Comp.Cpu / max, 0f, 1f);
        var severity = (short) Math.Clamp((int) (ratio * (ent.Comp.AlertLevels - 1)), 0, ent.Comp.AlertLevels - 1);
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
                UpdateAlert((uid, cpu));
            }
        }
    }
}
```

- [ ] **Step 2: Adicionar as FTL de examine e popup**

Em `Resources/Locale/en-US/silicons/station-ai.ftl`:
```
station-ai-cpu-examine = Processing power: [color=cyan]{ $cpu }/{ $max } CPU[/color].
station-ai-cpu-insufficient = Not enough CPU (needs { $cost }).
```
Em `Resources/Locale/pt-BR/silicons/station-ai.ftl`:
```
station-ai-cpu-examine = Poder de processamento: [color=cyan]{ $cpu }/{ $max } CPU[/color].
station-ai-cpu-insufficient = CPU insuficiente (precisa de { $cost }).
```

- [ ] **Step 3: Build de verificação (Debug + Release p/ analisadores)**

Run:
```
dotnet build Content.Server/Content.Server.csproj -c Release
```
Expected: BUILD SUCCEEDED, sem RA0049/RA0051 (o sistema é `partial` e os `[Dependency]` não são `readonly`).

- [ ] **Step 4: Commit**

```bash
git add Content.Server/Silicons/StationAi/StationAiCpuSystem.cs Resources/Locale/en-US/silicons/station-ai.ftl Resources/Locale/pt-BR/silicons/station-ai.ftl
git commit -m "feat(malf): sistema de CPU (ciclo de vida, regen, gasto, alert, examine)"
```

---

## Task 4: Gancho central de gasto — CpuCost + OnRadialMessage + override do servidor

**Files:**
- Modify: `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Held.cs`
- Modify: `Content.Server/Silicons/StationAi/StationAiSystem.cs`

- [ ] **Step 1: Adicionar `CpuCost` à classe base das ações**

Em `SharedStationAiSystem.Held.cs`, na classe `BaseStationAiAction`, adicionar a propriedade virtual (logo abaixo de `User`):

```csharp
public abstract class BaseStationAiAction
{
    [field:NonSerialized]
    public EntityUid User { get; set; }

    /// <summary>
    /// Custo de CPU desta ação para a IA Malf. 0 = grátis (padrão). Cada ação paga
    /// sobrescreve. Debitado de forma autoritativa no servidor em OnRadialMessage.
    /// </summary>
    public virtual float CpuCost => 0f;
}
```

- [ ] **Step 2: Chamar o gancho no `OnRadialMessage`**

Em `SharedStationAiSystem.Held.cs`, no método `OnRadialMessage`, inserir a checagem ANTES de levantar o evento:

```csharp
    private void OnRadialMessage(StationAiRadialMessage ev)
    {
        if (!TryGetEntity(ev.Entity, out var target))
            return;

        ev.Event.User = ev.Actor;

        // Gasto de CPU (autoritativo no servidor; no cliente o gancho base deixa passar).
        if (!TrySpendActionCpu(ev.Actor, ev.Event))
            return;

        RaiseLocalEvent(target.Value, (object) ev.Event);
    }

    /// <summary>
    /// Gancho de gasto de CPU. Base (cliente) sempre deixa passar; o servidor sobrescreve
    /// para debitar do <see cref="StationAiCpuComponent"/> e negar se faltar saldo.
    /// </summary>
    protected virtual bool TrySpendActionCpu(EntityUid ai, BaseStationAiAction action) => true;
```

- [ ] **Step 3: Sobrescrever o gancho no servidor**

Em `Content.Server/Silicons/StationAi/StationAiSystem.cs`, adicionar o `[Dependency]` (junto aos outros, no topo da classe) e o override (em qualquer ponto da classe `partial`):

```csharp
    [Dependency] private StationAiCpuSystem _cpu = default!;
```
```csharp
    protected override bool TrySpendActionCpu(EntityUid ai, BaseStationAiAction action)
    {
        return _cpu.TryConsume(ai, action.CpuCost);
    }
```

- [ ] **Step 4: Build de verificação**

Run: `dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: BUILD SUCCEEDED. (Como ainda nenhuma ação sobrescreve `CpuCost`, tudo custa 0 — sem mudança de comportamento ainda.)

- [ ] **Step 5: Commit**

```bash
git add Content.Shared/Silicons/StationAi/SharedStationAiSystem.Held.cs Content.Server/Silicons/StationAi/StationAiSystem.cs
git commit -m "feat(malf): gancho central de gasto de CPU nas acoes do radial"
```

---

## Task 5: Custos das ações (CpuCost overrides)

**Files:**
- Modify: `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Airlock.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiBulkDoorEvents.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiBorgEvents.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiTurretEvents.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiAtmosEvents.cs`

- [ ] **Step 1: Porta única (3) — em `SharedStationAiSystem.Airlock.cs`**

Em cada uma das três classes, adicionar o override. Para `StationAiBoltEvent`:
```csharp
    public override float CpuCost => 3f;
```
Para `StationAiEmergencyAccessEvent`:
```csharp
    public override float CpuCost => 3f;
```
Para `StationAiElectrifiedEvent`:
```csharp
    public override float CpuCost => 3f;
```

- [ ] **Step 2: Área (3) e estação (75) — em `StationAiBulkDoorEvents.cs`**

`StationAiBoltAreaEvent`, `StationAiElectrifyAreaEvent`, `StationAiEmergencyAccessAreaEvent` → cada uma recebe:
```csharp
    public override float CpuCost => 3f;
```
`StationAiBoltStationEvent`, `StationAiElectrifyStationEvent`, `StationAiEmergencyAccessStationEvent` → cada uma recebe:
```csharp
    public override float CpuCost => 75f;
```

- [ ] **Step 3: Borg — em `StationAiBorgEvents.cs`**

`StationAiSubvertBorgEvent`:
```csharp
    public override float CpuCost => 30f;
```
`StationAiDisableBorgEvent`:
```csharp
    public override float CpuCost => 10f;
```
`StationAiDetonateBorgEvent`:
```csharp
    public override float CpuCost => 50f;
```
`StationAiToggleImmobilizeEvent`:
```csharp
    public override float CpuCost => 10f;
```
(As demais — `StationAiTogglePanelLockEvent`, `StationAiToggleBorgLockEvent`, `StationAiControlBorgEvent` — ficam grátis; não receber override.)

- [ ] **Step 4: Torreta — armamento letal (10) — em `StationAiTurretEvents.cs`**

Em `StationAiTurretArmamentEvent` (que tem `public int Armament;`), adicionar:
```csharp
    // Armamento >= 1 é o modo letal (ver LethalArmament no StationAiTurretSystem).
    public override float CpuCost => Armament >= 1 ? 10f : 0f;
```

- [ ] **Step 5: Atmos — pânico (75) — em `StationAiAtmosEvents.cs`**

Em `StationAiAirAlarmModeEvent` (que tem `public AirAlarmMode Mode;`), adicionar:
```csharp
    public override float CpuCost => Mode == AirAlarmMode.Panic ? 75f : 0f;
```
(Se o `using` do `AirAlarmMode` não estiver no arquivo, ele já está — o campo `Mode` é desse tipo. Não adicionar import novo a menos que o build acuse.)

- [ ] **Step 6: Build de verificação**

Run: `dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: BUILD SUCCEEDED.

- [ ] **Step 7: Commit**

```bash
git add Content.Shared/Silicons/StationAi/SharedStationAiSystem.Airlock.cs Content.Shared/Silicons/StationAi/StationAiBulkDoorEvents.cs Content.Shared/Silicons/StationAi/StationAiBorgEvents.cs Content.Shared/Silicons/StationAi/StationAiTurretEvents.cs Content.Shared/Silicons/StationAi/StationAiAtmosEvents.cs
git commit -m "feat(malf): custos de CPU por acao (portas, borg, torreta, atmos)"
```

---

## Task 6: Hackear APC (servidor) — estado, evento, taxa, examine, destruição

**Files:**
- Modify: `Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiApcEvents.cs`
- Modify: `Content.Server/Silicons/StationAi/StationAiApcSystem.cs`

- [ ] **Step 1: Estado `Hacked` + `HackedBy` no componente da APC**

Em `StationAiApcControllableComponent.cs`, adicionar dentro da classe:
```csharp
    /// <summary>
    /// APC hackeada pela IA Malf? Networked para o cliente mostrar o tell visual e o radial.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Hacked;

    /// <summary>
    /// Qual IA (cérebro) hackeou esta APC. Só servidor — usado para decrementar a taxa de CPU
    /// quando a APC é destruída. Não networked de propósito.
    /// </summary>
    public EntityUid HackedBy;
```

- [ ] **Step 2: Evento de hackear**

Em `StationAiApcEvents.cs`, adicionar a classe nova:
```csharp
/// <summary>
/// Ação da IA Malf para hackear uma APC pelo menu radial: vira fonte de CPU e fica visível.
/// É pré-requisito para as ações de cortar/restaurar energia daquela APC.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiApcHackEvent : BaseStationAiAction
{
}
```

- [ ] **Step 3: Definir o visual layer da APC hackeada (enum compartilhado)**

No fim de `StationAiApcControllableComponent.cs` (fora da classe, mesmo namespace), adicionar:
```csharp
[Serializable, NetSerializable]
public enum StationAiApcVisuals : byte
{
    Hacked,
}
```
E garantir os `using` no topo do arquivo:
```csharp
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
```

- [ ] **Step 4: Lógica de hackear no servidor**

Em `StationAiApcSystem.cs`, adicionar os `using` necessários no topo:
```csharp
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Robust.Shared.Containers;
```
Adicionar os `[Dependency]` no corpo da classe:
```csharp
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
```
Registrar as subscrições em `Initialize` (junto às existentes):
```csharp
        SubscribeLocalEvent<StationAiApcControllableComponent, StationAiApcHackEvent>(OnHack);
        SubscribeLocalEvent<StationAiApcControllableComponent, ExaminedEvent>(OnApcExamined);
        SubscribeLocalEvent<StationAiApcControllableComponent, EntityTerminatingEvent>(OnApcTerminating);
```
E o gate do toggle existente: no início de `OnToggle`, recusar se a APC ainda não foi hackeada:
```csharp
    private void OnToggle(EntityUid uid, StationAiApcControllableComponent comp, StationAiApcToggleEvent args)
    {
        if (!comp.Hacked)
        {
            _popup.PopupEntity(Loc.GetString("station-ai-apc-not-hacked"), args.User, args.User, PopupType.MediumCaution);
            return;
        }

        // Toggle puro do estado real; o espelhamento de PowerOn vem do ApcMainBreakerChangedEvent.
        _apc.ApcToggleBreaker(uid, user: args.User);
    }
```
Adicionar os métodos novos:
```csharp
    private void OnHack(EntityUid uid, StationAiApcControllableComponent comp, StationAiApcHackEvent args)
    {
        if (comp.Hacked)
            return;

        comp.Hacked = true;
        comp.HackedBy = args.User;
        Dirty(uid, comp);

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
        _popup.PopupEntity(Loc.GetString("station-ai-apc-hacked"), args.User, args.User, PopupType.Medium);
    }

    private void OnApcExamined(EntityUid uid, StationAiApcControllableComponent comp, ref ExaminedEvent args)
    {
        if (comp.Hacked && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("station-ai-apc-compromised"));
    }

    private void OnApcTerminating(EntityUid uid, StationAiApcControllableComponent comp, ref EntityTerminatingEvent args)
    {
        if (!comp.Hacked)
            return;

        // A APC sumiu: a IA perde essa fonte de taxa (mas mantém a CPU já acumulada).
        if (TryComp<StationAiCpuComponent>(comp.HackedBy, out var cpu) && cpu.HackedApcCount > 0)
        {
            cpu.HackedApcCount--;
            Dirty(comp.HackedBy, cpu);
        }
    }
```

- [ ] **Step 5: FTL das novas strings da APC**

Em `Resources/Locale/en-US/silicons/station-ai.ftl`:
```
station-ai-apc-hacked = APC hacked. It now feeds your CPU.
station-ai-apc-not-hacked = You must hack this APC first.
station-ai-apc-compromised = Its internals look [color=red]compromised[/color] — tampered with.
ai-apc-hack = Hack APC
```
Em `Resources/Locale/pt-BR/silicons/station-ai.ftl`:
```
station-ai-apc-hacked = APC hackeada. Agora ela alimenta sua CPU.
station-ai-apc-not-hacked = Você precisa hackear esta APC primeiro.
station-ai-apc-compromised = O interior parece [color=red]comprometido[/color] — adulterado.
ai-apc-hack = Hackear APC
```

- [ ] **Step 6: Build de verificação**

Run: `dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: BUILD SUCCEEDED, sem RA0049/RA0051.

- [ ] **Step 7: Commit**

```bash
git add Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs Content.Shared/Silicons/StationAi/StationAiApcEvents.cs Content.Server/Silicons/StationAi/StationAiApcSystem.cs Resources/Locale/en-US/silicons/station-ai.ftl Resources/Locale/pt-BR/silicons/station-ai.ftl
git commit -m "feat(malf): hackear APC (fonte de CPU + tell + examine + decremento)"
```

---

## Task 7: Radial e tell visual da APC (cliente)

**Files:**
- Modify: `Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs`
- Modify: `Content.Client/Silicons/StationAi/StationAiSystem.cs`

- [ ] **Step 1: Radial da APC — "Hackear" antes, toggle depois**

Substituir o corpo de `OnApcGetRadial` em `StationAiSystem.Apc.cs` por:
```csharp
    private void OnApcGetRadial(Entity<StationAiApcControllableComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Sob lei hostil e ainda não hackeada: a única opção é Hackear.
        if (LocalAiIsHostile() && !ent.Comp.Hacked)
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "turn_off"),
                Tooltip = Loc.GetString("ai-apc-hack"),
                Event = new StationAiApcHackEvent(),
            });
            return;
        }

        // Hackeada (ou IA leal, que nunca hackeia): cortar/restaurar energia.
        var powerOn = ent.Comp.PowerOn;
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, powerOn ? "turn_off" : "turn_on"),
            Tooltip = Loc.GetString(powerOn ? "ai-apc-power-off" : "ai-apc-power-on"),
            Event = new StationAiApcToggleEvent(),
        });
    }
```
Observação: `_aiCustomRsi` já é acessível (definido em `StationAiSystem.Borg.cs`, mesma classe parcial). O sprite "turn_off" é placeholder para o botão Hackear; o usuário pode trocar por arte dedicada depois.

- [ ] **Step 2: Visualizer do tell — recolorir a APC hackeada**

Em `Content.Client/Silicons/StationAi/StationAiSystem.cs`, registrar a subscrição no fim de `Initialize`:
```csharp
        SubscribeLocalEvent<StationAiApcControllableComponent, AppearanceChangeEvent>(OnApcAppearanceChange);
```
E adicionar o método (usa o `_sprite` já existente na classe):
```csharp
    private void OnApcAppearanceChange(Entity<StationAiApcControllableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Tell visual placeholder: tinge a APC hackeada de vermelho-azulado até a arte dedicada.
        // O usuário pode trocar por um layer/estado próprio na RSI da APC.
        var hacked = _appearance.TryGetData<bool>(ent.Owner, StationAiApcVisuals.Hacked, out var v, args.Component) && v;
        args.Sprite.Color = hacked ? new Color(0.6f, 0.7f, 1f) : Color.White;
    }
```
Garantir o `using Robust.Shared.Maths;` no topo (para `Color`) — se já houver `using Robust.Shared...` que cubra Color, o build dirá.

- [ ] **Step 3: Build de verificação**

Run: `dotnet build Content.Client/Content.Client.csproj -c Debug`
Expected: BUILD SUCCEEDED.

- [ ] **Step 4: Commit**

```bash
git add Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs Content.Client/Silicons/StationAi/StationAiSystem.cs
git commit -m "feat(malf): radial Hackear APC + tell visual no cliente"
```

---

## Task 8: UX de CPU no radial (custo no tooltip + cinza sem saldo)

**Files:**
- Modify: `Content.Client/Silicons/StationAi/StationAiBoundUserInterface.cs`

- [ ] **Step 1: Mostrar custo e cinzar ações inacessíveis**

Em `StationAiBoundUserInterface.cs`, substituir `ConvertToButtons` por (lê a CPU local e anota cada botão pago):
```csharp
    private IEnumerable<RadialMenuActionOptionBase> ConvertToButtons(IReadOnlyList<StationAiRadial> actions)
    {
        // CPU atual da IA local (se for Malf). Usada para anotar custo e cinzar o que não dá pra pagar.
        float? cpu = null;
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        if (player != null && EntMan.TryGetComponent<StationAiCpuComponent>(player.Value, out var cpuComp))
            cpu = cpuComp.Cpu;

        var models = new RadialMenuActionOptionBase[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var cost = action.Event.CpuCost;

            var tooltip = action.Tooltip;
            Color? bg = null;
            if (cost > 0f && cpu != null)
            {
                var afford = cpu.Value >= cost;
                tooltip = $"{action.Tooltip} ({(int) cost} CPU)";
                if (!afford)
                {
                    tooltip = $"{action.Tooltip} ({(int) cost} CPU — {Loc.GetString("station-ai-cpu-low")})";
                    bg = new Color(0.25f, 0.25f, 0.25f); // cinza: sem saldo (servidor nega ao clicar)
                }
            }

            models[i] = new RadialMenuActionOption<BaseStationAiAction>(HandleRadialMenuClick, action.Event)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(action.Sprite),
                ToolTip = tooltip,
                BackgroundColor = bg,
            };
        }

        return models;
    }
```
Garantir os `using` no topo do arquivo:
```csharp
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Maths;
```

- [ ] **Step 2: FTL do rótulo "sem saldo"**

Em `Resources/Locale/en-US/silicons/station-ai.ftl`:
```
station-ai-cpu-low = not enough CPU
```
Em `Resources/Locale/pt-BR/silicons/station-ai.ftl`:
```
station-ai-cpu-low = CPU insuficiente
```

- [ ] **Step 3: Build de verificação**

Run: `dotnet build Content.Client/Content.Client.csproj -c Debug`
Expected: BUILD SUCCEEDED.

- [ ] **Step 4: Commit**

```bash
git add Content.Client/Silicons/StationAi/StationAiBoundUserInterface.cs Resources/Locale/en-US/silicons/station-ai.ftl Resources/Locale/pt-BR/silicons/station-ai.ftl
git commit -m "feat(malf): radial mostra custo de CPU e cinza acoes sem saldo"
```

---

## Task 9: Build completo de release (sanidade)

**Files:** nenhum (verificação)

- [ ] **Step 1: Build limpo de servidor e cliente em Release**

Run:
```
cd ~/estacao-honk/space-station-14
dotnet build Content.Server/Content.Server.csproj -c Release
dotnet build Content.Client/Content.Client.csproj -c Release
```
Expected: ambos BUILD SUCCEEDED, sem warnings RA0049/RA0051.

- [ ] **Step 2: (se falhar) corrigir e recompilar antes de seguir**

Não prosseguir para o staging com build quebrado.

---

## Task 10: Validação em jogo no staging + nota no vault

**Files:** nenhum (teste manual; padrão do fork — `ss14-staging-server`)

- [ ] **Step 1: Subir no staging (porta 1213)**

Deploy do build para o staging conforme `ss14-staging-server` / skill `deploy-ss14` (set COMPLETO de DLLs para evitar mismatch de Robust). NÃO ir para produção (1212) ainda.

- [ ] **Step 2: Virar IA Malf e checar os critérios de aceite**

No staging, usar o verbo de admin "Tornar IA Malf" (de `ss14-malf-ai-design`). Verificar, um a um:
1. [ ] Alert de CPU aparece no HUD da IA e sobe devagar SEM APC hackeada (~6/min).
2. [ ] No radial de uma APC, sob lei hostil, aparece só "Hackear APC". Após hackear: a APC fica visivelmente alterada (tinta) e examinar mostra "comprometido".
3. [ ] Depois de hackear, a taxa de regen de CPU acelera; cortar/restaurar energia só aparece no radial APÓS hackear.
4. [ ] Ações mostram o custo no tooltip; com CPU < custo a ação fica cinza e, ao clicar, o servidor nega com popup "CPU insuficiente".
5. [ ] Gastar uma ação debita a CPU e o alert/examine refletem o novo valor.
6. [ ] Ações de estação inteira custam 75 e drenam ~38% do pote.
7. [ ] Destruir uma APC hackeada reduz a taxa de regen (a CPU já acumulada permanece).

- [ ] **Step 3: Afinar custos/regen se necessário**

Ajustar os números (constantes nos eventos / defaults do componente) conforme o teste, recompilar e repetir o Step 2. Este é o eixo principal de tuning (ver spec).

- [ ] **Step 4: Propor nota no vault (obrigatório antes de encerrar — CLAUDE.md)**

Propor ao usuário atualizar:
- `~/honk-memory/projects/estacao-honk/decisoes.md` — nova decisão D-0xx (Fase 3 Bloco 1: economia de CPU + hackear APC).
- Memória `ss14-malf-ai-design` — marcar Bloco 1 como implementado/em staging, com os números finais.
Após aprovação do usuário e commit no vault, o hook reindexará o RAG.

- [ ] **Step 5: (após autorização explícita) deploy em produção**

Só com OK do usuário: deploy para 1212 com o set completo de DLLs + DLL nova na produção (Resources symlink). Ver `feedback-deploy-robust-mismatch`.

---

## Notas de revisão (self-review do plano)

- **Cobertura do spec:** CPU acumulada+APC=taxa (Task 1/3), hackear pré-requisito+fonte+tell visual+examine (Task 6/7), todas as ações custam CPU (Task 4/5), UI radial custo/cinza + alert HUD + examine core (Task 3/8). Deploy/staging (Task 9/10). Tudo coberto.
- **Custos conferem com o spec revisado:** porta única/área 3, estação 75, torreta letal 10, subverter 30, desligar/imobilizar 10, detonar 50, pânico atmos 75, hackear/energia 0.
- **Consistência de tipos:** `CpuCost` (float) definido em `BaseStationAiAction` e sobrescrito em todos; `TryConsume(EntityUid, float)`; `StationAiCpuComponent.HackedApcCount` (int) mexido só em Task 6; `StationAiApcVisuals.Hacked` usado em Task 6 (set) e Task 7 (get).
- **Sem TDD unitário:** decisão consciente — segue o padrão do fork (build + staging), documentada no topo. Tarefas de código terminam em build; Task 10 valida comportamento em jogo.
