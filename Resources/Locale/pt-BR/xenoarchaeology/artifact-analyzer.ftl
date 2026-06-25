analysis-console-menu-title = Console de Análise Mark 3 de Ampla Gama
analysis-console-server-list-button = Servidor
analysis-console-extract-button = Extraia pontos
analysis-console-info-no-scanner = Nenhum analisador conectado! Por favor, conecte um usando uma ferramenta multifuncional.
analysis-console-info-no-artifact = Nenhum artefato presente! Coloque um no suporte para visualizar informações do nó.
analysis-console-info-ready = Sistemas operacionais. Pronto para escanear.
analysis-console-no-node = Selecione um nó para visualizar
analysis-console-info-id = [font="Monospace" size=11]ID:[/font]
analysis-console-info-id-value = [font="Monospace" size=11][color=yellow]{ $id }[/color][/font]
analysis-console-info-class = [font="Monospace" size=11]Classe:[/font]
analysis-console-info-class-value = [font="Monospace" size=11]{ $class }[/font]
analysis-console-info-locked = [font="Monospace" size=11]Status:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
        [0] red]Bloqueado
        [1] lime]Desbloqueado
       *[2] plum]Ativo
    }[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]Durabilidade:[/font]
analysis-console-info-durability-value = [font="Monospace" size=11][color={ $color }]{ $current }/{ $max }[/color][/font]
analysis-console-info-effect = [font="Monospace" size=11]Efeito:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
        [true] { $info }
       *[false] Desbloqueie nós para obter informações
    }[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]Gatilhos:[/font]
analysis-console-info-triggered-value = [font="Monospace" size=11][color=gray]{ $triggers }[/color][/font]
analysis-console-info-scanner = Varredura...
analysis-console-info-scanner-paused = Pausado.
analysis-console-progress-text =
    { $seconds ->
        [one] T-{ $seconds } segundo
       *[other] T-{ $seconds } segundos
    }
analysis-console-extract-value = [font="Monospace" size=11][color=orange]Nó { $id } (+{ $value })[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] Nenhum nó desbloqueado possui pontos restantes para extrair [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Pesquisa Total: { $value }[/color][/font]
analyzer-artifact-extract-popup = Energia brilha na superfície do artefato!
