using System;
using System.Collections.Generic;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Administration;

/// <summary>
/// Guarda codigos curtos de vinculo (auto-vinculo Discord&lt;-&gt;SS14 iniciado no site).
///
/// O jogador usa o comando <c>vincularsite</c> (ou o botao no lobby) e recebe um codigo
/// tipo <c>HONK-4821</c>. O site manda esse codigo ao bot, que pergunta ao servidor de
/// quem ele e pelo endpoint <c>GET /admin/link/resolve</c>. O codigo vale 10 minutos e e
/// de uso unico.
///
/// O dicionario e protegido por lock porque a geracao roda na thread do jogo (comando de
/// console) e a resolucao roda na thread da API HTTP.
/// </summary>
public sealed partial class LinkCodeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    // Sem 0/O/1/I para nao confundir quem digita.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly object _lock = new();
    private readonly Dictionary<string, (NetUserId User, string Name, DateTime Expira)> _codes = new();

    // Contas SS14 que estao vinculadas ao site. O bot manda a lista completa periodicamente.
    private readonly HashSet<Guid> _vinculados = new();
    private bool _recebeuLista;

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        // So avisa depois que o bot mandou a lista pelo menos uma vez; senao o cliente
        // receberia "nao vinculado" e depois "vinculado", disparando a confirmacao a toa.
        if (!_recebeuLista)
            return;
        if (e.NewStatus != SessionStatus.Connected && e.NewStatus != SessionStatus.InGame)
            return;
        EnviarStatus(e.Session);
    }

    private void EnviarStatus(ICommonSession session)
    {
        bool vinculado;
        lock (_lock)
        {
            vinculado = _vinculados.Contains(session.UserId.UserId);
        }
        RaiseNetworkEvent(new LinkSiteStatusEvent(vinculado), session);
    }

    /// <summary>Diz se a conta esta vinculada ao site.</summary>
    public bool EstaVinculado(Guid user)
    {
        lock (_lock)
        {
            return _vinculados.Contains(user);
        }
    }

    /// <summary>
    /// Recebe do bot a lista COMPLETA de contas vinculadas e avisa os clientes cujo status mudou.
    /// Roda na main thread (chamado pelo endpoint via RunOnMainThread).
    /// </summary>
    public void AtualizarVinculados(IEnumerable<Guid> ids)
    {
        var novo = new HashSet<Guid>(ids);
        var primeira = !_recebeuLista;
        var mudaram = new HashSet<Guid>();

        lock (_lock)
        {
            foreach (var g in novo)
            {
                if (!_vinculados.Contains(g))
                    mudaram.Add(g);
            }
            foreach (var g in _vinculados)
            {
                if (!novo.Contains(g))
                    mudaram.Add(g);
            }
            _vinculados.Clear();
            foreach (var g in novo)
                _vinculados.Add(g);
        }

        _recebeuLista = true;

        foreach (var session in _playerManager.Sessions)
        {
            // Na primeira lista, todo mundo recebe o status inicial (o cliente nao mostra
            // confirmacao no primeiro aviso). Depois, so quem mudou.
            if (primeira || mudaram.Contains(session.UserId.UserId))
                EnviarStatus(session);
        }
    }

    /// <summary>Gera e guarda um codigo novo para o jogador. Chamado da thread do jogo.</summary>
    public string GerarCodigo(NetUserId user, string name)
    {
        lock (_lock)
        {
            LimparExpirados();
            string code;
            do
            {
                code = "HONK-" + AleatorioStr(4);
            } while (_codes.ContainsKey(code));

            _codes[code] = (user, name, DateTime.UtcNow + Ttl);
            return code;
        }
    }

    /// <summary>Registra um codigo escolhido pelo cliente (a janela do lobby gera e mostra o
    /// codigo, e pede pro servidor guarda-lo). Devolve o codigo normalizado, ou null se invalido.</summary>
    public string? RegistrarCodigo(string? code, NetUserId user, string name)
    {
        var chave = (code ?? "").Trim().ToUpperInvariant();
        if (chave.Length < 4 || chave.Length > 24)
            return null;
        foreach (var ch in chave)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '-')
                return null;
        }
        lock (_lock)
        {
            LimparExpirados();
            _codes[chave] = (user, name, DateTime.UtcNow + Ttl);
        }
        return chave;
    }

    /// <summary>Resolve e consome um codigo. Chamado da thread da API HTTP.</summary>
    public bool TryResolver(string? code, out NetUserId user, out string name)
    {
        user = default;
        name = "";
        var chave = (code ?? "").Trim().ToUpperInvariant();
        if (chave.Length == 0)
            return false;

        lock (_lock)
        {
            LimparExpirados();
            if (!_codes.TryGetValue(chave, out var valor))
                return false;

            _codes.Remove(chave);
            user = valor.User;
            name = valor.Name;
            return true;
        }
    }

    private void LimparExpirados()
    {
        var agora = DateTime.UtcNow;
        List<string>? expirados = null;
        foreach (var (k, v) in _codes)
        {
            if (v.Expira < agora)
                (expirados ??= new List<string>()).Add(k);
        }
        if (expirados == null)
            return;
        foreach (var k in expirados)
            _codes.Remove(k);
    }

    private string AleatorioStr(int n)
    {
        var chars = new char[n];
        for (var i = 0; i < n; i++)
            chars[i] = Alfabeto[_random.Next(Alfabeto.Length)];
        return new string(chars);
    }
}
