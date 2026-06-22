# IA Malf — Fase 3 Bloco 3: Defesa contra Upload de Leis (carência + hack + blefe)

**Data:** 2026-06-22
**Fork:** Estação Honk (pt-BR, thestarentity/space-station-14)
**Antecede:** Bloco 1 (CPU + hackear APC), Bloco 1.5 (ícones/ajustes), Bloco 2 (core shunting) — todos EM PRODUÇÃO.

## Objetivo

Fechar o loophole de "subir Crewsimov no console de upload encerra a Malf num clique", sem
tornar a IA imune de graça nem impossível de parar. A Malf ganha uma defesa em duas camadas
contra o `StationAiUploadComputer`, e os uploads viram um **blefe**: a IA é avisada da tentativa
e finge (verbalmente) que mudou, mantendo as leis Malf de verdade.

Reabre conscientemente a decisão de 2026-06-15 ("NÃO dar imunidade a placas de lei"): não é
imunidade crua, é interceptação com custo, contra-jogo e tell.

## Mecânica (decisões fechadas no brainstorm com o usuário)

### Duas camadas de proteção

1. **Carência inicial (automática, temporária).** Quando a IA é sorteada/forçada Malf, ganha um
   cronômetro de **~10 min** (`GraceDuration`). Durante a carência, qualquer upload de lei nela é
   interceptado (blefe) **sem ela fazer nada**. Serve de rede de segurança contra reset precoce
   "no minuto 2".

2. **Hack do console (ativo, permanente).** A Malf alt-clica o console de upload → radial "Hackear
   console de upload" (custa **~30 CPU**, patamar de "subverter borg"). Marca o console como
   **comprometido**. Enquanto comprometido, uploads naquele console viram blefe — **independente da
   carência**.

### Regra anti-furo (descoberta simulando uma rodada)

Sem isso, a Malf preguiçosa ganha imunidade permanente de graça: o RD sobe Crewsimov no min 5,
é interceptado em silêncio, ele acha que resetou e nunca mais volta; a carência expira no min 10 e
ninguém mais sobe lei. Para evitar:

- **Durante a carência, o blefe AVISA a IA** ("tentativa de sobrescrever leis detectada"). É o
  despertador: proteção temporária, tem que hackear pra valer.
- **Depois da carência, se NÃO hackeou → upload funciona de verdade** (reset real). Braços cruzados
  é aposta perigosa, não presente.
- **~2 min antes de expirar**, alerta pra IA: "proteção temporária acabando — hackeie o console".
  Garante que ela nunca é resetada por desconhecer a mecânica.

### Tradeoff de balanceamento (auto-equilíbrio)

- Carência = proteção **invisível** mas **temporária**.
- Hack = proteção **permanente** mas **detectável** (planta o tell no console + reparável).

A Malf escolhe entre discrição e permanência; a tripulação sempre tem um objeto físico (o console)
pra investigar/consertar, além do núcleo como âncora final.

## Condição central de interceptação

No override do fork `OnUpdaterInsert` (`Content.Server/Silicons/Laws/SiliconLawSystem.cs:304`),
para CADA cérebro-alvo `update` do loop:

```
interceptar SE:
    IsLawsetHostile(leis vivas do update)              // é uma IA Malf (reusa detecção existente)
    E ( CurTime < update.GraceUntil                    // ainda na carência
        OU HasComp<StationAiUploadHackedComponent>(ent) )  // ent = o console que recebeu a placa
```

- Se interceptar: **pular** `SetLaws` E **pular** a alteração do `ShowCrewIconsComponent`
  (`UncertainCrewBorder`) desse `update` — senão o crew-icon da Malf mudaria como se as leis tivessem
  mudado (tell falso/bug). Em vez disso, mandar o **aviso de blefe** ao cérebro interceptado.
- Se NÃO interceptar (IA leal, ou Malf fora de carência e console não comprometido): comportamento
  vanilla intacto (aplica leis + ícone normalmente).

O loop é **por entidade**, então numa rodada multi-IA cada cérebro é avaliado isolado: deflete só
a(s) Malf protegida(s), aplica normal nas demais. (Conflito multi-IA já é tratado assim no fork.)

A regra Malf aplica o lawset no `OnSelected` via `SetLaws` (não passa por `OnUpdaterInsert`) → a
atribuição inicial nunca é interceptada por engano. Item 8 (sync de leis do borg) usa `SetLaws`
direto, também fora do updater → sem conflito.

## Componentes e arquivos

### Novos (Shared)
- `StationAiUploadDefenseComponent` (no cérebro): `GraceUntil` (TimeSpan), `WarnedGraceEnding` (bool).
  Networked não é necessário (lógica server-side); manter server-only salvo necessidade de UI.
  *Decisão de implementação:* pode virar campo no `StationAiCpuComponent` já existente em vez de
  componente novo — avaliar no plano (o cérebro já carrega CPU). Preferência: componente próprio,
  responsabilidade única.
- `StationAiUploadHackedComponent` (no console): marcador "comprometido". **Networked** (o cliente
  precisa pra pintar o tell). Opcional: `EntityUid? HackedBy` server-only (log/autoria, padrão APC).
- Evento direcionado `StationAiHackUploadConsoleEvent` (radial → server), no estilo dos eventos de
  radial já existentes (APC/turret/borg).
- Enum de visual `StationAiUploadConsoleVisuals { Compromised }` pro tell por tinta.

### Novos (Server)
- `StationAiUploadDefenseSystem`: 
  - estampa a carência no `OnSelected` da regra Malf (ou hook próprio em `AfterAntagEntitySelected`);
  - tick leve (ou agendamento) pro aviso de "~2 min pra acabar";
  - método `IsProtected(brain, console)` consumido pelo `SiliconLawSystem.OnUpdaterInsert`;
  - manda os avisos de blefe / fim de carência (chat + popup ao cérebro).
- Handler do radial `OnHackUploadConsole` (no `StationAiSystem` server, padrão dos outros): valida
  acesso/estado, cobra CPU central em `OnRadialMessage` (handler NÃO recobra — padrão do shunt/Bloco 2),
  marca `StationAiUploadHackedComponent` no console, dispara appearance do tell, popup no alvo + log admin.

### Novos (Client)
- `StationAiSystem.UploadConsole.cs` (ou dentro de Structures): `GetStationAiRadial` no console →
  botão "Hackear console de upload" usando o ícone **`generalhack`** já existente
  (`actions_ai_custom.rsi`, feito pelo usuário). Esconder/cinza se já comprometido ou sem CPU
  (mesmo padrão de custo/cinza do BUI atual).
- Visualizer do tell: tinge o layer `computerLayerScreen` (state `aiupload`) do console quando
  `Compromised` — **modulação de cor do sprite existente**, padrão de `StationAiSystem.cs:103`. Cor
  proposta: vermelho/âmbar discreto. **Nenhum sprite novo.**

### Modificados
- `SiliconLawSystem.cs` (`OnUpdaterInsert`): inserir a checagem de interceptação por entidade.
- `StationAiMalfRuleSystem.cs` (`OnSelected`): estampar a carência no cérebro.
- `computers.yml` (`StationAiUploadComputer`): adicionar `StationAiWhitelist` + a UI de radial da IA
  (`enum.AiUi.Key: StationAiBoundUserInterface`), igual foi feito na torreta, pra habilitar alt-clique.
  Adicionar o `Appearance`/visualizer necessário pro tell.

### Counter-play do hack (reparo do console)
- O console comprometido é **reparável** pela tripulação → limpa `StationAiUploadHackedComponent` e o
  tell, devolvendo o upload. *Decisão de implementação no plano:* reusar o painel de fios/hack do
  computador (Wires) OU uma interação simples de ferramenta. Preferência: alinhar com o sistema de
  Wires já presente no console (tem `MaintenancePanel`/`generic_panel_open`). Detalhar no plano após
  inspecionar `WiresSystem`. Núcleo destruído continua resetando tudo (âncora).

## Balanceamento (números iniciais, ajustáveis com teste)
- `GraceDuration` = 10 min. `GraceEndingWarning` = 2 min antes.
- Custo do hack = 30 CPU (≈ subverter borg). Cabe na carência mesmo começando do zero (regen base +
  1-2 APCs). Revisar com a economia real.
- Aviso de fim de carência **obrigatório** (protege o jogador da IA de morrer por desconhecer a regra).

## Cenários simulados (checagem de bugs/conflitos)
1. **Reset precoce (min 2):** carência deflete → IA viva. ✔ (objetivo da camada 1)
2. **Malf preguiçosa + RD desiste:** regra anti-furo → após carência, upload real reseta. ✔ (sem
   imunidade grátis permanente)
3. **Malf hackeia cedo:** console tinge (tell) → RD acha comprometido → conserta → próximo upload vale. ✔
4. **Multi-IA:** loop por entidade deflete só a Malf protegida. ✔
5. **Crew-icon:** pular `UncertainCrewBorder` no alvo interceptado evita tell falso/bug. ✔
6. **Atribuição inicial Malf:** via `SetLaws`, fora do updater → nunca auto-interceptada. ✔
7. **Borg law sync (Item 8):** `SetLaws` direto, fora do updater → sem conflito. ✔
8. **IA leal:** `IsLawsetHostile` falso → upload vanilla normal sempre. ✔
9. **Gotcha Robust (Bloco 2):** NÃO assinar `(componente, evento direcionado)` já assinado por outro
   sistema (ex.: `EntityTerminatingEvent` do console/APC). Validar no boot do staging. ✔ (lição I-R005)
10. **Núcleo destruído / APC shuntada destruída:** independem desta feature; continuam matando a IA. ✔

## Sprites
**Nenhum sprite novo.** Radial usa `generalhack.png` (existente, do usuário). Tell por tinta do
sprite existente do console. (Regra explícita do usuário: só sprites que ele fez.)

## FTL (en-US + pt-BR)
- Botão do radial + tooltip de custo (com variação "sem processamento suficiente").
- Aviso de interceptação/blefe ao cérebro.
- Aviso de fim de carência.
- Popup/examine do console comprometido.
- (Reusar `silicons/station-ai.ftl`, padrão do Bloco 2.)

## Deploy
- C# (Shared+Server+Client) → set COMPLETO de DLLs (evitar mismatch Robust), `.bak` antes
  ([[feedback-dll-rebuild]], [[feedback-deploy-robust-mismatch]]).
- **OBRIGATÓRIO staging (1213) antes de produção (1212)** — é mudança forte de gameplay e mexe no
  sistema de leis (core). O boot do staging pega Duplicate Subscriptions / prototype errors antes dos
  jogadores (lição I-R005, Bloco 2 quebrou boot em produção por pular staging). Usar `/deploy-ss14`.
- FTL en+pt por item.

## Testes (gameplay no staging, virando Malf pelo verbo admin)
1. Reset no min 2 → deflete (carência). 2. Esperar carência sem hackear → upload reseta. 3. Hackear →
console tinge → upload vira blefe + aviso. 4. Reparar console → upload volta a valer. 5. Conferir CPU
cobrada uma vez. 6. Multi-IA (se viável) deflete só a Malf. 7. IA leal: upload normal. 8. Boot limpo.
