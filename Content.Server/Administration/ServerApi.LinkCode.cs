using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Robust.Server.ServerStatus;

namespace Content.Server.Administration;

/// <summary>
/// Endpoint do auto-vinculo do site: resolve o codigo gerado no jogo.
///
/// Requer:  Authorization: SS14Token &lt;admin.api_token&gt;
///
///   GET /admin/link/resolve?code=HONK-XXXX
///     200 -> { "UserId": "guid", "Username": "Nome" }  (e consome o codigo)
///     404 -> codigo invalido ou expirado
/// </summary>
public sealed partial class ServerApi
{
    private void _RegisterLinkEndpoints()
    {
        RegisterHandler(HttpMethod.Get, "/admin/link/resolve", ResolveLinkCode);
        RegisterHandler(HttpMethod.Post, "/admin/link/status", SetLinkStatus);
    }

    // ── POST /admin/link/status ────────────────────────────────────────────────
    //
    // O bot manda a lista COMPLETA de contas SS14 vinculadas ao site:
    //   { "UserIds": ["guid1", "guid2", ...] }
    // O servidor guarda e avisa os clientes cujo status mudou (esconde/mostra o botao
    // "Vincular ao site" e confirma na tela quem acabou de vincular).

    private async Task SetLinkStatus(IStatusHandlerContext context)
    {
        if (!await CheckAccess(context))
            return;

        var body = await ReadJson<LinkStatusBody>(context);
        if (body == null)
            return;

        var guids = new List<Guid>();
        if (body.UserIds != null)
        {
            foreach (var raw in body.UserIds)
            {
                if (Guid.TryParse(raw, out var g))
                    guids.Add(g);
            }
        }

        await RunOnMainThread(() =>
        {
            _entitySystemManager.GetEntitySystem<LinkCodeSystem>().AtualizarVinculados(guids);
        });

        await RespondOk(context);
    }

    private sealed class LinkStatusBody
    {
        public List<string>? UserIds { get; set; }
    }

    private async Task ResolveLinkCode(IStatusHandlerContext context)
    {
        if (!await CheckAccess(context))
            return;

        var query = ParseQuery(context.Url.Query);
        var code = query.GetValueOrDefault("code");
        if (string.IsNullOrWhiteSpace(code))
        {
            await RespondBadRequest(context, "Query param 'code' obrigatorio.");
            return;
        }

        var sys = _entitySystemManager.GetEntitySystem<LinkCodeSystem>();
        if (!sys.TryResolver(code, out var user, out var name))
        {
            await RespondError(context, ErrorCode.PlayerNotFound, HttpStatusCode.NotFound,
                "Codigo invalido ou expirado.");
            return;
        }

        await context.RespondJsonAsync(new LinkResolveResponse
        {
            UserId = user.UserId,
            Username = name,
        });
    }

    private sealed class LinkResolveResponse
    {
        public required Guid UserId { get; init; }
        public required string Username { get; init; }
    }
}
