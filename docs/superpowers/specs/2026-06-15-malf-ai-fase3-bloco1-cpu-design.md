# IA Malf — Fase 3, Bloco 1: Economia de CPU + Hackear APC

Data: 2026-06-15
Status: design aprovado, pré-implementação
Contexto: Fases 1 (subverter borg) e 2 (lawset MALF + role + sorteio Secret + objetivos)
já estão em produção. Pesquisa SS13 que embasa este bloco:
`~/honk-memory/projects/estacao-honk/ia-malf-ss13-pesquisa.md`.
Memória de design: `ss14-malf-ai-design`.

## Problema

Hoje a IA Malf é **grátis e ilimitada**: todas as ações do radial saem sem custo, sem
progressão e sem *tell*. E pará-la é trivial (subir uma lei Crewsimov pelo console de
upload). Este bloco ataca o primeiro problema — "sem limite" — introduzindo a economia
de CPU do SS13, que dá progressão, escassez e contra-jogo (o tell das APCs hackeadas).

Os blocos seguintes da Fase 3 (fora do escopo deste spec) tratam o resto:
core shunting, hackear console de upload e o Doomsday/Overload/Blackout.

## Escopo deste bloco

1. Sistema de CPU na IA Malf (acumula no tempo; APCs hackeadas são a taxa de ganho).
2. Ação "Hackear APC" como pré-requisito, que vira fonte de CPU e fica visível (tell).
3. **Todas** as ações do radial passam a custar CPU.
4. UI de CPU para a IA: custo/cinza no radial + alert de HUD (sprite próprio) + examine.

Fora de escopo: shunting, console de upload, Doomsday/Overload/Override/Blackout.

## Fundação reusada (não refazer)

- `StationAiApcControllableComponent` (Shared, networked) + `StationAiApcSystem` (Server) —
  hoje a ação de APC é um toggle puro do disjuntor. Vai ganhar o estado `Hacked`.
- `StationAiBulkDoorSystem` — já tem o padrão de gating por lei hostil
  (`HostileLawsets` / `DenyIfNotHostile`) e logs de admin. É onde as ações debitam CPU.
- `StationAiHostileLawComponent` — marcador networked na IA; modelo para networkar a CPU.
- Padrão de eventos `BaseStationAiAction` → `StationAiRadialMessage`.
- Alert de essência do **Revenant** (`Resources/Prototypes/Alerts/revenant.yml`) — modelo
  para o alert de CPU (recurso numérico com sprite próprio no HUD).

## Arquitetura

### Componente de CPU

`StationAiCpuComponent` (Content.Shared, `[RegisterComponent, NetworkedComponent,
AutoGenerateComponentState]`), pendurado na entidade da IA (mesma da
`StationAiHostileLawComponent`). Campos:

- `Cpu` (float, `[AutoNetworkedField]`) — CPU atual, networkada pro cliente.
- `MaxCpu` (float, DataField) — teto. Default `200`.
- `BaseRegen` (float, DataField) — CPU por segundo sem APCs. Default `0.1`.
- `RegenPerApc` (float, DataField) — CPU/s adicional por APC hackeada. Default `0.2`.
- `HackedApcCount` (int) — quantas APCs hackeadas alimentam a taxa (derivado;
  recomputado quando uma APC é hackeada/destruída/consertada).

### Sistema de CPU

`StationAiCpuSystem` (Content.Server, `partial`, campos `[Dependency]` NÃO `readonly` —
pegadinha RA0049/RA0051 já documentada em `ss14-malf-ai-design`). Responsável por:

- **Tick de regen** (`Update`): `Cpu = Min(MaxCpu, Cpu + (BaseRegen + RegenPerApc *
  HackedApcCount) * frameTime)`. Faz `Dirty` quando muda (com throttle pra não floodar a
  rede — ex.: só dirty a cada ~0.5 CPU ou a cada N ticks).
- **`TrySpend(Entity<StationAiCpuComponent> ia, float custo)`** → bool. Se
  `Cpu >= custo`, debita, `Dirty`, retorna true. Senão, retorna false (o chamador faz o
  popup "CPU insuficiente" e nega).
- Helper para resolver a entidade-IA a partir do `args.User` das ações do radial e achar
  o `StationAiCpuComponent` (mesma resolução que o gating de lei hostil já usa).

### Hackear APC

Estender `StationAiApcControllableComponent` com `bool Hacked` (`[DataField,
AutoNetworkedField]`).

- Novo evento `StationAiApcHackEvent : BaseStationAiAction` (Shared).
- No radial da APC, **sob lei hostil**, a primeira opção vira "Hackear" enquanto
  `Hacked == false`. Depois de hackeada, o radial mostra as opções existentes de
  "Cortar/Restaurar energia".
- `StationAiApcSystem.OnHack`: marca `Hacked = true`, recomputa a taxa da IA
  (`HackedApcCount++` / re-soma), dispara o tell visual (Appearance) e loga no admin.
- **Tell visível (duas camadas):**
  - Visual: estado de Appearance `Hacked` na APC → tom/LED alterado (sprite/visualizer no
    client; usa o sistema de Appearance da APC). O usuário fornecerá o sprite se quiser um
    visual dedicado; caso contrário, recolorir/LED via shader/estado existente.
  - Examine: ao examinar uma APC hackeada, texto "comprometida" (FTL en+pt).
- **Remover a fonte:** se a APC é destruída ou consertada (perde `Hacked`), recomputar a
  taxa (decrementa `HackedApcCount`). A IA não regride a CPU já acumulada, só perde ritmo.

### Custo nas ações

Cada handler das ações do radial (portas, torretas, borg, atmos, comportas) chama
`TrySpend` ANTES de executar. Falhou → popup "CPU insuficiente" + nega (sem efeito).
Ações de "estação inteira" continuam também gated por lei hostil (a checagem de CPU é
adicional, não substitui `DenyIfNotHostile`).

Tabela de custos inicial (ajustável no staging antes de produção):

| Ação | Custo CPU |
|---|---|
| Ferrolho / eletrificar / emergência — porta única ou área | 3 |
| Lockdown estação inteira (cada categoria: bolt/electrify/emergência) | 30 |
| Torretas → modo letal | 10 |
| Subverter borg | 30 |
| Desligar / imobilizar borg | 10 |
| Detonar borg | 50 |
| Pânico atmos (estação) | 50 |
| Hackear APC | 0 (é a renda) |
| Cortar / restaurar energia (pós-hack) | 0 |

Os custos ficam em constantes nomeadas no(s) sistema(s) (ou num pequeno mapa central),
não espalhados como literais, pra facilitar o tuning.

### UI de CPU

- **Radial:** cada opção mostra o custo; opções sem saldo aparecem desabilitadas/cinzas.
  O cliente decide com base na `Cpu` networkada (mesma ideia do
  `StationAiHostileLawComponent`, que o cliente usa pra esconder opções).
- **Alert de HUD (persistente):** alert novo "CPU" modelado no alert de essência do
  Revenant, mostrando `Cpu/MaxCpu` como %. **Sprite desenhado pelo usuário.** Mantido
  SEPARADO da energia real da IA (perder energia desliga a IA; CPU é outra coisa) pra
  não confundir os dois estados.
- **Examine no core:** examinar o core da IA mostra a CPU atual.

## Balanceamento

- Sem APC hackeada, a IA quase não progride (`BaseRegen` baixo) — força o jogo de
  hackear, que tem o custo do tell visível.
- Teto `MaxCpu` evita banking infinito.
- Curva (regen + custos) é ponto de partida; **validar no staging (porta 1213) antes de
  produção**, por ser mudança forte de gameplay (`ss14-staging-server`).

## Deploy

- C#: rebuild Content.Server + Content.Client (`feedback-dll-rebuild`); cuidar do set
  COMPLETO de DLLs pra evitar mismatch de Robust (`feedback-deploy-robust-mismatch`).
- FTL en-US + pt-BR para cada string nova (hackear, examine "comprometida", popup CPU
  insuficiente, alert, custos se exibidos).
- YAML novo (componente/alert) que referencia DLL nova quebra a DLL antiga no boot
  (Resources é symlink compartilhado prod↔repo) → deployar DLL nova na produção junto.
- Testar no staging primeiro; só ir pra produção com autorização do usuário.

## Riscos / pegadinhas conhecidas

- RA0049/RA0051 (Release): sistema com `[Dependency]` precisa ser `partial`, campos não
  `readonly`.
- Throttle do `Dirty` da CPU pra não floodar rede a cada tick.
- Detecção de lei hostil compara CONTEÚDO das leis; "MalfAi" já está em `HostileLawsets`
  (`StationAiBulkDoorSystem`) — não mexer nisso.
- A ação de APC hoje não checa acesso (a IA controla livremente); o gating de "Hackear"
  é por lei hostil + estado `Hacked`, mantendo o padrão.

## Critérios de aceite

1. IA Malf tem CPU que sobe no tempo; sem APC hackeada, sobe muito devagar.
2. Hackear uma APC aumenta a taxa de ganho e deixa a APC visivelmente comprometida
   (visual + examine).
3. Cortar/restaurar energia de uma APC só aparece após hackeá-la.
4. Toda ação do radial debita o custo da tabela; sem saldo, a ação é negada com popup e
   não tem efeito.
5. A IA vê a CPU no radial (custo/cinza), num alert de HUD (%), e ao examinar o core.
6. Build limpo; sobe em staging sem erro; comportamento validado em staging antes de prod.
