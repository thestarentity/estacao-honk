game-ticker-restart-round = Reiniciando rodada...
game-ticker-start-round = A rodada está começando agora...
game-ticker-start-round-cannot-start-game-mode-fallback = Falha ao iniciar o modo { $failedGameMode }! Alternando para o modo { $fallbackMode }...
game-ticker-start-round-cannot-start-game-mode-restart = Falha ao iniciar o modo { $failedGameMode }! Reiniciando a rodada...
game-ticker-start-round-invalid-map = O mapa selecionado { $map } não é elegível para o modo de jogo { $mode }. O modo de jogo pode não funcionar conforme o esperado...
game-ticker-unknown-role = Desconhecido
game-ticker-delay-start = O início da rodada foi adiado por { $seconds } segundos.
game-ticker-pause-start = O início da rodada foi pausado.
game-ticker-pause-start-resumed = O contador de início da rodada agora foi retomado.
game-ticker-player-join-game-message = Bem-vindo à Space Station 14! Se é a primeira vez que você joga, certifique-se de ler as regras do jogo, e não tenha medo de pedir ajuda no LOOC (OOC local) ou no OOC (normalmente disponível apenas entre rodadas).
game-ticker-get-info-text =
    Olá e bem-vindo à [color=white]Space Station 14![/color]
    A rodada atual é: [color=white]#{ $roundId }[/color]
    A quantidade atual de jogadores é: [color=white]{ $playerCount }[/color]
    O mapa atual é: [color=white]{ $mapName }[/color]
    O modo de jogo atual é: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-get-info-preround-text =
    Olá e bem-vindo à [color=white]Space Station 14![/color]
    A rodada atual é: [color=white]#{ $roundId }[/color]
    A contagem atual de jogadores é: [color=white]{ $playerCount }[/color] ([color=white]{ $readyCount }[/color] { $readyCount ->
        [one] pronto
       *[other] prontos
    })
    O mapa atual é: [color=white]{ $mapName }[/color]
    O modo de jogo atual é: [color=white]{ $gmTitle }[/color]
    >[color=yellow]{ $desc }[/color]
game-ticker-no-map-selected = [color=yellow]Mapa ainda não selecionado![/color]
game-ticker-player-no-jobs-available-when-joining = Ao tentar entrar no jogo, nenhum cargo estava disponível.
# Displayed in chat to admins when a player joins
player-join-message = O jogador { $name } entrou.
player-first-join-message = O jogador { $name } se juntou pela primeira vez.
# Displayed in chat to admins when a player leaves
player-leave-message = O jogador { $name } saiu.
latejoin-arrival-announcement = { $character } ({ $job }) chegou à estação!
latejoin-arrival-announcement-special = { $job } { $character } a bordo!
latejoin-arrival-sender = Estação
latejoin-arrivals-direction = Um shuttle que vai transferi-lo para sua estação chegará em breve.
latejoin-arrivals-direction-time = Um shuttle que vai transferi-lo para sua estação chegará em { $time }.
latejoin-arrivals-dumped-from-shuttle = Uma força misteriosa impede você de sair com o shuttle de chegada.
latejoin-arrivals-teleport-to-spawn = Uma força misteriosa teleporta você do shuttle de chegadas. Tenha um turno seguro!
preset-not-enough-ready-players = Não é possível iniciar { $presetName }. Requer { $minimumPlayers } jogadores, mas temos { $readyPlayersCount }.
preset-no-one-ready = Não é possível iniciar { $presetName }. Nenhum jogador está pronto.
game-run-level-PreRoundLobby = Lobby pré-jogo
game-run-level-InRound = Em jogo
game-run-level-PostRound = Pós-rodada
