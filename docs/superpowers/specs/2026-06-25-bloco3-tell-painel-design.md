# Bloco 3 — Refino do tell do console hackeado + nota sobre o console de reset

Data: 2026-06-25
Branch: `malf-law-upload-defense`

## Contexto

O Bloco 3 (defesa da IA Malf contra upload de leis) já está completo na branch.
Dois pontos de feedback do usuário em jogo:

1. Existe um **segundo console** na sala do RD que "reseta" a IA.
2. O **tell visual** do console de upload hackeado (tela vermelha sempre ligada)
   é óbvio demais e entrega a sabotagem de longe.

## Investigação (fatos confirmados no código)

- **`StationAiUploadComputer`** (sala da IA): aceita placas de lei (`circuit_holder`),
  reescreve leis em ~1s. É o que o Bloco 3 defende.
- **`StationAiFixerComputer`** (sala do RD): NÃO aceita placa de lei. Slot é pra carta
  da IA (`station_ai_holder`). Ações: Ejetar, Reparar (revive IA morta) e **Purgar**.
  Purgar **deleta a IA inteira** (`SharedStationAiFixerConsoleSystem.cs:249-250`,
  `PredictedQueueDel`). Exige a IA já encartada.
- O console de upload herda `WiresPanel`/`Wires` do `BaseComputer`
  (`base_structurecomputers.yml:83-85`) → já abre com chave de fenda.

## Decisões

### Parte A — Console de reset (Fixer): NÃO mexer

O purge é um contra-jogo "caro" e legítimo: a tripulação precisa dominar a IA,
encartá-la e levá-la ao RD. O Bloco 3 defende o atalho barato (encaixar disco em 1s).
Blindar a IA contra o purge a deixaria quase impossível de remover. O purge já gera
admin log de impacto alto. Zero código nesta parte.

### Parte B — Tell vira investigação ativa

Trocar o brilho vermelho sempre-ligado por descoberta com chave de fenda:

- **Painel fechado:** console parece normal, uploads são blefados em silêncio.
- **Painel aberto (chave de fenda):** a tela tinge de vermelho + exame revela a
  sabotagem da fiação. Só quem abre o painel vê.
- **Reparo (multitool):** passa a exigir o painel aberto (mexer nos fios). Mantém o
  DoAfter de 3s já existente.

## Mudanças

1. **Client** `StationAiSystem.UploadConsole.cs`: tinge a tela só quando
   `Compromised && MaintenancePanelState (aberto)`.
2. **Server** `StationAiUploadConsoleSystem.cs`: reparo com multitool exige painel
   aberto (senão popup); novo `ExaminedEvent` revela a sabotagem com painel aberto.
3. **FTL** en/pt: `station-ai-upload-console-panel-closed` e
   `station-ai-upload-console-examine-compromised`.

Sem sprite novo. Sem mudança no Fixer.
