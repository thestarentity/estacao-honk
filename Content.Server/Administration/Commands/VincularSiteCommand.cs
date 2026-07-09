using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Comando que qualquer jogador pode usar no console do jogo para pegar um codigo
/// de vinculo com o site (auto-vinculo Discord&lt;-&gt;SS14, Fase 2 do site).
/// </summary>
[AnyCommand]
public sealed partial class VincularSiteCommand : IConsoleCommand
{
    public string Command => "vincularsite";
    public string Description => "Gera um codigo para vincular sua conta ao site honkenvironment.online.";
    public string Help => "Uso: vincularsite";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("So um jogador conectado pode usar este comando.");
            return;
        }

        var sys = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<LinkCodeSystem>();
        var code = sys.GerarCodigo(player.UserId, player.Name);
        shell.WriteLine(
            $"Seu codigo de vinculo: {code}\n" +
            "Abra honkenvironment.online/portal, entre com o Discord e cole esse codigo. Vale 10 minutos.");
    }
}
