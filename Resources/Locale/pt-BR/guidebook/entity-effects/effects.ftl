-create-3rd-person =
    { $chance ->
        [1] Cria
       *[other] criar
    }
-cause-3rd-person =
    { $chance ->
        [1] Causas
       *[other] causar
    }
-satiate-3rd-person =
    { $chance ->
        [1] Satisfaça
       *[other] satisfazer
    }
entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Cria
       *[other] criar
    } { $amount ->
        [1] { INDEFINITE($entname) }
       *[other] { $amount } { MAKEPLURAL($entname) }
    }
entity-effect-guidebook-destroy =
    { $chance ->
        [1] Destroi
       *[other] destruir
    } O objeto
entity-effect-guidebook-break =
    { $chance ->
        [1] Quebra
       *[other] Quebre
    } O objeto
entity-effect-guidebook-explosion =
    { $chance ->
        [1] Causas
       *[other] causar
    } uma explosão
entity-effect-guidebook-emp =
    { $chance ->
        [1] Causas
       *[other] causar
    } um pulso eletromagnético
entity-effect-guidebook-flash =
    { $chance ->
        [1] Causas
       *[other] causar
    } um flash cegante
entity-effect-guidebook-foam-area =
    { $chance ->
        [1] Cria
       *[other] criar
    } grandes quantidades de espuma
entity-effect-guidebook-smoke-area =
    { $chance ->
        [1] Cria
       *[other] criar
    } grandes quantidades de fumaça
entity-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Satisfaça
       *[other] satisfazer
    } { $relative ->
        [1] sede em média
       *[other] sede a { NATURALFIXED($relative, 3) }x da taxa média
    }
entity-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Satisfaça
       *[other] satisfazer
    } { $relative ->
        [1] fome em média
       *[other] fome a { NATURALFIXED($relative, 3) }x a taxa média
    }
entity-effect-guidebook-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Cura
                [deals] Causa
               *[both] Modifica a saúde por
            }
       *[other]
            { $healsordeals ->
                [heals] curar
                [deals] causar
               *[both] modificar saúde por
            }
    } { $changes }
entity-effect-guidebook-even-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Cura igualmente
                [deals] Distribui igualmente
               *[both] Modifica a saúde de forma uniforme por
            }
       *[other]
            { $healsordeals ->
                [heals] curar igualmente
                [deals] dividir igualmente
               *[both] modificar a saúde de forma uniforme por
            }
    } { $changes }
entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]
            { $chance ->
                [1] Causas
               *[other] causar
            } { LOC($key) } por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) } sem acúmulo
        [add]
            { $chance ->
                [1] Causas
               *[other] causar
            } { LOC($key) } por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) } com acúmulo
        [set]
            { $chance ->
                [1] Causas
               *[other] causar
            } { LOC($key) } para { NATURALFIXED($time, 3) } { MANY("second", $time) } sem acúmulo
       *[remove]
            { $chance ->
                [1] Remove
               *[other] Remover
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } de { LOC($key) }
    }
entity-effect-guidebook-status-effect =
    { $type ->
        [update]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } por pelo menos { NATURALFIXED($time, 3) } { MANY("segundo", $time) } sem acúmulo
        [add]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } por pelo menos { NATURALFIXED($time, 3) } { MANY("segundo", $time) } com acúmulo
        [set]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } por pelo menos { NATURALFIXED($time, 3) } { MANY("segundo", $time) } sem acúmulo
       *[remove]
            { $chance ->
                [1] Remove
               *[other] remover
            } { NATURALFIXED($time, 3) } { MANY("segundo", $time) } de { $key }
    } { $delay ->
        [0] imediatamente
       *[other] após um atraso de { NATURALFIXED($delay, 3) } segundos
    }
entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } permanente
        [add]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } permanente
        [set]
            { $chance ->
                [1] Causa
               *[other] causar
            } { $key } permanente
       *[remove]
            { $chance ->
                [1] Remove
               *[other] remover
            } { $key }
    } { $delay ->
        [0] imediatamente
       *[other] após um atraso de { NATURALFIXED($delay, 3) } segundos
    }
entity-effect-guidebook-knockdown =
    { $type ->
        [update]
            { $chance ->
                [1] Causas
               *[other] causar
            } { LOC($key) } por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) } sem acúmulo
        [add]
            { $chance ->
                [1] Causas
               *[other] causar
            } knockdown por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) } com acúmulo
       *[set]
            { $chance ->
                [1] Causas
               *[other] causar
            } knockdown por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) } sem acúmulo
        [remove]
            { $chance ->
                [1] Remove
               *[other] Remover
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } de knockdown
    }
entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Define
       *[other] Defina a temperatura para o efeito.
    } defina a temperatura da solução exatamente para { NATURALFIXED($temperature, 2) }k
entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Ajusta a temperatura da solução.
               *[-1] Remove
            }
       *[other]
            { $deltasign ->
                [1] Adicionar
               *[-1] Remover
            }
    } aqueça a solução até que ela atinja { $deltasign ->
        [1] no máximo { NATURALFIXED($maxtemp, 2) }k
       *[-1] pelo menos { NATURALFIXED($mintemp, 2) }k
    }
entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Ajusta a temperatura da solução.
               *[-1] Remove
            }
       *[other]
            { $deltasign ->
                [1] Adicionar
               *[-1] Remover
            }
    } { NATURALFIXED($amount, 2) }u de { $reagent } { $deltasign ->
        [1] à
       *[-1] da
    } solução
entity-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Ajusta a temperatura da solução.
               *[-1] Remove
            }
       *[other]
            { $deltasign ->
                [1] Adicionar
               *[-1] Remover
            }
    } { NATURALFIXED($amount, 2) } de reagentes no grupo { $group } { $deltasign ->
        [1] à
       *[-1] da
    } solução
entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Ajusta a temperatura da solução.
               *[-1] Remove
            }
       *[other]
            { $deltasign ->
                [1] Adicionar
               *[-1] Remover
            }
    } { POWERJOULES($amount) } de calor { $deltasign ->
        [1] ao
       *[-1] do
    } corpo em que está
entity-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Causas
       *[other] causar
    } a doença { $disease }
entity-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Causas
       *[other] causar
    } as doenças { $diseases }
entity-effect-guidebook-jittering =
    { $chance ->
        [1] Causas
       *[other] causar
    } tremor
entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Limpa
       *[other] limpar
    } o sangue de outras substâncias
entity-effect-guidebook-cure-disease =
    { $chance ->
        [1] Cura
       *[other] curar
    } doenças
entity-effect-guidebook-eye-damage =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Causa
               *[-1] Cura
            }
       *[other]
            { $deltasign ->
                [1] causar
               *[-1] curar
            }
    } dano ocular
entity-effect-guidebook-vomit =
    { $chance ->
        [1] Causas
       *[other] causar
    } vomitando
entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Cria
       *[other] criar
    } { $moles } { $moles ->
        [1] molécula
       *[other] moléculas
    } de { $gas }
entity-effect-guidebook-drunk =
    { $chance ->
        [1] Causas
       *[other] causar
    } Bebedice
entity-effect-guidebook-electrocute =
    { $chance ->
        [1]
            { $stuns ->
                [true] Eletricita
               *[false] Choques
            }
       *[other]
            { $stuns ->
                [true] Eletricidade estática.
               *[false] choque
            }
    } o metabolizador para { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-emote =
    { $chance ->
        [1] Forçará
       *[other] forçar
    } o metabolizador para [bold][color=white]{ $emote }[/color][/bold]
entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Apaga
       *[other] Apagar
    } atingir
entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Aumenta
       *[other] aumentar
    } inflamabilidade
entity-effect-guidebook-ignite =
    { $chance ->
        [1] Ignita
       *[other] Ignite (other) - Ação: Causa um pequeno incêndio no alvo.
    } o metabolizador
entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Faz
       *[other] Faça
    } tornar o metabolizador consciente
entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Polimorfismos
       *[other] polimorfismo
    } o metabolizador em um { $entityname }
entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Induz
               *[-1] Reduz
            }
       *[other]
            { $deltasign ->
                [1] induzir
               *[-1] reduzir
            }
    } sangramento
entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Aumenta
               *[-1] Diminui
            }
       *[other]
            { $deltasign ->
                [1] aumenta
               *[-1] diminui
            }
    } nível de sangue
entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Paralisa
       *[other] paralisar
    } o metabolizador por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Modifica
       *[other] modificar
    } aumenta a velocidade de movimento por { NATURALFIXED($sprintspeed, 3) }x por pelo menos { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Impedem temporariamente
       *[other] temporariamente conter
    } Desligar narcolepsia
entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Lava
       *[other] lavar
    } Lave o rosto com uma touca de sabão para remover a torta de creme.
entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Cura
       *[other] curar
    } uma infecção zumbi em andamento
entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Dá
       *[other] Dê
    } um indivíduo a infecção zumbi
entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Cura
       *[other] curar
    } uma infecção zumbi em andamento, e fornece imunidade a futuras infecções
entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Regenera
       *[other] Regenerar
    } { NATURALFIXED($time, 3) } { MANY("second", $time) } de apodrecimento
entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Causas
       *[other] causar
    }uma reação de fumaça ou espuma para { NATURALFIXED($duration, 3) } { MANY("second", $duration) }
entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Causas
       *[other] causar
    } { $reagent } a ser adicionado ao seu contêiner de solução interno
entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Ajuda
       *[other] Ajuda
    }desbloqueie um artefato alienígena.
entity-effect-guidebook-artifact-durability-restore = Restaura { $restored } de durabilidade em nós de artefatos alienígenas ativos.
entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Ajusta
       *[other] ajustar
    } { $attribute } em { $positive ->
        [false] [color=red]{ $amount }[/color]
       *[true] [color=green]{ $amount }[/color]
    }
entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Há séculos
       *[other] idade de volta
    }a planta, dependendo da idade da planta e do tempo para crescer
entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Restaura
       *[other] Restaurar
    }viabilidade a uma planta tornada inviável por uma mutação
entity-effect-guidebook-plant-remove-kudzu =
    { $chance ->
        [1] Remove
       *[other] Remover
    }crescimento da planta de kudzu
entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Aumenta
       *[other] aumentar
    }aumenta a duração de vida e/ou a saúde base da planta com 10% de chance para cada
entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Aumenta
       *[other] aumentar
    }aumenta a potência da planta por { $increase } até um máximo de { $limit }. Causa a perda das sementes da planta uma vez que a potência atinge { $seedlesstreshold }. Tentar adicionar potência acima de { $limit } pode causar uma diminuição na produção com 10% de chance
entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Restaura o
       *[other] Restaurar o
    }sementes da planta
entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Remove as sementes da planta.
       *[other] Remova as sementes da planta.
    } sementes da planta
entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Mutante
       *[other] mutar
    }uma planta para produzir { $name }
entity-effect-guidebook-add-reagent-to-bloodstream =
    { $chance ->
        [1] Injeta
       *[other] injetar
    } { $quantity } de { $reagent } diretamente na corrente sanguínea
