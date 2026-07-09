using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Comando que qualquer jogador pode usar para pegar/registrar um codigo de vinculo com o
/// site (auto-vinculo Discord&lt;-&gt;SS14, Fase 2 do site).
///
/// A janela "Vincular ao site" no lobby gera o codigo no cliente e chama
/// <c>vincularsite &lt;codigo&gt;</c> para registra-lo no servidor. Sem argumento (uso pelo
/// console), o servidor gera o codigo e o imprime.
/// </summary>
[AnyCommand]
public sealed partial class VincularSiteCommand : IConsoleCommand
{
    public string Command => "vincularsite";
    public string Description => "Gera/registra um codigo para vincular sua conta ao site honkenvironment.online.";
    public string Help => "Uso: vincularsite [codigo]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("So um jogador conectado pode usar este comando.");
            return;
        }

        var sys = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LinkCodeSystem>();

        string? code;
        if (args.Length >= 1)
        {
            code = sys.RegistrarCodigo(args[0], player.UserId, player.Name);
            if (code == null)
            {
                shell.WriteError("Codigo invalido.");
                return;
            }
        }
        else
        {
            code = sys.GerarCodigo(player.UserId, player.Name);
        }

        shell.WriteLine(
            $"Codigo de vinculo: {code} (vale 10 minutos). " +
            "Cole em honkenvironment.online/portal, entrando com o Discord.");
    }
}
