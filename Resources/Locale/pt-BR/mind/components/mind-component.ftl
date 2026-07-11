# MindContainerComponent localization

comp-mind-ghosting-prevented = Você não pode se manifestar como fantasma no momento.

## Messages displayed when a body is examined and in a certain state

comp-mind-examined-catatonic = { CAPITALIZE(SUBJECT($ent)) } está totalmente { GENDER($ent) ->
    [female] catatônica
   *[other] catatônico
   }. Os estresses da vida no espaço profundo devem ter sido demais para { OBJECT($ent) }. Qualquer recuperação é impossível.
comp-mind-examined-dead = { CAPITALIZE(SUBJECT($ent)) } está { GENDER($ent) ->
    [female] morta
   *[other] morto
   }.
comp-mind-examined-ssd = { CAPITALIZE(SUBJECT($ent)) } { CONJUGATE-HAVE($ent) } olha vazio, com uma expressão absenteista e parece completamente indiferente a qualquer coisa. { CAPITALIZE(SUBJECT($ent)) } pode sair desse estado em breve.
comp-mind-examined-dead-and-ssd = { CAPITALIZE(POSS-ADJ($ent)) } alma está em repouso e pode retornar em breve.
comp-mind-examined-dead-and-irrecoverable = { CAPITALIZE(POSS-ADJ($ent)) } alma partiu e seguiu em frente. Qualquer recuperação é impossível.
