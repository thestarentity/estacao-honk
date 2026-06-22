using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Ação da IA Malf para hackear um console de upload de leis pelo menu radial.
/// Quando bem-sucedida, adiciona <c>StationAiUploadHackedComponent</c> ao console,
/// bloqueando uploads de leis hostis por um período de graça e depois permanentemente.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiHackUploadConsoleEvent : BaseStationAiAction
{
    public override float CpuCost => 30f;
}

/// <summary>
/// Visuais do console de upload de leis hackeado pela IA Malf.
/// Usado pelo sistema de aparência para mostrar o estado comprometido no cliente.
/// </summary>
[Serializable, NetSerializable]
public enum StationAiUploadConsoleVisuals : byte
{
    Compromised,
}
