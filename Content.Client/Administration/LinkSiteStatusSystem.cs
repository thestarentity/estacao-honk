using System;
using Content.Shared.Administration;

namespace Content.Client.Administration;

/// <summary>
/// Guarda, no cliente, se a conta esta vinculada ao site (honkenvironment.online).
/// O servidor manda o status ao conectar e sempre que ele muda.
///
/// <see cref="StatusAtualizado"/> dispara em todo aviso (para esconder/mostrar o botao).
/// <see cref="AcabouDeVincular"/> dispara so na transicao "nao vinculado -> vinculado"
/// depois do primeiro aviso, para confirmar na tela sem falso positivo ao conectar.
/// </summary>
public sealed class LinkSiteStatusSystem : EntitySystem
{
    public bool Vinculado { get; private set; }

    public event Action<bool>? StatusAtualizado;
    public event Action? AcabouDeVincular;

    private bool _recebeuPrimeiro;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LinkSiteStatusEvent>(OnStatus);
    }

    private void OnStatus(LinkSiteStatusEvent ev)
    {
        var antes = Vinculado;
        Vinculado = ev.Vinculado;

        StatusAtualizado?.Invoke(Vinculado);

        if (_recebeuPrimeiro && !antes && Vinculado)
            AcabouDeVincular?.Invoke();

        _recebeuPrimeiro = true;
    }
}
