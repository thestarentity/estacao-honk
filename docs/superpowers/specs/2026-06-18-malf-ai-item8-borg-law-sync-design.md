# Item 8 — Sincronizar leis do borg com as da IA (Fase 5A)

**Data:** 2026-06-18
**Contexto:** Item 8 da lista de ajustes do Bloco 1.5 da IA Malf. Relacionado à Fase 5A
(IA controla borg vazio). Ver `malf-cpu-bloco1-ajustes.md` no vault e a memória
`ss14-malf-ai-design`.

## Objetivo

Quando a IA de estação **assume** um borg vazio (Fase 5A), as leis do borg passam a ser
as leis **atuais da IA**. Quando a IA **larga** o borg (por ação, morte, detonação ou
deleção), as leis anteriores do borg são **restauradas**.

Motivação: consistência. Uma IA Malf que pilota um borg deve operar sob as leis Malf;
ao sair, o borg volta ao que era. Para uma IA leal o efeito é nulo na prática (as leis
já coincidem com as da estação), mas sincronizar sempre mantém o código simples.

## Decisões de design (aprovadas pelo usuário — opção A)

1. **Silencioso:** a sincronização NÃO dispara o aviso de "leis alteradas" na tela. A IA
   já conhece as próprias leis; o aviso vermelho a cada entrada/saída do borg seria ruído.
2. **Qualquer IA:** sincroniza para IA leal E Malf (sem checar lei hostil). Mais simples;
   inofensivo para a leal.

## Fundação reusada (confirmada no código)

- `StationAiBorgSystem.OnControlBorg` — ponto onde a IA assume o borg (já salva acesso,
  ativa o chassi, etc.). É aqui que sincronizamos.
- `StationAiBorgSystem.StopPiloting` — ponto único de saída (ação "voltar", morte,
  detonação, deleção todos passam por aqui). É aqui que restauramos.
- `StationAiPilotedBorgComponent` — marcador server-only que já guarda estado a desfazer
  (acesso). Ganha um campo para as leis salvas.
- `SiliconLawSystem.GetLaws(uid)` — lê as leis efetivas de uma entidade.
- `SiliconLawSystem.SetLaws(laws, target, cue)` — grava leis num alvo com `SiliconLawProvider`.
- `StationAiBrain` (= `args.User`) é `SiliconLawProvider` + `SiliconLawBound`.
- Todo borg (`base_borg_chassis`) tem `SiliconLawProvider` (laws `Crewsimov`), então
  `SetLaws` funciona direto, sem adicionar componente.

## Mudanças

### 1. `StationAiPilotedBorgComponent` (server)
Novo campo:
```csharp
/// Leis originais do borg, salvas ao assumir, para restaurar ao largar.
[DataField]
public List<SiliconLaw>? SavedLaws;
```

### 2. `SiliconLawSystem.SetLaws` (server)
Adicionar um parâmetro opcional para suprimir a notificação, sem mudar os chamadores
existentes:
```csharp
public void SetLaws(List<SiliconLaw> newLaws, EntityUid target, SoundSpecifier? cue = null, bool notify = true)
```
Quando `notify == false`, grava `component.Lawset.Laws` sem chamar `NotifyLawsChanged`.
A tela de leis lê as leis vivas em `OnBoundUIOpened`/`GetLaws`, então a UI continua
correta mesmo silenciosa.

### 3. `StationAiBorgSystem` (server)
- Dependência nova: `SiliconLawSystem _laws`.
- Em `OnControlBorg`, depois do bloco de acesso e antes do log/popup de sucesso:
  - `piloted.SavedLaws = GetLaws(uid).Laws;` (snapshot das leis atuais do borg)
  - `SetLaws(<cópia das leis da IA>, uid, notify: false)` onde a cópia vem de
    `GetLaws(args.User).Laws.Select(l => l.ShallowClone()).ToList()` — cópia independente
    para o borg e a IA não compartilharem a mesma lista.
- Em `StopPiloting`, junto da restauração de acesso:
  - se `comp.SavedLaws != null`: `SetLaws(comp.SavedLaws, borg, notify: false)`.

## Fora de escopo

- Borg COM jogador (Fase 5B) — não existe no fork; só pilotamos borg vazio.
- Sincronização contínua: se as leis da IA mudarem ENQUANTO ela pilota (ex.: tempestade
  iônica no núcleo), o borg não acompanha em tempo real. Faz-se um snapshot ao assumir.
  É aceitável para o escopo (a IA volta ao núcleo para "re-sincronizar").

## Como testar (staging, porta 1213)

1. Logar como IA, virar Malf (verbo de admin "Tornar IA Malf").
2. Abrir as leis (deve mostrar leis Malf).
3. Alt-clicar num borg vazio → "Controlar borg".
4. Pilotando, abrir as leis do borg → deve mostrar as leis Malf, **sem** aviso vermelho.
5. "Voltar ao núcleo" → as leis do borg voltam a Crewsimov.
6. Repetir com IA leal: borg segue Crewsimov antes, durante e depois (sem efeito visível,
   sem erro).
