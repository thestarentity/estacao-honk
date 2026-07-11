### UI

chat-manager-max-message-length = Sua mensagem excede o limite de { $maxMessageLength } caracteres
chat-manager-ooc-chat-enabled-message = O chat OOC foi ativado.
chat-manager-ooc-chat-disabled-message = O chat OOC foi desativado.
chat-manager-looc-chat-enabled-message = O chat LOOC foi ativado.
chat-manager-looc-chat-disabled-message = O chat LOOC foi desabilitado.
chat-manager-dead-looc-chat-enabled-message = Jogadores mortos podem agora usar LOOC.
chat-manager-dead-looc-chat-disabled-message = Jogadores mortos não podem mais usar LOOC.
chat-manager-crit-looc-chat-enabled-message = Jogadores Crit podem agora usar LOOC.
chat-manager-crit-looc-chat-disabled-message = Os jogadores Críticos não podem mais usar o LOOC.
chat-manager-admin-ooc-chat-enabled-message = O chat OOC do administrador foi ativado.
chat-manager-admin-ooc-chat-disabled-message = O chat OOC do administrador foi desativado.
chat-manager-max-message-length-exceeded-message = Sua mensagem excedeu o limite de { $limit } caracteres
chat-manager-no-headset-on-message = Você não está usando um headset!
chat-manager-no-radio-key = Nenhuma chave de rádio especificada!
chat-manager-no-such-channel = Não existe um canal com a chave '{ $key }'!
chat-manager-whisper-headset-on-message = Você não pode sussurrar no rádio!
chat-manager-server-wrap-message = [bold]{ $message }[/bold]
chat-manager-sender-announcement = Comando Central
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{ $sender } Anúncio:[/font][font size=12]
    { $message }[/bold][/font]
chat-manager-entity-say-wrap-message = [BubbleHeader][bold][Name]{ $entityName }[/Name][/bold][/BubbleHeader] { $verb }, [font={ $fontType } size={ $fontSize }]{ chat-manager-speech-double-quote-begin }[BubbleContent]{ $message }[/BubbleContent]{ chat-manager-speech-double-quote-end }[/font]
chat-manager-entity-say-bold-wrap-message = [BubbleHeader][bold][Name]{ $entityName }[/Name][/bold][/BubbleHeader] { $verb }, [font={ $fontType } size={ $fontSize }]{ chat-manager-speech-double-quote-begin }[BubbleContent][bold]{ $message }[/bold][/BubbleContent]{ chat-manager-speech-double-quote-end }[/font]
chat-manager-entity-whisper-wrap-message = [font size=11][italic][BubbleHeader][Name]{ $entityName }[/Name][/BubbleHeader] sussurra,{ chat-manager-speech-double-quote-begin }[BubbleContent]{ $message }[/BubbleContent]{ chat-manager-speech-double-quote-end }[/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [font size=11][italic][BubbleHeader]Alguém[/BubbleHeader] sussurra, { chat-manager-speech-double-quote-begin }[BubbleContent]{ $message }[/BubbleContent]{ chat-manager-speech-double-quote-end }[/italic][/font]
# THE() is not used here because the entity and its name can technically be disconnected if a nameOverride is passed...
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
       *[false] O { $entityName } { $message }[/italic]
        [true] { CAPITALIZE($entityName) } { $message }[/italic]
    }
chat-manager-entity-looc-wrap-message = LOOC: [bold]{ $entityName }:[/bold] { $message }
chat-manager-send-ooc-wrap-message = OOC: [bold]{ $playerName }:[/bold] { $message }
chat-manager-send-ooc-patron-wrap-message = OOC: [bold][color={ $patronColor }]{ $playerName }[/color]:[/bold] { $message }
chat-manager-send-dead-chat-wrap-message = { $deadChannelName }: [bold][BubbleHeader]{ $playerName }[/BubbleHeader]:[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = { $adminChannelName }: [bold]([BubbleHeader]{ $userName }[/BubbleHeader]):[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = { $adminChannelName }: [bold]{ $playerName }:[/bold] { $message }
chat-manager-send-admin-announcement-wrap-message = [bold]{ $adminChannelName }: { $message }[/bold]
chat-manager-send-hook-ooc-wrap-message = OOC: [bold](D){ $senderName }:[/bold] { $message }
chat-manager-send-hook-admin-wrap-message = ADMIN: [bold](D){ $senderName }:[/bold] { $message }
chat-manager-dead-channel-name = MORTO
chat-manager-admin-channel-name = ADMIN
chat-manager-rate-limited = Você está enviando mensagens com muita rapidez!
chat-manager-rate-limit-admin-announcement = Aviso de limite de taxa: { $player }

## Speech verbs for chat

chat-speech-verb-suffix-exclamation = !
chat-speech-verb-suffix-exclamation-strong = !!
chat-speech-verb-suffix-question = ?
chat-speech-verb-suffix-stutter = -
chat-speech-verb-suffix-mumble = ..
chat-speech-verb-name-none = Nenhum
chat-speech-verb-name-default = Padrão
chat-speech-verb-default = diz
chat-speech-verb-name-exclamation = Exclamando
chat-speech-verb-exclamation = exclama
chat-speech-verb-name-exclamation-strong = Gritando
chat-speech-verb-exclamation-strong = grita
chat-speech-verb-name-question = Perguntando
chat-speech-verb-question = pergunta
chat-speech-verb-name-stutter = Gaguejando
chat-speech-verb-stutter = gagueja
chat-speech-verb-name-mumble = Murmurando
chat-speech-verb-mumble = murmura
chat-speech-verb-name-arachnid = Aracnídeo
chat-speech-verb-insect-1 = trisca
chat-speech-verb-insect-2 = chia
chat-speech-verb-insect-3 = estala
chat-speech-verb-name-moth = Mariposa
chat-speech-verb-winged-1 = bate asas
chat-speech-verb-winged-2 = agita as asas
chat-speech-verb-winged-3 = zumbe
chat-speech-verb-name-slime = Slime
chat-speech-verb-slime-1 = escorrega
chat-speech-verb-slime-2 = borbulha
chat-speech-verb-slime-3 = pinga
chat-speech-verb-name-plant = Diona
chat-speech-verb-plant-1 = sussurra
chat-speech-verb-plant-2 = balança
chat-speech-verb-plant-3 = range
chat-speech-verb-name-robotic = Robótico
chat-speech-verb-robotic-1 = informa
chat-speech-verb-robotic-2 = bipa
chat-speech-verb-robotic-3 = apita
chat-speech-verb-name-reptilian = Réptil
chat-speech-verb-reptilian-1 = sibila
chat-speech-verb-reptilian-2 = bufa
chat-speech-verb-reptilian-3 = resfolega
chat-speech-verb-name-skeleton = Esqueleto
chat-speech-verb-skeleton-1 = chacoalha
chat-speech-verb-skeleton-2 = estala
chat-speech-verb-skeleton-3 = range os dentes
chat-speech-verb-name-vox = Vox
chat-speech-verb-vox-1 = grasna
chat-speech-verb-vox-2 = berra
chat-speech-verb-vox-3 = coaxa
chat-speech-verb-name-canine = Canino
chat-speech-verb-canine-1 = late
chat-speech-verb-canine-2 = uiva
chat-speech-verb-canine-3 = uiva
chat-speech-verb-name-goat = Cabra
chat-speech-verb-goat-1 = bale
chat-speech-verb-goat-2 = grunhe
chat-speech-verb-goat-3 = berra
chat-speech-verb-name-sheep = Ovelha
chat-speech-verb-sheep-1 = bale
chat-speech-verb-sheep-2 = mée
chat-speech-verb-name-small-mob = Rato
chat-speech-verb-small-mob-1 = chia
chat-speech-verb-small-mob-2 = piia
chat-speech-verb-name-large-mob = Carpa
chat-speech-verb-large-mob-1 = ruge
chat-speech-verb-large-mob-2 = rosna
chat-speech-verb-name-monkey = Macaco
chat-speech-verb-monkey-1 = chocalha
chat-speech-verb-monkey-2 = grasna
chat-speech-verb-name-cluwne = Cluwne
chat-speech-verb-name-parrot = Papagaio
chat-speech-verb-parrot-1 = grasna
chat-speech-verb-parrot-2 = canta
chat-speech-verb-parrot-3 = chia
chat-speech-verb-cluwne-1 = ri
chat-speech-verb-cluwne-2 = gargalha
chat-speech-verb-cluwne-3 = dá risada
chat-speech-verb-name-ghost = Fantasma
chat-speech-verb-ghost-1 = reclama
chat-speech-verb-ghost-2 = sussurra
chat-speech-verb-ghost-3 = tarareia
chat-speech-verb-ghost-4 = murmura
chat-speech-verb-name-electricity = Eletricidade
chat-speech-verb-electricity-1 = crepita
chat-speech-verb-electricity-2 = zumbe
chat-speech-verb-electricity-3 = faísca
chat-speech-verb-vulpkanin-1 = rosna
chat-speech-verb-vulpkanin-2 = late
chat-speech-verb-vulpkanin-3 = ronrona
chat-speech-verb-vulpkanin-4 = ganhe
chat-speech-verb-vulpkanin = Vulpkanin
chat-speech-verb-name-wawa = Wawa
chat-speech-verb-wawa-1 = entoa
chat-speech-verb-wawa-2 = declara
chat-speech-verb-wawa-3 = proclama
chat-speech-verb-wawa-4 = pondera
chat-manager-dead-chat-enabled-message = O chat dos mortos foi ativado.
chat-manager-dead-chat-disabled-message = O chat dos mortos foi desativado.
# Aspas duplas Unicode U+201C e U+201D.
chat-manager-speech-double-quote-begin = “
chat-manager-speech-double-quote-end = ”
chat-manager-follow-button = (F)
