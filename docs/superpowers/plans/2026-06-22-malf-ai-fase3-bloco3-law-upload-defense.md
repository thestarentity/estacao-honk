# IA Malf — Bloco 3: Defesa contra Upload de Leis — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para implementar tarefa a tarefa. Os passos usam checkbox (`- [ ]`).

**Goal:** Dar à IA Malf uma defesa em duas camadas (carência inicial automática + hack permanente do console) que transforma uploads de lei em blefe, com tell e contra-jogo, sem imunidade grátis.

**Architecture:** Interceptação centralizada no override `OnUpdaterInsert` do `SiliconLawSystem` (server), avaliada por entidade; carência estampada no cérebro pela regra Malf; ação de hack pelo radial do console (custa CPU), marcando o console como comprometido (networked) e pintando o sprite existente; reparo do console limpa o marcador.

**Tech Stack:** C# (Robust/SS14 ECS), YAML de protótipos, FTL (en-US + pt-BR). Fork Estação Honk.

## Global Constraints
- **Responder ao usuário em pt-BR.** Comentários de código podem ser en/pt no padrão do arquivo vizinho.
- **Nenhum sprite novo.** Radial usa `generalhack.png` (existente, `actions_ai_custom.rsi`). Tell = modulação de cor do sprite existente do console. (Regra explícita do usuário.)
- **Sem Co-Authored-By, sem emojis, sem marcadores de IA** em commits/changelogs ([[feedback-workflow]]).
- **Toda ação de IA custa CPU**, cobrança central em `OnRadialMessage` + Refund se recusar; o handler da ação NÃO recobra (padrão Bloco 2 / [[ss14-ia-cpu-audit]]).
- **Detecção de Malf reusa `IsLawsetHostile`** / `StationAiHostileLawComponent` — não criar detecção nova.
- **Gotcha Robust (lição I-R005):** dois sistemas NÃO podem assinar o mesmo par (componente, evento direcionado) → `Duplicate Subscriptions` FATAL no boot. Validar boot no staging.
- **Deploy:** set COMPLETO de DLLs + `.bak`; **staging (1213) antes de produção (1212), obrigatório** (mexe no core de leis). Usar `/deploy-ss14`.
- **Verificação real** = build limpo + boot no staging sem erro de prototype/subscription + teste de gameplay em jogo. Não há teste unitário pra este subsistema.

**Build (dev, da raiz do fork):** `dotnet build Content.Server Content.Client Content.Shared` (0 erros).
**Boot/gameplay:** `/deploy-ss14` para staging → conferir "Ready" sem exceção → virar Malf pelo verbo admin "Tornar IA Malf".

**Números (ajustáveis):** `GraceDuration` = 10 min; aviso de fim 2 min antes; custo do hack = 30 CPU.

**Arquivos de referência a espelhar (ler antes de codar a tarefa correspondente):**
- Radial em estrutura + handler server + custo CPU: `Content.Client/Silicons/StationAi/StationAiSystem.Apc.cs`, `Content.Server/.../StationAiSystem.Apc.cs` (hack de APC — padrão mais próximo).
- Cobrança central de CPU: `BaseStationAiAction.CpuCost` + `OnRadialMessage` no `StationAiSystem` server.
- Tell por tinta: `Content.Client/Silicons/StationAi/StationAiSystem.cs:103` (APC âmbar/vermelho).
- Marcador networked no alvo: `StationAiApcControllableComponent` (`Hacked`/`HackedBy`).
- Ação shunt (custo central, handler não recobra): `Content.Server/Silicons/.../StationAiShuntSystem.cs` (Bloco 2).

---

### Task 1: Tipos Shared (componentes, evento, enum de visual)

**Files:**
- Create: `Content.Shared/Silicons/Laws/Components/StationAiUploadDefenseComponent.cs`
- Create: `Content.Shared/Silicons/StationAi/Components/StationAiUploadHackedComponent.cs`
- Create: `Content.Shared/Silicons/StationAi/StationAiUploadConsoleEvents.cs`

**Interfaces — Produces:**
- `StationAiUploadDefenseComponent` (no cérebro da IA): `public TimeSpan GraceUntil;` `public bool WarnedGraceEnding;`. `[RegisterComponent]`, server-side (sem `[AutoNetworkedField]`).
- `StationAiUploadHackedComponent` (no console): `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]` com `[DataField, AutoNetworkedField] public EntityUid? HackedBy;`. A presença do componente = "comprometido" (o cliente lê via state pro tell).
- `enum StationAiUploadConsoleVisuals : byte { Compromised }` (em `StationAiUploadConsoleEvents.cs` ou arquivo `StationAiUploadConsoleVisuals.cs`).
- `StationAiHackUploadConsoleEvent : BaseStationAiAction` — evento direcionado do radial (espelhar o evento de hack de APC; herdar `BaseStationAiAction` p/ ganhar `CpuCost`). Override `CpuCost => 30`.

**Steps:**
- [ ] **Step 1:** Ler `StationAiApcControllableComponent` e o evento de hack de APC p/ copiar exatamente o padrão de atributos/networking.
- [ ] **Step 2:** Criar os 3 arquivos com os tipos acima (código completo seguindo o padrão lido).
- [ ] **Step 3:** Build: `dotnet build Content.Shared` → 0 erros.
- [ ] **Step 4:** Commit: `git add -A && git commit -m "Bloco 3: tipos shared da defesa de upload de leis da IA Malf"`

---

### Task 2: Carência + interceptação no OnUpdaterInsert + avisos (o coração)

**Files:**
- Create: `Content.Server/Silicons/StationAi/StationAiUploadDefenseSystem.cs`
- Modify: `Content.Server/Silicons/Laws/SiliconLawSystem.cs:304-323` (override `OnUpdaterInsert`)
- Modify: `Content.Server/GameTicking/Rules/StationAiMalfRuleSystem.cs:27-41` (`OnSelected`)
- Modify: `Resources/Locale/en-US/silicons/station-ai.ftl` + `Resources/Locale/pt-BR/silicons/station-ai.ftl`

**Interfaces:**
- Consumes: `StationAiUploadDefenseComponent`, `StationAiUploadHackedComponent` (Task 1); `_law.IsLawsetHostile(...)` / leis vivas existentes.
- Produces:
  - `StationAiUploadDefenseSystem.StampGrace(EntityUid brain)` — `EnsureComp<StationAiUploadDefenseComponent>` + `GraceUntil = _timing.CurTime + TimeSpan.FromMinutes(10)`.
  - `StationAiUploadDefenseSystem.IsProtected(EntityUid brain, EntityUid console)` → `bool` = `IsLawsetHostile(leis vivas de brain)` E (`_timing.CurTime < def.GraceUntil` OU `HasComp<StationAiUploadHackedComponent>(console)`).
  - `StationAiUploadDefenseSystem.NotifyBluff(EntityUid brain)` — popup + msg de chat ao cérebro ("tentativa de sobrescrever leis interceptada").

**Steps:**
- [ ] **Step 1:** Em `StationAiMalfRuleSystem.OnSelected`, após aplicar o lawset, chamar `_uploadDefense.StampGrace(target)` (adicionar `[Dependency] StationAiUploadDefenseSystem _uploadDefense`). Garantir que o verbo admin "Tornar IA Malf" também passa por `AfterAntagEntitySelectedEvent` (passa — `ForceMakeAntag`).
- [ ] **Step 2:** Criar `StationAiUploadDefenseSystem` com `StampGrace`, `IsProtected`, `NotifyBluff` e um `Update`/tick leve que, para cada `StationAiUploadDefenseComponent` ainda sob lei hostil, se `!WarnedGraceEnding` e faltam ≤2 min p/ `GraceUntil`, manda o aviso de fim de carência e marca `WarnedGraceEnding = true`. (Tick com throttle, ex.: a cada 5 s.)
- [ ] **Step 3:** No `OnUpdaterInsert` (override server), dentro do `while (query.MoveNext(out var update))`: se `_uploadDefense.IsProtected(update, ent.Owner)` → `_uploadDefense.NotifyBluff(update)` e `continue` (pular `SetLaws` E o bloco `ShowCrewIconsComponent`/`UncertainCrewBorder`). Caso contrário, comportamento atual intacto. Adicionar `[Dependency] StationAiUploadDefenseSystem _uploadDefense` no `SiliconLawSystem`.
- [ ] **Step 4:** FTL en+pt: `station-ai-upload-intercepted` (aviso de blefe) e `station-ai-upload-grace-ending` (aviso de fim de carência).
- [ ] **Step 5:** Build: `dotnet build Content.Server` → 0 erros.
- [ ] **Step 6:** Deploy staging (`/deploy-ss14` staging) → boot "Ready" sem `Duplicate Subscriptions`/prototype error. Em jogo: virar Malf, subir Crewsimov no min ~0 → leis NÃO mudam + aviso de blefe aparece.
- [ ] **Step 7:** Commit: `git add -A && git commit -m "Bloco 3: carência + interceptação de upload + avisos de blefe/fim de carência"`

---

### Task 3: Ação de hackear o console (radial + CPU + marcador)

**Files:**
- Create: `Content.Client/Silicons/StationAi/StationAiSystem.UploadConsole.cs`
- Create/Modify: `Content.Server/Silicons/StationAi/StationAiSystem.UploadConsole.cs` (ou pasta equivalente onde ficam os handlers server do radial)
- Modify: `Resources/Prototypes/Entities/Structures/Machines/Computers/computers.yml:1607` (`StationAiUploadComputer`)
- Modify: `Resources/Locale/{en-US,pt-BR}/silicons/station-ai.ftl`

**Interfaces:**
- Consumes: `StationAiHackUploadConsoleEvent` + `StationAiUploadHackedComponent` (Task 1); `generalhack` icon.
- Produces: handler server `OnHackUploadConsole` que faz `EnsureComp<StationAiUploadHackedComponent>(console, comp => comp.HackedBy = brain)`, dispara appearance `StationAiUploadConsoleVisuals.Compromised = true`, popup no console (alvo) + log admin. CPU (30) é cobrada central em `OnRadialMessage` via `CpuCost` — o handler NÃO recobra.

**Steps:**
- [ ] **Step 1:** Ler `StationAiSystem.Apc.cs` (client e server) pra copiar o padrão de `GetStationAiRadial` + handler + cobrança.
- [ ] **Step 2:** No `computers.yml`, no `StationAiUploadComputer`: adicionar `- type: StationAiWhitelist`, a UI `- type: UserInterface` com `enum.AiUi.Key: type: StationAiBoundUserInterface` (espelhar a torreta/APC) e `- type: Appearance`.
- [ ] **Step 3:** Client `StationAiSystem.UploadConsole.cs`: subscrever o GetRadial do console → botão "Hackear console de upload" com `generalhack`; esconder se já tem `StationAiUploadHackedComponent`; cinza/custo se sem CPU (padrão do BUI).
- [ ] **Step 4:** Server handler `OnHackUploadConsole`: validações + `EnsureComp` marcador + appearance + popup no alvo (`uid`, recipient = IA — padrão BUG2 do Bloco 1.5) + log admin.
- [ ] **Step 5:** FTL en+pt: `station-ai-radial-hack-upload-console` + tooltip de custo (variação "sem processamento suficiente").
- [ ] **Step 6:** Build: `dotnet build Content.Server Content.Client` → 0 erros.
- [ ] **Step 7:** Deploy staging → em jogo: Malf alt-clica console → botão aparece → hackear cobra 30 CPU uma vez → subir Crewsimov depois da carência vira blefe.
- [ ] **Step 8:** Commit: `git add -A && git commit -m "Bloco 3: ação de hackear console de upload (radial + custo CPU + marcador)"`

---

### Task 4: Tell visual do console comprometido (tinta, sem sprite novo)

**Files:**
- Create: `Content.Client/Silicons/StationAi/StationAiUploadConsoleVisualizerSystem.cs` (ou adicionar ao `StationAiSystem.UploadConsole.cs` client)

**Interfaces:**
- Consumes: `StationAiUploadConsoleVisuals.Compromised` (appearance, Task 1/3).

**Steps:**
- [ ] **Step 1:** Ler `StationAiSystem.cs:90-140` (tint da APC) pra copiar o padrão de `AppearanceChange` + `LayerSetColor`.
- [ ] **Step 2:** Visualizer: ao `Compromised = true`, `LayerSetColor` no layer `computerLayerScreen` (state `aiupload`) p/ um tom discreto (ex.: vermelho `new Color(1f, 0.3f, 0.3f)`); ao falso, restaurar `Color.White`.
- [ ] **Step 3:** Build: `dotnet build Content.Client` → 0 erros.
- [ ] **Step 4:** Deploy staging → console hackeado fica visivelmente tingido; não-hackeado normal.
- [ ] **Step 5:** Commit: `git add -A && git commit -m "Bloco 3: tell visual do console de upload comprometido (tinta)"`

---

### Task 5: Contra-jogo — reparar o console limpa o hack

**Files:**
- Modify: `Content.Server/Silicons/StationAi/StationAiSystem.UploadConsole.cs` (handler server da Task 3)
- (Ler `WiresSystem` / o painel de manutenção do computador pra escolher o gatilho de reparo)

**Interfaces:**
- Produces: ao reparar (gatilho escolhido), `RemComp<StationAiUploadHackedComponent>(console)` + appearance `Compromised = false` + popup de "console reparado".

**Steps:**
- [ ] **Step 1:** Ler o `WiresComponent`/`MaintenancePanel` do console (já tem `generic_panel_open`) e decidir o gatilho mais simples e robusto: hack de fios (corte/mend de um fio) OU interação de ferramenta (chave/cortador). Escolher o que melhor casa com o computador.
- [ ] **Step 2:** Implementar o handler que, no gatilho, faz `RemComp` + appearance false + popup. Garantir que NÃO assina um par (componente, evento) já assinado (lição I-R005) — se reusar `EntityTerminatingEvent` etc., delegar a um único assinante.
- [ ] **Step 3:** Build: `dotnet build Content.Server` → 0 erros.
- [ ] **Step 4:** Deploy staging → reparar console comprometido → tinta some → próximo upload volta a aplicar de verdade.
- [ ] **Step 5:** Commit: `git add -A && git commit -m "Bloco 3: contra-jogo — reparar console limpa o hack da IA Malf"`

---

### Task 6: Revisão de integração + validação de gameplay completa + FTL final

**Files:**
- Modify: FTL en+pt se faltar alguma chave; nenhum código novo esperado.

**Steps:**
- [ ] **Step 1:** Conferir que toda chave FTL usada existe em en-US E pt-BR (grep das chaves `station-ai-upload*`).
- [ ] **Step 2:** Build completo: `dotnet build Content.Server Content.Client Content.Shared` → 0 erros.
- [ ] **Step 3:** Deploy staging e rodar os 8 cenários da spec em jogo: (1) reset min 2 deflete; (2) sem hackear, pós-carência reseta; (3) hackear→tell→blefe; (4) reparar→upload volta; (5) CPU cobrada 1x; (6) multi-IA deflete só a Malf (se viável testar); (7) IA leal upload normal; (8) boot limpo.
- [ ] **Step 4:** Atualizar a memória/vault: `ss14_malf_ai_design.md`, `ss14_ia_remaining_roadmap.md` (Bloco 3 = FEITO; próximo = Bloco 4), e propor nota no vault `~/honk-memory/` (decisão D-0xx: reabertura da imunidade a placas de lei + design da defesa). Pedir aprovação do usuário antes de commitar no vault.
- [ ] **Step 5:** Deploy produção **só após autorização explícita do usuário** (set completo de DLLs, `.bak`, watchdog).

---

## Self-Review (feita)
- **Cobertura da spec:** carência (T2), hack/CPU (T3), anti-furo/avisos (T2), tell (T4), contra-jogo (T5), interceptação por entidade + skip crew-icon (T2), sprites só existentes (T3/T4), FTL en+pt (T2/T3/T6), deploy staging-first (todas), cenários (T6). ✔
- **Placeholders:** os "ler arquivo X" são instruções de espelhamento de padrão real (o subagente lê o código vizinho pra escrever a implementação exata), não TODOs de design. Decisões deixadas ao subagente: componente próprio vs campo (fechado: componente próprio, T1) e gatilho de reparo (T5, escolha informada após ler WiresSystem). ✔
- **Consistência de tipos:** `StationAiUploadDefenseComponent.GraceUntil/WarnedGraceEnding`, `StationAiUploadHackedComponent.HackedBy`, `StationAiHackUploadConsoleEvent.CpuCost=30`, `StationAiUploadConsoleVisuals.Compromised`, `IsProtected/StampGrace/NotifyBluff` — usados consistentes entre T1→T2→T3→T4→T5. ✔
