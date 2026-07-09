using System;
using System.Collections.Generic;
using Robust.Shared.Network;
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

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    // Sem 0/O/1/I para nao confundir quem digita.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly object _lock = new();
    private readonly Dictionary<string, (NetUserId User, string Name, DateTime Expira)> _codes = new();

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
