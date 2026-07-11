using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems
{
    public sealed partial class BwoinkSystem
    {
        // ── Métodos para integração com o bot Discord ─────────────────────────

        /// <summary>
        /// Drena e retorna todas as entradas de ahelp pendentes para o bot consumir.
        /// </summary>
        public List<BotAhelpEntry> DrainBotRelayQueue()
        {
            var entries = new List<BotAhelpEntry>(_botPendingQueue);
            _botPendingQueue.Clear();
            return entries;
        }

        /// <summary>
        /// Envia uma resposta do bot Discord para o jogador in-game via bwoink.
        /// Retorna false se o jogador não estiver online.
        /// </summary>
        public bool SendBotResponse(NetUserId userId, string senderName, string text)
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
                return false;

            var escaped = FormattedMessage.EscapeText(text);
            var bwoinkText = $"[color=red]{FormattedMessage.EscapeText(senderName)}[/color]: {escaped}";
            var msg = new BwoinkTextMessage(userId, SystemUserId, bwoinkText, playSound: true);

            // Notifica o jogador
            RaiseNetworkEvent(msg, session.Channel);

            // Notifica admins online
            foreach (var channel in GetTargetAdmins())
                RaiseNetworkEvent(msg, channel);

            return true;
        }

        private void EnqueueForBot(NetUserId userId, string userName, string text, bool isAdmin)
        {
            _botPendingQueue.Add(new BotAhelpEntry
            {
                UserId    = userId.UserId.ToString(),
                UserName  = userName,
                Text      = text,
                IsAdmin   = isAdmin,
                RoundId   = _gameTicker.RoundId,
                Timestamp = DateTime.UtcNow,
            });
        }
    }

    public sealed class BotAhelpEntry
    {
        public required string   UserId    { get; init; }
        public required string   UserName  { get; init; }
        public required string   Text      { get; init; }
        public required bool     IsAdmin   { get; init; }
        public required int      RoundId   { get; init; }
        public required DateTime Timestamp { get; init; }
    }
}
