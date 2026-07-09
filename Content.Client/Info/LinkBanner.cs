using Content.Client.Administration;
using Content.Client.Changelog;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        private readonly IConfigurationManager _cfg;
        private readonly Button _vincularButton;
        private LinkSiteStatusSystem? _statusSys;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        public LinkBanner()
        {
            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();

            var rulesButton = new Button() {Text = Loc.GetString("server-info-rules-button")};
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button() { Text = Loc.GetString("server-info-guidebook-button") };
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton();
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            buttons.AddChild(changelogButton);

            _vincularButton = new Button { Text = "Vincular ao site" };
            _vincularButton.OnPressed += _ => new Content.Client.Lobby.UI.VincularSiteWindow().OpenCentered();
            buttons.AddChild(_vincularButton);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }

            // Esconde o botao "Vincular ao site" se a conta ja esta vinculada.
            if (IoCManager.Resolve<IEntitySystemManager>()
                .TryGetEntitySystem<LinkSiteStatusSystem>(out var sys))
            {
                _statusSys = sys;
                _vincularButton.Visible = !sys.Vinculado;
                sys.StatusAtualizado += OnVinculoStatus;
                sys.AcabouDeVincular += OnAcabouDeVincular;
            }
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            if (_statusSys == null)
                return;

            _statusSys.StatusAtualizado -= OnVinculoStatus;
            _statusSys.AcabouDeVincular -= OnAcabouDeVincular;
            _statusSys = null;
        }

        private void OnVinculoStatus(bool vinculado)
        {
            _vincularButton.Visible = !vinculado;
        }

        private void OnAcabouDeVincular()
        {
            var win = new DefaultWindow { Title = "Vincular ao site" };
            var box = new BoxContainer { Orientation = LayoutOrientation.Vertical };
            box.AddChild(new Label
            {
                Text = "Sua conta foi vinculada ao site com sucesso.",
            });
            var fechar = new Button { Text = "Fechar" };
            fechar.OnPressed += _ => win.Close();
            box.AddChild(fechar);
            win.Contents.AddChild(box);
            win.OpenCentered();
        }
    }
}
