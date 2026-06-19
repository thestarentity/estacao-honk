# Fase 3 — Bloco 2: Core Shunting (IA Malf)

> Spec de design. Fechado com o usuário em 2026-06-19 (mesmo fluxo da Fase 2, Bloco 1 e item 8:
> brainstorm → spec → plano → execução). Próximo passo após aprovação: writing-plans.

## Objetivo

Dar à IA Malf a habilidade de **shuntar o núcleo**: esconder seu processo dentro de uma APC
que ela hackeou. Ela **sobrevive à destruição do núcleo físico**, mas fica **dormente** enquanto
escondida. A tripulação precisa caçar e destruir a APC certa para matá-la de vez.

Isso resolve o ponto de balanceamento "o núcleo é o único/trivial contra-jogo" levantado na pesquisa
do SS13 (`~/honk-memory/projects/estacao-honk/ia-malf-ss13-pesquisa.md`, módulo #3 / seção 3).

## Decisões fechadas com o usuário (2026-06-19)

1. **Núcleo físico durante o shunt:** fica na sala, **inerte e vulnerável**, com aparência de
   "vazio/desligado" (tell de que a IA shuntou). A IA não vê nem age por ele.
2. **Capacidade shuntada:** **dormente**. Sem radial, sem módulos, sem visão de ação. A única
   ação possível é **"Voltar ao núcleo"**.
3. **Núcleo destruído enquanto shuntada:** a IA **sobrevive presa na APC para sempre**, sem poder
   agir. O objetivo dela passa a ser só sobreviver. A tripulação tem que achar/destruir a APC.
4. **Pré-requisito + custo do shunt:** só pode shuntar para uma APC **já hackeada**; custa
   **CPU alto (~50, ajustável no teste)**.
5. **APC ocupada destruída:** a IA **morre** (caminho de morte normal da IA → mente vira fantasma).
6. **Tell extra sutil** na APC ocupada: além do tinte de "hackeada", uma pista a mais
   (examine "atividade anômala" + sinal visual leve) para distinguir das outras APCs hackeadas.

### Padrões decididos por default (aprovados no brainstorm)

- **Voltar ao núcleo:** grátis, sem cooldown (ela não pode agir shuntada, não há o que abusar).
- **Não pula direto de APC para APC.** Para re-shuntar tem que voltar ao núcleo e pagar CPU de novo.
- **Cardar (intellicard) o núcleo vazio não faz nada** enquanto ela está shuntada (ela não está lá).

## Arquitetura

Toda a fundação relevante já existe (confirmado no código 2026-06-19):

- A entidade-**cérebro** da IA fica num container `StationAiCoreComponent.Container` no **núcleo**.
  É a MESMA entidade que carrega leis, `StationAiHostileLawComponent` e `StationAiCpuComponent`.
- O núcleo tem uma `RemoteEntity` (o "olho"/holo) que dá visão e para onde as ações são relayadas
  (`OnHeldRelay` → `core.Comp.RemoteEntity`). Sem olho/sem energia = IA não age (`OnCorePower`).
- Já existe `JumpToCoreEvent` (`OnCoreJump`) que reposiciona o olho — modelo do "voltar ao núcleo".
- O intellicard move o cérebro entre containers (`StationAiHolderComponent.Container`) — **modelo
  técnico do shunt** (mover o cérebro de container).
- APC hackeada: `StationAiApcControllableComponent.Hacked` + `HackedBy` (server) + radial já existem.
- CPU: `StationAiCpuSystem.TryConsume` (gancho central `TrySpendActionCpu` em `OnRadialMessage`).

### Por que MOVER o cérebro (e não só marcá-lo)

Para a IA **sobreviver à destruição do núcleo**, o cérebro NÃO pode continuar dentro do container do
núcleo (senão seria deletado/derrubado junto). O shunt **move a entidade-cérebro do container do núcleo
para um container na APC hospedeira** — mesmo mecanismo do intellicard. Assim:

- Destruir o núcleo não toca no cérebro (ela sobrevive).
- Destruir a APC ocupada arrasta o cérebro → morte (contra-jogo).

### Componentes e sistemas

**Novos:**

- `StationAiShuntedComponent` (Shared, networked) — no cérebro. Campos: `HostApc` (EntityUid?,
  a APC hospedeira) e `CoreLost` (bool, true quando o núcleo foi destruído e ela ficou presa).
  Enquanto presente → bloqueia abrir o radial e qualquer ação (checagem central).
- `StationAiShuntSystem` (Server) — orquestra:
  - **Shuntar:** valida APC hackeada + saldo de CPU (`TryConsume ~50`); move o cérebro do container
    do núcleo para um container na APC (container dedicado, ex. `station_ai_shunt_slot`); adiciona
    `StationAiShuntedComponent`; despawna/desliga o olho; aplica aparência "vazio" no núcleo e
    "ocupada" na APC; popups + log de admin.
  - **Voltar ao núcleo:** só se o núcleo existir e não estiver `CoreLost`; move o cérebro de volta,
    remove o componente, religa o olho, reverte as aparências.
  - **Núcleo destruído enquanto shuntada:** detecta a destruição do núcleo (hook no
    `StationAiCoreComponent` — `EntityTerminatingEvent`/`BreakageEventArgs`/destruição) e, se o cérebro
    estiver shuntado, marca `CoreLost = true` (ela vira permanente-presa; "Voltar ao núcleo" some).
  - **APC ocupada destruída:** hook na destruição da APC hospedeira → mata a IA shuntada pelo caminho
    de morte padrão (mente → fantasma), garantindo que o round/objetivos reajam.

**Mexidos:**

- `BaseStationAiAction` / `OnRadialMessage` (ou `TrySpendActionCpu`): negar QUALQUER ação enquanto
  `StationAiShuntedComponent` estiver presente (a IA shuntada é dormente). Ponto único de bloqueio.
- `StationAiApcControllableComponent`: novo flag networked `Occupied` (para o tell visual + radial).
- `StationAiApcVisuals`: novo estado `Occupied` (tinte/sinal extra além de `Hacked`).
- Radial da APC (Client): ação **"Shuntar núcleo"** só aparece em APC **hackeada e não ocupada**;
  custo de CPU mostrado/cinza como as demais ações.
- Ação **"Voltar ao núcleo"**: disponível para o cérebro shuntado (verbo/ação no olho-ausente ou
  alerta dedicado), escondida quando `CoreLost`.
- Núcleo (`StationAiCoreComponent` appearance): estado "vazio/desligado" enquanto shuntada.

### Fluxo de dados

```
IA hackeia APC (já existe) ──► APC.Hacked = true, HackedBy = cérebro
IA alt-clica APC hackeada ──► radial "Shuntar núcleo" (custo ~50 CPU)
   └─ServidorTryConsume(50) OK──► move cérebro: container núcleo → container APC
                                  + StationAiShuntedComponent{HostApc=APC}
                                  + olho off, núcleo "vazio", APC "ocupada"
Shuntada: toda ação negada; só "Voltar ao núcleo" disponível
   ├─ Voltar (núcleo vivo) ──► move cérebro de volta, olho on, aparências revertidas
   ├─ Núcleo destruído ──────► CoreLost=true; "Voltar" some; presa para sempre (só sobreviver)
   └─ APC ocupada destruída ─► IA morre (mente → fantasma)
```

## Tratamento de erros / casos de borda

- **Sem CPU suficiente:** ação cinza no radial; servidor recusa silenciosamente (igual às demais).
- **APC perde energia enquanto ocupada:** a IA continua presa (energia não a expulsa); só destruição
  da APC a mata. (Decisão: simplicidade; revisar no teste se ficar estranho.)
- **APC já ocupada por... ela mesma:** só há uma IA Malf por round (sorteio mira a Station AI única);
  não há colisão de duas IAs na mesma APC. Ainda assim, `Occupied` impede shuntar para uma APC ocupada.
- **Intellicard no núcleo vazio:** sem efeito enquanto shuntada (o cérebro não está no núcleo).
- **Round termina / IA deletada por admin enquanto shuntada:** o cérebro está num container normal da
  APC; o caminho de cleanup padrão se aplica. Garantir que remover o componente reverte aparências.
- **IA leal nunca shunta:** o radial "Shuntar núcleo" é uma ação de CPU como as outras, gated por lei
  hostil (mesmo mecanismo que esconde as ações perigosas para a IA leal).

## Custos / FTL

- **CPU:** shuntar ~50 (constante ajustável); voltar = 0.
- **FTL en-US + pt-BR** para: ação "Shuntar núcleo", ação "Voltar ao núcleo", popups (shuntou / voltou /
  núcleo perdido / preso), examine da APC ocupada ("atividade anômala") e do núcleo vazio.

## Testes

- **Unit/integração (se viável no padrão do repo):** shuntar move o cérebro para a APC e debita CPU;
  voltar move de volta; destruir o núcleo shuntada seta `CoreLost`; destruir a APC ocupada mata a IA.
- **Manual no staging (1213), obrigatório (mudança forte de gameplay):**
  1. Virar IA Malf (verbo de admin), hackear uma APC, shuntar (confirmar custo de CPU e núcleo "vazio").
  2. Confirmar que shuntada nenhuma ação do radial funciona; só "Voltar ao núcleo".
  3. Voltar ao núcleo e confirmar reativação normal.
  4. Shuntar de novo, destruir o núcleo, confirmar que ela sobrevive presa e "Voltar" sumiu.
  5. Destruir a APC ocupada e confirmar a morte da IA (fantasma / reação do round).
  6. Confirmar o tell sutil: a APC ocupada se distingue das outras hackeadas (examine + visual).

## Deploy

- C# em **Shared + Server + Client** → **set completo de DLLs** (evita mismatch de Robust;
  ver `[[feedback-deploy-robust-mismatch]]` / `[[feedback-dll-rebuild]]`).
- **Staging (1213) primeiro, obrigatório.** Produção (1212) só após teste e autorização do usuário.
- Backup `.bak-pre-shunt` antes de copiar DLLs; reiniciar watchdog; validar boot limpo.

## Fora de escopo (YAGNI / blocos futuros)

- Pular entre APCs sem voltar ao núcleo (decidido: não).
- Re-ancorar em um núcleo novo construído (decidido: não; ela fica presa).
- Demais módulos malf (vigilância, sabotagem, Doomsday) → Blocos 4, 5, 6 do roadmap.
