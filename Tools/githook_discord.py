#!/usr/bin/env python3
"""Avisa o canal #githook do Discord quando alguem envia commits para o GitHub.

Roda dentro do GitHub Actions (workflow githook-discord.yml). Le o evento de push
que o GitHub deixa em GITHUB_EVENT_PATH, monta um aviso curto em portugues e posta
no webhook do Discord daquele servidor (secret DISCORD_GITHOOK_WEBHOOK).

Nao depende de nada da VPS nem do bot ligado: roda no proprio GitHub. Sem o secret,
sai em silencio (o fork publico de quem faz PR nao tem o segredo, e tudo bem).

So o PUSH vira aviso. Ja tentamos avisar falha de workflow, mas os workflows de
build do upstream falham SEMPRE neste fork (a API de admin fica no .gitignore, entao
o repo publico nao compila), e isso virava spam a cada push. Um aviso por push.

Formato proposital: portugues, sem emoji no texto de estrutura e sem travessao, como
o resto do projeto. Cada commit vira uma linha "hash mensagem (autor)".
"""
import json
import os
import sys
import urllib.request

MAX_COMMITS = 10           # o resto vira "e mais N commits"
MAX_MSG = 100              # corta a primeira linha da mensagem do commit


def _curta(texto: str, limite: int) -> str:
    texto = (texto or "").splitlines()[0].strip() if texto else ""
    return texto if len(texto) <= limite else texto[:limite - 1] + "…"


def montar(evento: dict) -> dict | None:
    """Evento de push do GitHub -> corpo JSON para o webhook do Discord, ou None se
    nao houver o que anunciar (ex.: push que so apaga uma branch)."""
    commits = evento.get("commits") or []
    if not commits:
        return None

    repo = (evento.get("repository") or {}).get("name") or "repositorio"
    ref = evento.get("ref") or ""
    branch = ref.rsplit("/", 1)[-1] if ref else "?"
    quem = ((evento.get("pusher") or {}).get("name")
            or (evento.get("sender") or {}).get("login") or "alguem")
    compare = evento.get("compare") or (evento.get("repository") or {}).get("html_url") or ""

    linhas = []
    for c in commits[:MAX_COMMITS]:
        hash_curto = (c.get("id") or "")[:7]
        autor = (c.get("author") or {}).get("name") or "?"
        url = c.get("url") or ""
        msg = _curta(c.get("message"), MAX_MSG)
        alvo = f"[`{hash_curto}`]({url})" if url else f"`{hash_curto}`"
        linhas.append(f"{alvo} {msg} ({autor})")
    if len(commits) > MAX_COMMITS:
        linhas.append(f"e mais {len(commits) - MAX_COMMITS} commit(s)")

    n = len(commits)
    titulo = f"{n} commit{'s' if n != 1 else ''} em {repo} ({branch})"
    return {
        "username": "GitHub",
        "embeds": [{
            "title": titulo,
            "url": compare,
            "description": "\n".join(linhas),
            "footer": {"text": f"enviado por {quem}"},
            "color": 0x2B3137,
        }],
    }


def main() -> int:
    caminho = os.environ.get("GITHUB_EVENT_PATH")
    if not caminho or not os.path.exists(caminho):
        print("Sem GITHUB_EVENT_PATH: nada a fazer.")
        return 0
    with open(caminho, encoding="utf-8") as f:
        evento = json.load(f)

    corpo = montar(evento)
    if corpo is None:
        print("Push sem commits para anunciar; nada enviado.")
        return 0

    webhook = os.environ.get("DISCORD_GITHOOK_WEBHOOK")
    if not webhook:
        # Sem segredo (fork de terceiros, ou ainda nao configurado): mostra e sai.
        print("DISCORD_GITHOOK_WEBHOOK nao configurado; nada enviado. Previa:")
        print(json.dumps(corpo, ensure_ascii=False, indent=2))
        return 0

    dados = json.dumps(corpo).encode("utf-8")
    # O Discord recusa (403) requisicao sem User-Agent. Precisa de um.
    req = urllib.request.Request(webhook, data=dados, headers={
        "Content-Type": "application/json",
        "User-Agent": "HonkGithook (https://honkenvironment.online, 1.0)",
    })
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            print(f"Discord respondeu {r.status}.")
    except Exception as exc:                      # nunca derruba o CI por isso
        print(f"Falha ao avisar o Discord: {exc}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
