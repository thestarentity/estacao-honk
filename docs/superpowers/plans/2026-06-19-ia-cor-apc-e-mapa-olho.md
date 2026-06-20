# IA: cor da APC só na tela + mapa que move o olho — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o tell visual da APC hackeada/ocupada tingir apenas a tela (+ a luz) em vez do sprite inteiro, e permitir que a IA clique no radar ou no monitor de tripulação para teletransportar seu olho até o ponto clicado.

**Architecture:** Parte 1 é uma correção client-side no visualizador da APC da IA (tinge a camada `ChargeState` e a point light, em vez de `Sprite.Color`). Parte 2 adiciona um evento de rede dedicado (`StationAiMoveEyeEvent`): os dois controles de mapa já existentes (`ShuttleNavControl` do radar e `NavMapControl` do monitor) convertem o clique em `EntityCoordinates` e disparam o evento; o servidor acha o núcleo da IA pela sessão e reposiciona a entidade remota (olho).

**Tech Stack:** C# / Space Station 14 (RobustToolbox ECS), Content.Client / Content.Shared / Content.Server.

## Global Constraints

- Responder ao usuário em **pt-BR**; comentários de código novos em pt-BR seguindo o estilo do fork.
- Commits: **sem** `Co-Authored-By`, **sem** emojis, **sem** marcadores de IA na mensagem.
- Nada vai para produção (1212) sem passar por **staging (porta 1213)** primeiro.
- Deploy de C# usa o skill `/deploy-ss14` (copia o set COMPLETO de DLLs para evitar mismatch de Robust).
- Não tocar em `Resources/Locale/en-US/` nem em arquivos de tradução.
- Verificação deste projeto é por **compilação + boot + teste manual no staging**, não por testes unitários — não há costura de teste para renderização de sprite e clique de UI. Cada task termina compilando o projeto afetado; a integração final é validada em staging com checklist manual.

---

## Estrutura de arquivos

| Arquivo | Responsabilidade | Ação |
|---|---|---|
| `Content.Client/Silicons/StationAi/StationAiSystem.cs` | Visualizador da APC da IA (Parte 1) + método cliente que dispara o evento de mover o olho (Parte 2) | Modificar |
| `Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs` | Evento de rede cliente→servidor com a coordenada clicada | Criar |
| `Content.Server/Silicons/StationAi/StationAiSystem.MoveEye.cs` | Handler servidor: acha o núcleo da IA e move a entidade remota | Criar |
| `Content.Server/Silicons/StationAi/StationAiSystem.cs` | Chamar `InitializeMoveEye()` no `Initialize` | Modificar (1 linha) |
| `Content.Client/Shuttles/BUI/RadarConsoleBoundUserInterface.cs` | Ligar o clique do radar ao mover-olho quando o dono for a IA | Modificar |
| `Content.Client/Pinpointer/UI/NavMapControl.cs` | Expor evento `OnMapClick` com a coordenada do clique | Modificar |
| `Content.Client/Medical/CrewMonitoring/CrewMonitoringBoundUserInterface.cs` | Ligar o clique do navmap ao mover-olho quando o dono for a IA | Modificar |

---

### Task 1: APC — tingir só a tela e a luz

**Files:**
- Modify: `Content.Client/Silicons/StationAi/StationAiSystem.cs` (subscription na linha ~34, handler `OnApcAppearanceChange` ~linhas 95-112, e dependências)

**Interfaces:**
- Consumes: `ApcVisualLayers.ChargeState` (enum em `Content.Client.Power.APC`, definido em `ApcVisualizerSystem.cs`); `SpriteSystem.LayerMapTryGet`/`LayerSetColor`; `SharedPointLightSystem.SetColor`; `ApcVisualizerSystem` (para ordenação `after`).
- Produces: nada para tasks seguintes (mudança isolada).

- [ ] **Step 1: Adicionar usings e a dependência da luz**

No topo de `Content.Client/Silicons/StationAi/StationAiSystem.cs`, adicionar o using do namespace do APC (logo após os usings existentes do bloco `using Content...`/`using Robust...`):

```csharp
using Content.Client.Power.APC;
```

Dentro da classe, junto às outras `[Dependency]` (perto da linha 14), adicionar:

```csharp
    [Dependency] private SharedPointLightSystem _lights = default!;
```

- [ ] **Step 2: Ordenar nosso handler depois do visualizador padrão da APC**

Localizar a subscrição (linha ~34):

```csharp
        SubscribeLocalEvent<StationAiApcControllableComponent, AppearanceChangeEvent>(OnApcAppearanceChange);
```

Substituir por (garante que nossa cor não seja sobrescrita pelo `ApcVisualizerSystem`, que também roda no `AppearanceChangeEvent`):

```csharp
        SubscribeLocalEvent<StationAiApcControllableComponent, AppearanceChangeEvent>(OnApcAppearanceChange,
            after: new[] { typeof(ApcVisualizerSystem) });
```

- [ ] **Step 3: Reescrever o handler para tingir só a tela + a luz**

Substituir o corpo do método `OnApcAppearanceChange` (o bloco que hoje faz `args.Sprite.Color = occupied ? ... : ... : Color.White;`) por:

```csharp
    private void OnApcAppearanceChange(Entity<StationAiApcControllableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Tell visual: só a TELA (e a luz) muda de cor; o corpo da APC fica intacto.
        // Occupied (hospedando IA shuntada): laranja-âmbar. Hacked (só fonte de CPU): vermelho.
        // Nenhum dos dois: tela/luz no comportamento padrão da APC.
        var occupied = _appearance.TryGetData<bool>(ent.Owner, StationAiApcVisuals.Occupied, out var ov, args.Component) && ov;
        var hacked   = _appearance.TryGetData<bool>(ent.Owner, StationAiApcVisuals.Hacked,   out var hv, args.Component) && hv;

        Color? tint = occupied ? new Color(1f, 0.55f, 0.1f)   // laranja-âmbar (hospedando IA)
                    : hacked   ? new Color(1f, 0.2f,  0.2f)   // vermelho (só hackeada)
                    : null;                                   // sem tell

        // Nunca tingir o sprite inteiro (resquício do comportamento antigo).
        args.Sprite.Color = Color.White;

        // Tinge apenas a camada da tela; sem tell, volta ao branco (deixa a imagem da tela normal).
        if (_sprite.LayerMapTryGet((ent.Owner, args.Sprite), ApcVisualLayers.ChargeState, out var screenLayer, false))
            _sprite.LayerSetColor((ent.Owner, args.Sprite), screenLayer, tint ?? Color.White);

        // Tinge a luz emitida junto com a tela. Sem tell, não mexemos: o visualizador padrão
        // (que roda antes de nós) já ajustou a cor da luz pela carga da APC.
        if (tint != null && TryComp<PointLightComponent>(ent.Owner, out var light))
            _lights.SetColor(ent.Owner, tint.Value, light);
    }
```

- [ ] **Step 4: Compilar o cliente**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Client/Content.Client.csproj -v quiet`
Expected: build sem erros (0 Error(s)). Se faltar using para `PointLightComponent`/`SharedPointLightSystem`, eles estão em `Robust.Shared.GameObjects` — o `using Robust.Client.GameObjects;` já presente cobre o `SpriteSystem`; adicionar `using Robust.Shared.GameObjects;` se o compilador reclamar.

- [ ] **Step 5: Commit**

```bash
cd ~/estacao-honk/space-station-14
git add Content.Client/Silicons/StationAi/StationAiSystem.cs
git commit -m "fix(ia): tell da APC tinge so a tela e a luz, nao o corpo"
```

---

### Task 2: Evento de rede `StationAiMoveEyeEvent`

**Files:**
- Create: `Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs`

**Interfaces:**
- Produces: `StationAiMoveEyeEvent(NetCoordinates target)` — classe `[Serializable, NetSerializable]` em `Content.Shared.Silicons.StationAi`, com campo público `NetCoordinates Target`. Consumida pelo cliente (Task 4 e 5, que a disparam) e pelo servidor (Task 3, que a recebe).

- [ ] **Step 1: Criar o arquivo do evento**

Criar `Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs`:

```csharp
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Disparado pelo cliente quando a IA clica num ponto do mapa (radar ou monitor de
/// tripulação) para teletransportar seu olho (a entidade remota do núcleo) até a
/// coordenada clicada. O servidor identifica a IA pela sessão que enviou o evento.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiMoveEyeEvent : EntityEventArgs
{
    public NetCoordinates Target;

    public StationAiMoveEyeEvent(NetCoordinates target)
    {
        Target = target;
    }
}
```

- [ ] **Step 2: Compilar o shared**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Shared/Content.Shared.csproj -v quiet`
Expected: build sem erros.

- [ ] **Step 3: Commit**

```bash
cd ~/estacao-honk/space-station-14
git add Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs
git commit -m "feat(ia): evento de rede para mover o olho da IA pela coordenada"
```

---

### Task 3: Servidor — mover o olho ao receber o evento

**Files:**
- Create: `Content.Server/Silicons/StationAi/StationAiSystem.MoveEye.cs`
- Modify: `Content.Server/Silicons/StationAi/StationAiSystem.cs` (chamar `InitializeMoveEye()` dentro de `Initialize`, após `base.Initialize();` na linha ~81)

**Interfaces:**
- Consumes: `StationAiMoveEyeEvent` (Task 2); `TryGetCore(EntityUid, out Entity<StationAiCoreComponent?>)` (público em `SharedStationAiSystem`); `SharedTransformSystem.SetCoordinates(EntityUid, EntityCoordinates)`; `GetCoordinates(NetCoordinates)` (helper de `EntitySystem`).
- Produces: o comportamento server-side; nada de assinaturas para outras tasks.

- [ ] **Step 1: Criar o handler no servidor**

Criar `Content.Server/Silicons/StationAi/StationAiSystem.MoveEye.cs`:

```csharp
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private readonly SharedTransformSystem _eyeXform = default!;

    private void InitializeMoveEye()
    {
        SubscribeNetworkEvent<StationAiMoveEyeEvent>(OnMoveEye);
    }

    private void OnMoveEye(StationAiMoveEyeEvent ev, EntitySessionEventArgs args)
    {
        // O ator anexado à sessão é o olho da IA (StationAiHeld dentro do núcleo).
        if (args.SenderSession.AttachedEntity is not { } actor)
            return;

        // Acha o núcleo e a entidade remota (holograma/olho) que deve ser movida.
        if (!TryGetCore(actor, out var core) || core.Comp?.RemoteEntity is not { } remote)
            return;

        var coords = GetCoordinates(ev.Target);
        if (!coords.IsValid(EntityManager))
            return;

        _eyeXform.SetCoordinates(remote, coords);
    }
}
```

Nota: se o compilador apontar que `SharedTransformSystem` já está injetado em outra partial do `StationAiSystem` server com este mesmo nome, reusar o campo existente e remover a linha `[Dependency] private readonly SharedTransformSystem _eyeXform = default!;`. O nome `_eyeXform` foi escolhido para não colidir.

- [ ] **Step 2: Chamar `InitializeMoveEye()` no Initialize**

Em `Content.Server/Silicons/StationAi/StationAiSystem.cs`, dentro de `public override void Initialize()`, logo após `base.Initialize();` (linha ~81), adicionar:

```csharp
        InitializeMoveEye();
```

- [ ] **Step 3: Compilar o servidor**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Server/Content.Server.csproj -v quiet`
Expected: build sem erros. `EntitySessionEventArgs` está em `Robust.Shared.GameObjects` (já no using); `SenderSession` é `ICommonSession` — não precisa using extra para acessar `.AttachedEntity`.

- [ ] **Step 4: Commit**

```bash
cd ~/estacao-honk/space-station-14
git add Content.Server/Silicons/StationAi/StationAiSystem.MoveEye.cs Content.Server/Silicons/StationAi/StationAiSystem.cs
git commit -m "feat(ia): servidor move o olho da IA para a coordenada clicada"
```

---

### Task 4: Cliente — radar dispara o mover-olho

**Files:**
- Modify: `Content.Client/Silicons/StationAi/StationAiSystem.cs` (adicionar método `MoveEyeTo`)
- Modify: `Content.Client/Shuttles/BUI/RadarConsoleBoundUserInterface.cs`

**Interfaces:**
- Consumes: `StationAiMoveEyeEvent` (Task 2); `ShuttleNavControl.OnRadarClick` (campo `Action<EntityCoordinates>?` já existente); `RadarConsoleWindow.RadarScreen` (controle nomeado público); `StationAiCoreComponent` (marca o dono como núcleo da IA); `GetNetCoordinates`/`RaiseNetworkEvent` (helpers de `EntitySystem`).
- Produces: `StationAiSystem.MoveEyeTo(EntityCoordinates coords)` — método público no sistema cliente, também consumido pela Task 5.

- [ ] **Step 1: Adicionar `MoveEyeTo` no sistema cliente**

Em `Content.Client/Silicons/StationAi/StationAiSystem.cs`, adicionar o método público à classe (perto do fim, antes de `public override void Shutdown()`):

```csharp
    /// <summary>
    /// Pede ao servidor para mover o olho da IA até a coordenada clicada num mapa.
    /// </summary>
    public void MoveEyeTo(EntityCoordinates coords)
    {
        RaiseNetworkEvent(new StationAiMoveEyeEvent(GetNetCoordinates(coords)));
    }
```

Se faltar using, adicionar no topo `using Robust.Shared.Map;` (para `EntityCoordinates`).

- [ ] **Step 2: Ligar o clique do radar (só quando o dono é a IA)**

Em `Content.Client/Shuttles/BUI/RadarConsoleBoundUserInterface.cs`, adicionar no topo os usings:

```csharp
using Content.Client.Silicons.StationAi;
using Content.Shared.Silicons.StationAi;
```

E no método `Open()`, após `_window = this.CreateWindow<RadarConsoleWindow>();`, adicionar:

```csharp
        // Fork: se este radar pertence ao núcleo da IA, clicar move o olho e fecha o mapa.
        if (EntMan.HasComponent<StationAiCoreComponent>(Owner))
        {
            _window.RadarScreen.OnRadarClick = coords =>
            {
                EntMan.System<StationAiSystem>().MoveEyeTo(coords);
                Close();
            };
        }
```

- [ ] **Step 3: Compilar o cliente**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Client/Content.Client.csproj -v quiet`
Expected: build sem erros.

- [ ] **Step 4: Commit**

```bash
cd ~/estacao-honk/space-station-14
git add Content.Client/Silicons/StationAi/StationAiSystem.cs Content.Client/Shuttles/BUI/RadarConsoleBoundUserInterface.cs
git commit -m "feat(ia): clique no radar move o olho da IA e fecha o mapa"
```

---

### Task 5: Cliente — monitor de tripulação dispara o mover-olho

**Files:**
- Modify: `Content.Client/Pinpointer/UI/NavMapControl.cs` (expor `OnMapClick` e invocá-lo no clique limpo)
- Modify: `Content.Client/Medical/CrewMonitoring/CrewMonitoringBoundUserInterface.cs`

**Interfaces:**
- Consumes: `NavMapControl.MapUid` (grade exibida); `CrewMonitoringWindow.NavMap` (controle nomeado público); `StationAiSystem.MoveEyeTo` (Task 4); `StationAiCoreComponent`.
- Produces: `NavMapControl.OnMapClick` — campo `Action<EntityCoordinates>?` invocado no clique limpo (sem arrasto). Nulo por padrão: nenhum console existente muda de comportamento.

- [ ] **Step 1: Expor `OnMapClick` no NavMapControl**

Em `Content.Client/Pinpointer/UI/NavMapControl.cs`, junto aos outros eventos públicos (perto da linha 41, ao lado de `TrackedEntitySelectedAction`), adicionar:

```csharp
    /// <summary>
    /// Fork: disparado num clique limpo (sem arrasto) com a coordenada clicada na grade
    /// exibida. Usado pela IA para mover o olho. Nulo por padrão — não afeta outros consoles.
    /// </summary>
    public Action<EntityCoordinates>? OnMapClick;
```

- [ ] **Step 2: Invocar `OnMapClick` no KeyBindUp**

Em `KeyBindUp` (linhas ~196-242), substituir o bloco do `if (args.Function == EngineKeyFunctions.UIClick)` por esta versão (preserva a seleção de entidade existente e adiciona a invocação do `OnMapClick`):

```csharp
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (_xform == null || _physics == null)
                return;

            // If the cursor has moved a significant distance, exit (é arrasto, não clique)
            if ((StartDragPosition - args.PointerLocation.Position).Length() > MinDragDistance)
                return;

            // Get the clicked position
            var offset = Offset + _physics.LocalCenter;
            var localPosition = args.PointerLocation.Position - GlobalPixelPosition;

            // Convert to a world position
            var unscaledPosition = (localPosition - MidPointVector) / MinimapScale;
            var worldPosition = Vector2.Transform(new Vector2(unscaledPosition.X, -unscaledPosition.Y) + offset, _transformSystem.GetWorldMatrix(_xform));

            // Fork: coordenada clicada na grade exibida (para mover o olho da IA).
            if (OnMapClick != null && MapUid != null)
            {
                var gridLocal = new Vector2(unscaledPosition.X, -unscaledPosition.Y) + offset;
                OnMapClick.Invoke(new EntityCoordinates(MapUid.Value, gridLocal));
            }

            // Find closest tracked entity in range
            if (TrackedEntitySelectedAction == null || TrackedEntities.Count == 0)
                return;

            var closestEntity = NetEntity.Invalid;
            var closestDistance = float.PositiveInfinity;

            foreach ((var currentEntity, var blip) in TrackedEntities)
            {
                if (!blip.Selectable)
                    continue;

                var currentDistance = (_transformSystem.ToMapCoordinates(blip.Coordinates).Position - worldPosition).Length();

                if (closestDistance < currentDistance || currentDistance * MinimapScale > MaxSelectableDistance)
                    continue;

                closestEntity = currentEntity;
                closestDistance = currentDistance;
            }

            if (closestDistance > MaxSelectableDistance || !closestEntity.IsValid())
                return;

            TrackedEntitySelectedAction.Invoke(closestEntity);
        }
```

Se faltar using para `EntityCoordinates`, adicionar `using Robust.Shared.Map;` no topo.

- [ ] **Step 3: Ligar o clique do navmap (só quando o dono é a IA)**

Em `Content.Client/Medical/CrewMonitoring/CrewMonitoringBoundUserInterface.cs`, adicionar no topo os usings:

```csharp
using Content.Client.Silicons.StationAi;
using Content.Shared.Silicons.StationAi;
```

E no método `Open()`, após `_menu = this.CreateWindow<CrewMonitoringWindow>();` e `_menu.Set(stationName, gridUid);`, adicionar:

```csharp
        // Fork: se este monitor pertence ao núcleo da IA, clicar no mapa move o olho e fecha.
        if (EntMan.HasComponent<StationAiCoreComponent>(Owner))
        {
            _menu.NavMap.OnMapClick = coords =>
            {
                EntMan.System<StationAiSystem>().MoveEyeTo(coords);
                Close();
            };
        }
```

- [ ] **Step 4: Compilar o cliente**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build Content.Client/Content.Client.csproj -v quiet`
Expected: build sem erros.

- [ ] **Step 5: Commit**

```bash
cd ~/estacao-honk/space-station-14
git add Content.Client/Pinpointer/UI/NavMapControl.cs Content.Client/Medical/CrewMonitoring/CrewMonitoringBoundUserInterface.cs
git commit -m "feat(ia): clique no monitor de tripulacao move o olho da IA e fecha o mapa"
```

---

### Task 6: Deploy em staging e verificação manual

**Files:** nenhum (deploy + teste).

- [ ] **Step 1: Build completo da solução**

Run: `cd ~/estacao-honk/space-station-14 && dotnet build -v quiet`
Expected: 0 Error(s).

- [ ] **Step 2: Deploy para staging (porta 1213)**

Usar o skill `/deploy-ss14` com destino **staging**. Aguardar o boot validar (sem exceções de inicialização nos logs).

- [ ] **Step 3: Verificação manual da Parte 1 (cor da APC)**

Como IA Malf, em staging:
- Hackear uma APC → confirmar que **só a tela** fica vermelha e o **corpo continua normal**; a luz emitida acompanha o vermelho.
- Shuntar o núcleo para a APC → tela e luz laranja-âmbar; corpo normal.
- Reverter (sair do shunt / APC volta) → tela e luz voltam ao normal.

- [ ] **Step 4: Verificação manual da Parte 2 (mapa → olho)**

Como IA, em staging:
- Abrir o **radar** (ação com atalho) → clicar num ponto → o olho vai ao local e a janela fecha.
- Abrir o **monitor de tripulação** → clicar num ponto vazio → o olho vai ao local e fecha; clicar sobre um **crachá** de tripulante → o olho vai até aquele tripulante.
- **Arrastar** o mapa (sem soltar parado) → o mapa desloca e o olho **não** pula.
- Abrir um **console de radar comum** (não-IA, ex. console de uma nave) e confirmar que clicar nele **não** dispara nada novo (comportamento inalterado).

- [ ] **Step 5: Decisão de produção**

Se tudo passar em staging, propor ao usuário o deploy para produção (1212) via `/deploy-ss14`. Não subir para produção sem a aprovação do usuário.

---

## Auto-revisão do plano (feita)

- **Cobertura do spec:** Parte 1 → Task 1; Parte 2 evento → Task 2; servidor → Task 3; radar → Task 4; monitor → Task 5; staging + teste manual → Task 6. Sem lacunas.
- **Placeholders:** nenhum "TBD"/"TODO"; todo passo de código mostra o código real.
- **Consistência de tipos:** `MoveEyeTo(EntityCoordinates)` definido na Task 4 e usado na Task 5; `StationAiMoveEyeEvent(NetCoordinates)` definido na Task 2 e usado nas Tasks 3/4; `OnMapClick`/`OnRadarClick` são `Action<EntityCoordinates>?` consistentes; servidor converte com `GetCoordinates(NetCoordinates)`.
- **Ordem de execução:** Task 1 independente; Tasks 2→3 e 2→4→5 têm dependências respeitadas (evento antes dos consumidores; `MoveEyeTo` antes do uso no navmap).
