using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
/// O servidor avisa o cliente se a conta dele esta vinculada ao site (honkenvironment.online).
/// O cliente usa isso para esconder o botao "Vincular ao site" e para confirmar na tela
/// quando o vinculo acabou de acontecer.
/// </summary>
[Serializable, NetSerializable]
public sealed class LinkSiteStatusEvent : EntityEventArgs
{
    public bool Vinculado;

    public LinkSiteStatusEvent(bool vinculado)
    {
        Vinculado = vinculado;
    }
}
