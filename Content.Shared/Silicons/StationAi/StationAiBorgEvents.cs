using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Ação da IA de estação para subverter um borg pelo menu radial (segurar Alt + clicar no borg).
/// Só aparece e só funciona sob um lawset hostil. Reaproveita o pipeline de emag: o borg ganha a
/// lei "obedeça à IA" e o papel de silício subvertido, passando a servir a IA.
/// É um gatilho simples (sem payload): o servidor decide o efeito a partir do estado do borg.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiSubvertBorgEvent : BaseStationAiAction
{
}

/// <summary>
/// Ação da IA para desligar um borg pelo menu radial (só sob lei hostil). Reaproveita o mesmo
/// "disable" do console de robótica (ejeta o cérebro do borg após um curto atraso). Um borg
/// subvertido finge desligar, mas continua funcional (proteção já existente do emag).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiDisableBorgEvent : BaseStationAiAction
{
}

/// <summary>
/// Ação da IA para detonar (explodir) um borg pelo menu radial (só sob lei hostil + confirmação
/// por duplo-clique). Reaproveita o mesmo "destroy" do console de robótica. Um borg subvertido
/// não explode de fato (proteção já existente do emag).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiDetonateBorgEvent : BaseStationAiAction
{
}

/// <summary>
/// Ação da IA para trancar/destrancar o painel de manutenção de um borg pelo menu radial.
/// Disponível sob qualquer lei (uso defensivo ou ofensivo). É um toggle: o cliente decide
/// <see cref="Lock"/> a partir do estado atual (tem ou não <c>StationAiBorgPanelLockComponent</c>).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiTogglePanelLockEvent : BaseStationAiAction
{
    public bool Lock;
}

/// <summary>
/// Ação da IA para imobilizar/liberar um borg pelo menu radial (enraíza no lugar, zerando a
/// velocidade). Disponível sob qualquer lei. Toggle: o cliente decide <see cref="Immobilize"/>
/// a partir do estado atual (tem ou não <c>StationAiBorgImmobilizedComponent</c>).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiToggleImmobilizeEvent : BaseStationAiAction
{
    public bool Immobilize;
}

/// <summary>
/// Ação da IA para trancar/destrancar o BORG em si (o <c>LockComponent</c>, que normalmente exige
/// ID com acesso). Trancado, o borg não pode ter os módulos reconfigurados (UI bloqueada por
/// <c>UIRequiresLock</c>). Disponível sob qualquer lei. Toggle: o cliente decide <see cref="Lock"/>
/// a partir do estado atual do lock.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiToggleBorgLockEvent : BaseStationAiAction
{
    public bool Lock;
}

/// <summary>
/// Ação da IA para ASSUMIR o controle de um borg VAZIO pelo menu radial (Fase 5A). A mente da IA
/// continua DONA do cérebro no núcleo e passa a VISITAR o borg (<c>MindSystem.Visit</c>) — a IA vira
/// um borg comum, perde a câmera de estação e usa os módulos do borg. Disponível sob qualquer lei
/// (borg vazio). Só funciona em borg SEM jogador; o servidor reconfirma. Para voltar ao núcleo, a IA
/// usa a ação <c>ActionStationAiLeaveBorg</c> (<see cref="StationAiLeaveBorgEvent"/>).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiControlBorgEvent : BaseStationAiAction
{
}

/// <summary>
/// Ação instantânea concedida ao borg enquanto a IA o pilota (Fase 5A): devolve a mente da IA ao
/// núcleo (<c>MindSystem.UnVisit</c>). É um <see cref="InstantActionEvent"/> (funciona porque,
/// pilotando o borg, a IA é um mob comum — diferente do olho-holo, onde InstantAction não dispara).
/// </summary>
public sealed partial class StationAiLeaveBorgEvent : InstantActionEvent
{
}
