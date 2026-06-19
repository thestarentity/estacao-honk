# Core Shunting (IA Malf — Fase 3, Bloco 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar à IA Malf a habilidade de "shuntar" — esconder seu cérebro dentro de uma APC hackeada, sobrevivendo à destruição do núcleo, mas ficando dormente.

**Architecture:** Reusa a máquina de container do core. O shunt MOVE a entidade-cérebro do container `station_ai_mind_slot` do núcleo para um container dedicado `station_ai_shunt_slot` na APC hospedeira. Sair do container do núcleo já dispara `OnAiRemove` (olho some, núcleo mostra "vazio"); voltar dispara `OnAiInsert` (olho religado). Sem olho = sem visão/ação = dormente. Um componente marcador no cérebro bloqueia o radial e guarda a APC. A morte da APC mata a IA; a morte do núcleo a prende para sempre.

**Tech Stack:** C# (Content.Shared / Content.Server / Content.Client), RobustToolbox ECS, Fluent (FTL en-US + pt-BR), YAML de protótipo.

## Global Constraints

- **Responder ao usuário sempre em pt-BR.** Comentários de código e textos de jogo: pt-BR (FTL pt-BR) + en-US (FTL en-US, fonte).
- **Commits:** sem `Co-Authored-By`, sem emojis, sem marcadores de IA (regra absoluta do projeto).
- **CPU central:** todo gasto passa por `StationAiCpuSystem.TryConsume(EntityUid ai, float cost)`. Custo do shunt = `50f` (constante, ajustável).
- **Bloqueio de lei:** o shunt é uma ação de CPU gated por lei hostil — o radial cliente já só mostra ações perigosas via `LocalAiIsHostile()`; não criar gate novo.
- **Deploy:** C# em Shared+Server+Client → set COMPLETO de DLLs (mismatch de Robust). Staging (1213) OBRIGATÓRIO antes de produção. Backup `.bak-pre-shunt`.
- **Verificação por build:** este domínio (mover cérebros entre containers no engine) não tem teste unitário barato; cada task termina com `dotnet build` 0 erros, e o teste real é o roteiro manual no staging (Task 7). Onde uma assinatura do engine é incerta, o passo manda confirmar pelo build.
- **Container do cérebro:** núcleo usa `StationAiCoreComponent.Container == "station_ai_mind_slot"`. O shunt usa um container novo `"station_ai_shunt_slot"` na APC.

---

### Task 1: Marcador, eventos e flag de APC (Shared)

**Files:**
- Create: `Content.Shared/Silicons/StationAi/StationAiShuntedComponent.cs`
- Create: `Content.Shared/Silicons/StationAi/StationAiShuntEvents.cs`
- Modify: `Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs` (add `Occupied` flag + `Occupied` visual)

**Interfaces:**
- Produces: `StationAiShuntedComponent { EntityUid? HostApc; bool CoreLost; }`; events `StationAiApcShuntEvent : BaseStationAiAction` (CpuCost=50) e `StationAiReturnFromShuntEvent : InstantActionEvent`; `StationAiApcControllableComponent.Occupied` (bool, networked); `StationAiApcVisuals.Occupied`.

- [ ] **Step 1: Criar o componente marcador**

`Content.Shared/Silicons/StationAi/StationAiShuntedComponent.cs`:
```csharp
using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Marca o cérebro de uma IA Malf que está SHUNTADO dentro de uma APC hackeada
/// (Fase 3, Bloco 2 — core shunting). Enquanto presente, a IA é dormente: o radial
/// e todas as ações ficam bloqueados. A IA sobrevive à destruição do núcleo, mas
/// fica presa na APC. Mora na entidade-cérebro (a mesma de leis/CPU).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiShuntedComponent : Component
{
    /// <summary>APC onde o cérebro está escondido. O cérebro fica no container "station_ai_shunt_slot" dela.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HostApc;

    /// <summary>True quando o núcleo foi destruído enquanto shuntada: ela fica presa, sem poder voltar.</summary>
    [DataField, AutoNetworkedField]
    public bool CoreLost;
}
```

- [ ] **Step 2: Criar os eventos do shunt**

`Content.Shared/Silicons/StationAi/StationAiShuntEvents.cs`:
```csharp
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Ação da IA Malf no radial de uma APC hackeada: shuntar o cérebro para dentro dela.
/// Chega ao servidor via StationAiRadialMessage e é levantada na APC.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiApcShuntEvent : BaseStationAiAction
{
    public override float CpuCost => 50f;
}

/// <summary>
/// Ação instantânea concedida ao cérebro shuntado para VOLTAR ao núcleo.
/// Removida quando o núcleo é destruído (CoreLost).
/// </summary>
public sealed partial class StationAiReturnFromShuntEvent : InstantActionEvent
{
}
```

- [ ] **Step 3: Adicionar flag `Occupied` e visual à APC controlável**

Em `Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs`, dentro da classe (após `Hacked`):
```csharp
    /// <summary>
    /// APC que está hospedando uma IA shuntada (Bloco 2). Networked para o tell visual sutil
    /// e para o radial esconder "Shuntar" numa APC já ocupada.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Occupied;
```
E no enum `StationAiApcVisuals`, adicionar o estado:
```csharp
public enum StationAiApcVisuals : byte
{
    Hacked,
    Occupied,
}
```

- [ ] **Step 4: Build**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Shared/Content.Shared.csproj -c Release`
Expected: Build succeeded, 0 erros.

- [ ] **Step 5: Commit**

```bash
git add Content.Shared/Silicons/StationAi/StationAiShuntedComponent.cs Content.Shared/Silicons/StationAi/StationAiShuntEvents.cs Content.Shared/Silicons/StationAi/StationAiApcControllableComponent.cs
git commit -m "feat(malf): marcador, eventos e flag de APC para o core shunting"
```

---

### Task 2: Sistema de shunt — shuntar e voltar (Server)

**Files:**
- Create: `Content.Server/Silicons/StationAi/StationAiShuntSystem.cs`
- Reference (não modificar): `StationAiCpuSystem.TryConsume`, `SharedStationAiSystem` (`TryGetCore`), `StationAiCoreComponent.Container`.

**Interfaces:**
- Consumes: `StationAiShuntedComponent`, `StationAiApcShuntEvent`, `StationAiReturnFromShuntEvent`, `StationAiApcControllableComponent.Occupied`, `StationAiApcVisuals.Occupied` (Task 1); `StationAiCpuSystem.TryConsume` (existente).
- Produces: `StationAiShuntSystem` com `bool TryShunt(EntityUid brain, EntityUid apc)` e `void ReturnToCore(EntityUid brain)`; constante de container `ShuntContainer = "station_ai_shunt_slot"`; ação concedida via `ActionsSystem` para voltar.

- [ ] **Step 1: Esqueleto do sistema com shuntar**

`Content.Server/Silicons/StationAi/StationAiShuntSystem.cs`:
```csharp
using Content.Server.Silicons.StationAi;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Containers;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Core shunting da IA Malf (Fase 3, Bloco 2). Move o cérebro da IA do container do núcleo
/// (station_ai_mind_slot) para um container na APC hackeada (station_ai_shunt_slot). Sair do
/// container do núcleo já dispara OnAiRemove (olho some, núcleo vazio); voltar dispara OnAiInsert.
/// Enquanto shuntada a IA é dormente (StationAiShuntedComponent bloqueia o radial em
/// SharedStationAiSystem.OnRadialMessage). Sobrevive à destruição do núcleo; morre com a APC.
/// </summary>
public sealed class StationAiShuntSystem : EntitySystem
{
    public const string ShuntContainer = "station_ai_shunt_slot";

    [Dependency] private readonly StationAiCpuSystem _cpu = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;

    /// <summary>Action prototype concedido ao cérebro para voltar ao núcleo. Definido no YAML da Task 6.</summary>
    public static readonly string ReturnActionId = "ActionStationAiReturnFromShunt";

    public override void Initialize()
    {
        base.Initialize();

        // Ação no radial da APC: shuntar.
        SubscribeLocalEvent<StationAiApcControllableComponent, StationAiApcShuntEvent>(OnShuntRequest);
        // Ação instantânea concedida ao cérebro: voltar ao núcleo.
        SubscribeLocalEvent<StationAiShuntedComponent, StationAiReturnFromShuntEvent>(OnReturnRequest);
    }

    private void OnShuntRequest(Entity<StationAiApcControllableComponent> apc, StationAiApcShuntEvent args)
    {
        var brain = args.User; // a entidade-cérebro (= ev.Actor, dona de leis/CPU)
        TryShunt(brain, apc.Owner);
    }

    private void OnReturnRequest(Entity<StationAiShuntedComponent> brain, StationAiReturnFromShuntEvent args)
    {
        if (brain.Comp.CoreLost)
            return; // núcleo destruído: não há para onde voltar.
        ReturnToCore(brain.Owner);
    }
```

- [ ] **Step 2: Implementar TryShunt**

Continuando a mesma classe:
```csharp
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
        if (!_stationAi.TryGetCore(brain, out var core))
            return false;

        var shuntSlot = _container.EnsureContainer<ContainerSlot>(apc, ShuntContainer);
        if (!_container.Insert(brain, shuntSlot)) // Insert remove do container antigo automaticamente
            return false;

        var shunted = AddComp<StationAiShuntedComponent>(brain);
        shunted.HostApc = apc;
        Dirty(brain, shunted);

        apcComp.Occupied = true;
        Dirty(apc, apcComp);
        _stationAi.SetApcOccupiedVisual(apc, true);

        // Concede a ação de voltar ao núcleo.
        _actions.AddAction(brain, ReturnActionId);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(brain):user} shuntou para a APC {ToPrettyString(apc):target}.");
        _popup.PopupEntity(Loc.GetString("station-ai-shunt-done"), brain, brain, PopupType.Medium);
        return true;
    }
```
> Nota de implementação (confirmar no build): `_container.Insert` e `EnsureContainer<ContainerSlot>` são as APIs de `SharedContainerSystem`. Se a assinatura divergir, ajustar para a usada por `OnIntellicardDoAfter` em `SharedStationAiSystem.cs` (mesmo movimento de cérebro entre slots).

- [ ] **Step 3: Implementar ReturnToCore**

```csharp
    /// <summary>Volta o cérebro ao container do núcleo (dispara OnAiInsert: olho religado).</summary>
    public void ReturnToCore(EntityUid brain)
    {
        if (!TryComp<StationAiShuntedComponent>(brain, out var shunted) || shunted.HostApc == null)
            return;

        var apc = shunted.HostApc.Value;

        // Acha o núcleo da IA. Como o cérebro está fora do núcleo, localizamos pelo CoreLost==false:
        // o núcleo é a entidade com StationAiCoreComponent cujo container está vazio e que pertence a esta IA.
        // Para o Bloco 2, usamos a referência guardada no shunt (Task 3 grava HostCore). Sem ela, aborta.
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

        _actions.RemoveAction(brain, _actions.GetAction(brain, ReturnActionId)?.Owner);
        RemComp<StationAiShuntedComponent>(brain);
        RemComp<StationAiShuntReturnComponent>(brain);
    }
}
```
> A referência ao núcleo (`StationAiShuntReturnComponent.Core`) é criada na Task 3, que também grava o núcleo no momento do shunt. Para manter a Task 2 compilando isoladamente, declare o componente mínimo agora (Step 4).

- [ ] **Step 4: Componente server-only para guardar o núcleo de origem**

No fim do arquivo (server-only, não networked):
```csharp
/// <summary>Guarda, no cérebro shuntado, qual era o núcleo de origem (para voltar). Server-only.</summary>
[RegisterComponent]
public sealed partial class StationAiShuntReturnComponent : Component
{
    [DataField]
    public EntityUid? Core;
}
```
E em `TryShunt` (Step 2), logo após achar o `core`, antes do Insert, gravar:
```csharp
        var ret = EnsureComp<StationAiShuntReturnComponent>(brain);
        ret.Core = core.Owner;
```

- [ ] **Step 5: Stub de `SetApcOccupiedVisual` no Shared**

Em `SharedStationAiSystem` (arquivo `SharedStationAiSystem.cs`), adicionar método público (usa `_appearance` já injetado no sistema):
```csharp
    /// <summary>Liga/desliga o tell visual sutil da APC ocupada por uma IA shuntada (Bloco 2).</summary>
    public void SetApcOccupiedVisual(EntityUid apc, bool occupied)
    {
        _appearance.SetData(apc, StationAiApcVisuals.Occupied, occupied);
    }
```
> Confirmar no build que `_appearance` é o nome do `SharedAppearanceSystem` injetado em `SharedStationAiSystem`. Se for outro nome, usar o existente.

- [ ] **Step 6: Build**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: Build succeeded, 0 erros. (Se faltar assinatura de container/ação, corrigir conforme o uso real em `SharedStationAiSystem.cs` e `SharedActionsSystem`.)

- [ ] **Step 7: Commit**

```bash
git add Content.Server/Silicons/StationAi/StationAiShuntSystem.cs Content.Shared/Silicons/StationAi/SharedStationAiSystem.cs
git commit -m "feat(malf): sistema de core shunting (shuntar e voltar ao nucleo)"
```

---

### Task 3: Destino do núcleo e da APC (Server)

**Files:**
- Modify: `Content.Server/Silicons/StationAi/StationAiShuntSystem.cs` (handlers de destruição)

**Interfaces:**
- Consumes: `StationAiShuntedComponent`, `StationAiShuntReturnComponent` (Task 2); `EntityTerminatingEvent`.
- Produces: regra "núcleo destruído → CoreLost"; regra "APC ocupada destruída → IA morre".

- [ ] **Step 1: Detectar destruição do núcleo de origem**

Em `Initialize()` do `StationAiShuntSystem`, adicionar:
```csharp
        SubscribeLocalEvent<StationAiCoreComponent, EntityTerminatingEvent>(OnCoreTerminating);
        SubscribeLocalEvent<StationAiApcControllableComponent, EntityTerminatingEvent>(OnHostApcTerminating);
```
E os handlers:
```csharp
    private void OnCoreTerminating(Entity<StationAiCoreComponent> core, ref EntityTerminatingEvent args)
    {
        // Se alguma IA shuntada tem este núcleo como origem, ela fica presa (CoreLost).
        var query = EntityQueryEnumerator<StationAiShuntedComponent, StationAiShuntReturnComponent>();
        while (query.MoveNext(out var brain, out var shunted, out var ret))
        {
            if (ret.Core != core.Owner || shunted.CoreLost)
                continue;

            shunted.CoreLost = true;
            Dirty(brain, shunted);

            // Tira a ação de voltar: não há mais núcleo.
            _actions.RemoveAction(brain, _actions.GetAction(brain, ReturnActionId)?.Owner);
            _popup.PopupEntity(Loc.GetString("station-ai-shunt-core-lost"), brain, brain, PopupType.LargeCaution);
        }
    }
```

- [ ] **Step 2: Matar a IA quando a APC ocupada é destruída**

```csharp
    private void OnHostApcTerminating(Entity<StationAiApcControllableComponent> apc, ref EntityTerminatingEvent args)
    {
        if (!apc.Comp.Occupied)
            return;

        // Acha o cérebro shuntado nesta APC e o mata (a APC sumindo = fim da caça).
        var query = EntityQueryEnumerator<StationAiShuntedComponent>();
        while (query.MoveNext(out var brain, out var shunted))
        {
            if (shunted.HostApc != apc.Owner)
                continue;

            // O cérebro está no container da APC que está terminando; QueueDel garante a morte.
            // O caminho padrão de mente/fantasma dispara ao deletar a entidade-cérebro.
            CleanupShunt(brain, apc.Owner);
            QueueDel(brain);
        }
    }
```
> Verificar no teste de staging que deletar o cérebro produz o fluxo de morte/fantasma esperado (mente vira fantasma). Se o engine apenas "derrubar" o cérebro em vez de deletá-lo junto com a APC, o `QueueDel` explícito cobre o caso.

- [ ] **Step 3: Build**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: Build succeeded, 0 erros.

- [ ] **Step 4: Commit**

```bash
git add Content.Server/Silicons/StationAi/StationAiShuntSystem.cs
git commit -m "feat(malf): nucleo destruido prende a IA shuntada; APC destruida a mata"
```

---

### Task 4: Bloquear ações enquanto shuntada (Shared)

**Files:**
- Modify: `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Held.cs` (`OnRadialMessage`)

**Interfaces:**
- Consumes: `StationAiShuntedComponent` (Task 1).
- Produces: nenhuma ação do radial é executada enquanto o ator tem `StationAiShuntedComponent`.

- [ ] **Step 1: Negar ações no ponto central**

Em `OnRadialMessage` (`SharedStationAiSystem.Held.cs`), logo após `ev.Event.User = ev.Actor;` e ANTES do gasto de CPU:
```csharp
        // Bloco 2: IA shuntada é dormente — nenhuma ação do radial funciona.
        if (HasComp<StationAiShuntedComponent>(ev.Actor))
            return;
```

- [ ] **Step 2: Build**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Shared/Content.Shared.csproj -c Release`
Expected: Build succeeded, 0 erros.

- [ ] **Step 3: Commit**

```bash
git add Content.Shared/Silicons/StationAi/SharedStationAiSystem.Held.cs
git commit -m "feat(malf): IA shuntada nao executa nenhuma acao do radial"
```

---

### Task 5: Radial, ação de voltar e tell visual (Client)

**Files:**
- Modify: `Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs` (opção "Shuntar núcleo")
- Create: `Content.Client/Silicons/StationAi/StationAiApcVisualizerSystem.cs` (tell visual da APC ocupada) — OU estender o visualizer existente se houver
- Modify: server `StationAiApcSystem.OnApcExamined` (examine "atividade anômala")

**Interfaces:**
- Consumes: `StationAiApcControllableComponent.Hacked/Occupied`, `StationAiApcShuntEvent`, `StationAiApcVisuals.Occupied`, `LocalAiIsHostile()` (existentes).
- Produces: botão "Shuntar núcleo" no radial; tinte/sinal na APC ocupada; linha de examine.

- [ ] **Step 1: Adicionar "Shuntar núcleo" ao radial da APC**

Em `OnApcGetRadial` (`Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs`), no bloco de APC hackeada (após o toggle de energia, dentro do caminho `Hacked`), adicionar — só se hostil e não ocupada:
```csharp
        // Bloco 2: shuntar o núcleo para dentro desta APC (só Malf, APC hackeada e livre).
        if (LocalAiIsHostile() && ent.Comp.Hacked && !ent.Comp.Occupied)
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "hackapc"), // placeholder; ícone dedicado depois
                Tooltip = Loc.GetString("ai-apc-shunt"),
                Event = new StationAiApcShuntEvent(),
            });
        }
```
> Reaproveita o sprite "hackapc" como placeholder (o usuário desenha o ícone dedicado depois, como nos blocos anteriores).

- [ ] **Step 2: Tell visual da APC ocupada**

Verificar como o estado `Hacked` é desenhado no cliente:
Run: `grep -rn "StationAiApcVisuals" ~/estacao-honk/space-station-14/Content.Client/`
- Se existir um visualizer para `StationAiApcVisuals.Hacked`, ESTENDER esse mesmo arquivo para tratar `Occupied` (tinte mais forte / piscar leve, distinto do Hacked).
- Se o `Hacked` for desenhado só por `_appearance.SetData` + um `GenericVisualizerComponent` no YAML, adicionar um mapeamento YAML para `Occupied` no protótipo da APC (Task 6) e NÃO criar arquivo cliente.

Implementar conforme o que o grep revelar. Resultado exigido: a APC ocupada fica visivelmente distinta de uma APC só hackeada (tell sutil).

- [ ] **Step 3: Examine "atividade anômala" na APC ocupada**

Em `StationAiApcSystem.OnApcExamined` (server), após a linha do `comp.Hacked`:
```csharp
        if (comp.Occupied && args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("station-ai-apc-anomalous"));
```

- [ ] **Step 4: Build (Client + Server)**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Client/Content.Client.csproj -c Release && dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: Build succeeded, 0 erros nos dois.

- [ ] **Step 5: Commit**

```bash
git add Content.Client/Silicons/StationAi/ Content.Server/Silicons/StationAi/StationAiApcSystem.cs
git commit -m "feat(malf): radial Shuntar nucleo + tell visual e examine da APC ocupada"
```

---

### Task 6: Ação YAML e FTL (en-US + pt-BR)

**Files:**
- Create: `Resources/Prototypes/Actions/station_ai_shunt.yml` (action `ActionStationAiReturnFromShunt`)
- Modify: `Resources/Locale/en-US/silicons/station-ai.ftl` (chaves novas)
- Modify: `Resources/Locale/pt-BR/silicons/station-ai.ftl` (chaves novas)
- Possível: protótipo da APC para o mapeamento visual de `Occupied` (se Task 5 Step 2 exigir).

**Interfaces:**
- Consumes: `StationAiReturnFromShuntEvent` (Task 1), `StationAiShuntSystem.ReturnActionId == "ActionStationAiReturnFromShunt"` (Task 2).
- Produces: action prototype + todas as chaves de FTL referenciadas no plano.

- [ ] **Step 1: Action prototype de voltar ao núcleo**

Confirmar o caminho real do FTL/Actions:
Run: `ls ~/estacao-honk/space-station-14/Resources/Prototypes/Actions/ | grep -i ai && grep -rln "job-name-station-ai\|ai-apc-hack" ~/estacao-honk/space-station-14/Resources/Locale/en-US/`

`Resources/Prototypes/Actions/station_ai_shunt.yml` (espelhar a estrutura de uma `entity` action existente, ex. a de JumpToCore):
```yaml
- type: entity
  id: ActionStationAiReturnFromShunt
  name: Voltar ao núcleo
  description: Retorna seu processo do esconderijo na APC para o núcleo da IA.
  components:
  - type: InstantAction
    event: !type:StationAiReturnFromShuntEvent
    useDelay: 1
    icon:
      sprite: Interface/Actions/actions_ai.rsi
      state: jump_to_core
```
> Ajustar `icon` para um state real do RSI de ações da IA (confirmar com `ls Resources/Textures/Interface/Actions/actions_ai.rsi/`). Espelhar o formato exato de uma action `entity` existente do repo (campos obrigatórios podem variar).

- [ ] **Step 2: FTL en-US**

Em `Resources/Locale/en-US/silicons/station-ai.ftl` (ou no arquivo que `ai-apc-hack` usa — confirmado no Step 1), adicionar:
```ftl
ai-apc-shunt = Shunt core here
station-ai-shunt-apc-not-hacked = This APC isn't hacked.
station-ai-shunt-done = You shunt your core into the APC. You are hidden, but dormant.
station-ai-shunt-return = You return your process to the core.
station-ai-shunt-core-lost = Your core is gone. You are trapped in the APC.
station-ai-apc-anomalous = There is anomalous processing activity inside.
```

- [ ] **Step 3: FTL pt-BR**

Em `Resources/Locale/pt-BR/silicons/station-ai.ftl`, adicionar (texto natural, sem cara de IA):
```ftl
ai-apc-shunt = Shuntar núcleo aqui
station-ai-shunt-apc-not-hacked = Esta APC não está hackeada.
station-ai-shunt-done = Você esconde seu núcleo na APC. Está escondida, mas dormente.
station-ai-shunt-return = Você devolve seu processo ao núcleo.
station-ai-shunt-core-lost = Seu núcleo se foi. Você está presa na APC.
station-ai-apc-anomalous = Há uma atividade de processamento anômala lá dentro.
```

- [ ] **Step 4: Build (carrega protótipos e valida YAML)**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Server/Content.Server.csproj -c Release`
Expected: Build succeeded. (Erros de protótipo aparecem no boot, validados na Task 7.)

- [ ] **Step 5: Commit**

```bash
git add Resources/Prototypes/Actions/station_ai_shunt.yml Resources/Locale/en-US/silicons/station-ai.ftl Resources/Locale/pt-BR/silicons/station-ai.ftl
git commit -m "feat(malf): acao de voltar do shunt + FTL en/pt do core shunting"
```

---

### Task 7: Build completo, deploy no staging e teste manual

**Files:** nenhum (integração + validação).

- [ ] **Step 1: Build completo da solução**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build -c Release`
Expected: Build succeeded, 0 erros.

- [ ] **Step 2: Deploy no staging (1213) com set completo de DLLs**

Usar o skill/rotina de deploy do projeto (`/deploy-ss14` para staging) — copia o set COMPLETO de DLLs (Shared+Server+Client), faz backup `.bak-pre-shunt`, reinicia o staging e valida o boot "Ready" sem erro de protótipo.

- [ ] **Step 3: Teste manual no staging (roteiro do spec)**

Confirmar no jogo:
1. Virar IA Malf (verbo de admin), hackear uma APC, **Shuntar núcleo** → custo de ~50 CPU debitado; núcleo fica "vazio"; popup de shunt.
2. Shuntada: **nenhuma** ação do radial funciona; só a ação "Voltar ao núcleo" está disponível.
3. **Voltar ao núcleo** → reativação normal (olho volta, radial volta).
4. Shuntar de novo, **destruir o núcleo** → ela sobrevive presa; a ação "Voltar" some; popup de núcleo perdido.
5. **Destruir a APC ocupada** → a IA morre (fantasma / reação do round).
6. **Tell sutil:** a APC ocupada se distingue visualmente das outras hackeadas + examine "atividade anômala".

- [ ] **Step 4: Registrar resultado**

Se tudo passar: anotar no plano que o staging validou, e PARAR (deploy em produção exige autorização explícita do usuário — não é automático). Se algo falhar: usar `superpowers:systematic-debugging`, corrigir, rebuildar e repetir o Step 2.

---

## Notas de verificação pós-plano

- **Cobertura do spec:** Task 1 (decisões 6 + flags), Task 2 (decisões 1,2,4 — shuntar/voltar + custo + núcleo vazio via OnAiRemove), Task 3 (decisões 3,5 — núcleo perdido / APC destruída), Task 4 (decisão 2 — dormente), Task 5 (decisão 6 — tell sutil + radial), Task 6 (FTL/ação), Task 7 (deploy/teste staging obrigatório). Todas as 6 decisões fechadas têm task.
- **Pontos a confirmar no build/staging (sinalizados nos steps):** assinatura exata de `_container.Insert`/`EnsureContainer<ContainerSlot>` (espelhar `OnIntellicardDoAfter`); nome do `SharedAppearanceSystem` em `SharedStationAiSystem`; API de `SharedActionsSystem.AddAction/RemoveAction/GetAction`; estrutura exata de uma action `entity` no YAML; arquivo FTL real de `ai-apc-hack`; se `Hacked` usa visualizer cliente ou GenericVisualizer YAML.
- **Após o Bloco 2:** propor nota no vault (`decisoes.md` D-novo + `expansao-ia-radial.md`) conforme o CLAUDE.md.
