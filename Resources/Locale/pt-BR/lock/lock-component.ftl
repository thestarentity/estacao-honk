lock-comp-on-examined-is-locked = O { $entityName } parece estar [color=darkred]trancado[/color].
lock-comp-on-examined-is-unlocked = O { $entityName } parece estar [color=darkgreen]desbloqueado[/color].
lock-comp-do-lock-success = Você trava o { $entityName }.
lock-comp-do-unlock-success = Você desbloqueia o { $entityName }.
lock-comp-has-user-access-fail = Acesso negado.
lock-comp-generic-fail = { CAPITALIZE(SUBJECT($target)) } está { GENDER($target) ->
    [female] trancada
   *[other] trancado
   }.

## ToggleLockVerb

toggle-lock-verb-unlock = Desbloquear
toggle-lock-verb-lock = Travar
