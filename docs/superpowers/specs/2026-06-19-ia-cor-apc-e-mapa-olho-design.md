# Design — Ajustes na IA: cor da APC só na tela + mapa que move o olho

Data: 2026-06-19
Branch: `ia-cor-apc-e-mapa-olho`
Status: aprovado pelo usuário

## Contexto

Dois ajustes na IA da estação (fork Estação Honk), antes da próxima etapa da IA.
Um terceiro pedido (inspeção/tooltip em tempo real) foi **adiado** para um spec
próprio, por mexer no motor de inspeção globalmente e ter risco de performance/rede.

Este documento cobre apenas as Partes 1 e 2.

---

## Parte 1 — Cor da APC apenas na tela (+ luz)

### Problema

Quando a IA hackeia uma APC ou shunta o núcleo para dentro dela, **o sprite inteiro**
da APC muda de cor. O desejado é que **só a tela (display)** mude — o corpo da APC
fica intacto. O usuário também quer que a **luz emitida** (point light) acompanhe a cor.

Causa: em `Content.Client/Silicons/StationAi/StationAiSystem.cs`, o handler
`OnApcAppearanceChange` faz `args.Sprite.Color = ...`, que tinge **todas as camadas**.

### Solução

No mesmo handler, em vez de pintar o sprite todo:

1. Pintar **somente a camada da tela** `ApcVisualLayers.ChargeState` via
   `_sprite.LayerSetColor` (obter o índice com `LayerMapTryGet`).
2. Pintar a **point light** da APC com a mesma cor (`SharedPointLightSystem.SetColor`).
3. Estado normal (nem hacked nem occupied): restaurar a tela para branco e devolver a
   cor da luz ao comportamento padrão (deixar o visualizador padrão re-aplicar).

Cores mantidas como hoje:
- Occupied (IA shuntada dentro): laranja-âmbar `(1f, 0.55f, 0.1f)`
- Hacked (só fonte de CPU): vermelho `(1f, 0.2f, 0.2f)`
- Normal: branco

### Ponto de atenção — ordem de execução

`ApcVisualizerSystem.OnAppearanceChange` (vanilla) também roda no `AppearanceChangeEvent`
da mesma APC e **define a cor da luz** pela carga e o estado RSI da tela. Para nossa cor
não ser sobrescrita, nosso handler do fork deve rodar **depois** do visualizador padrão:
usar `SubscribeLocalEvent<StationAiApcControllableComponent, AppearanceChangeEvent>(...,
after: new[] { typeof(ApcVisualizerSystem) })`.

`LayerSetColor` na camada da tela é independente do `LayerSetRsiState` que o vanilla usa,
então a imagem da tela continua trocando com a carga; só a tinta muda.

### Arquivos

- `Content.Client/Silicons/StationAi/StationAiSystem.cs` (handler `OnApcAppearanceChange`
  e o `SubscribeLocalEvent` correspondente)

### Risco

Baixo. Mudança client-side, visual, num arquivo. Sem impacto em rede/servidor.

---

## Parte 2 — Clicar no mapa move o olho da IA

### Problema

Jogadores na IA demoram para arrastar a câmera até um lugar. Queremos: abrir um mapa
que a IA já tem (radar **e** monitor de tripulação, ambos com atalho de teclado nas
ações), clicar num ponto, e o olho da IA é teletransportado para lá. A janela fecha.

### Mecânica existente reutilizada

- O "olho" da IA é o `RemoteEntity` do `StationAiCoreComponent` (um `StationAiHolo`).
- Já existe `JumpToCoreEvent` que reposiciona entidades da IA via `SharedTransformSystem`
  (`SharedStationAiSystem.Held.cs`). Reaproveitamos a mesma ideia, mas para uma
  coordenada arbitrária clicada.
- Precedente de "clicar no mapa → coordenada do mundo": o console da nave
  (`MapScreen` / radar) usa clique para marcar destino de FTL; o monitor de câmeras de
  segurança (`SurveillanceCameraNavMapControl`) seleciona por clique. Reaproveitamos a
  conversão pixel → coordenada desses controles.

### Comportamento

- Clicar em **qualquer** ponto do mapa move o olho para lá — **sem** restrição de
  energia ou câmera (a IA do fork já enxerga livre).
- A coordenada é interpretada na **grade que o mapa exibe** (a estação/nave atual).
- Após o clique: o olho pula e a **janela do mapa fecha**.
- Clique "limpo" (sem arrastar) = mover; arrastar continua deslocando o mapa, para não
  atrapalhar a navegação normal da visão.
- No monitor de tripulação, clicar sobre um crachá leva o olho até aquele tripulante
  (efeito colateral desejado).

### Arquitetura

**Shared** — novo evento de rede dedicado (não acoplar aos BUIs genéricos do radar/crew):

```csharp
[Serializable, NetSerializable]
public sealed class StationAiMoveEyeEvent : EntityEventArgs
{
    public NetCoordinates Target;
}
```

Arquivo sugerido: `Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs`.

**Cliente** — em cada uma das duas janelas de mapa da IA:

- Radar: `Content.Client/Shuttles/UI/RadarConsoleWindow` + seu controle de radar.
- Monitor de tripulação: o `NavMapControl` usado pela `CrewMonitoringBoundUserInterface`.

Em cada uma: capturar o clique limpo na área do mapa, converter pixel → coordenada do
mundo na grade exibida, montar `StationAiMoveEyeEvent` e disparar via
`RaiseNetworkEvent`, depois fechar a janela.

Para não alterar o comportamento desses mapas quando usados por **outras** entidades
(consoles normais), o disparo só ocorre quando o ator local é a IA (possui
`StationAiHeldComponent`). Mapas abertos por não-IA seguem inalterados.

**Servidor** — handler em um sistema da IA (ex.: `StationAiSystem` server) que:

1. `SubscribeNetworkEvent<StationAiMoveEyeEvent>`.
2. Resolve o núcleo da IA a partir da sessão/ator (`TryGetCore`).
3. Valida que existe `RemoteEntity`.
4. Converte `NetCoordinates` → `EntityCoordinates` e move o `RemoteEntity`
   (`SharedTransformSystem.SetCoordinates`), espelhando o que `JumpToCore` faz.

### Arquivos

- `Content.Shared/Silicons/StationAi/StationAiMoveEyeEvent.cs` (novo)
- `Content.Client/Shuttles/UI/RadarConsoleWindow.xaml.cs` (ou controle de radar) —
  captura de clique + disparo + fechar
- A janela/controle do monitor de tripulação (`NavMapControl` da crew monitoring) —
  idem
- `Content.Server/Silicons/StationAi/StationAiSystem.cs` (ou arquivo parcial novo) —
  handler do evento + movimentação do olho

### Risco

Médio. Mexe em UI compartilhada (radar/navmap usados por outros consoles), então o
gatilho é condicionado a "ator é a IA". Movimento do olho reusa transform já existente.
Por ser mudança forte de gameplay da IA, vai para **staging (porta 1213)** antes de
produção, conforme as regras do projeto.

---

## Fora de escopo (adiado)

- **#3 Inspeção/tooltip em tempo real.** Exige re-consultar e redesenhar o texto de
  inspeção a cada tick enquanto a janela está aberta, afetando o sistema de inspeção
  global (não só a IA) e com custo de rede/performance. Vira spec próprio depois.

## Plano de teste

- Parte 1: hackear e shuntar uma APC em jogo; confirmar que só a tela muda de cor
  (corpo intacto) e que a luz acompanha; confirmar que volta ao normal ao reverter.
- Parte 2: como IA, abrir radar e monitor de tripulação; clicar em pontos variados e
  confirmar que o olho vai ao local e a janela fecha; confirmar que consoles normais
  (não-IA) seguem inalterados; confirmar que arrastar ainda desloca o mapa.
- Deploy em staging (1213) antes de produção (1212).
