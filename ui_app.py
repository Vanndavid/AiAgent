"""Tiny local web UI for the minimal Research & Save agent.

No external framework required; uses Python stdlib http.server.
"""

from __future__ import annotations

import html
import urllib.parse
from http.server import BaseHTTPRequestHandler, HTTPServer

from research_save_agent import run_agent_detailed


HTML_TEMPLATE = """<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>Minimal Research & Save Agent</title>
    <style>
      body {{ font-family: Arial, sans-serif; max-width: 900px; margin: 24px auto; padding: 0 16px; }}
      h1 {{ margin-bottom: 4px; }}
      p {{ color: #444; }}
      textarea {{ width: 100%; min-height: 100px; padding: 8px; }}
      button {{ margin-top: 12px; padding: 8px 14px; cursor: pointer; }}
      .card {{ border: 1px solid #ddd; border-radius: 10px; padding: 16px; margin-top: 16px; }}
      pre {{ white-space: pre-wrap; background: #f8f8f8; padding: 12px; border-radius: 8px; }}
    </style>
  </head>
  <body>
    <h1>Research & Save Agent UI</h1>
    <p>Enter a goal, then run the agent loop and inspect each Reason→Act→Observe step.</p>

    <form method="post" action="/run">
      <label for="goal"><strong>Goal</strong></label>
      <textarea id="goal" name="goal" required>{goal}</textarea>
      <br />
      <button type="submit">Run Agent</button>
    </form>

    {result_block}
  </body>
</html>
"""


class AgentUIHandler(BaseHTTPRequestHandler):
    def _respond_html(self, content: str, status: int = 200) -> None:
        encoded = content.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def do_GET(self) -> None:  # noqa: N802
        page = HTML_TEMPLATE.format(
            goal="Research the ReAct loop and save concise notes to a file.",
            result_block="",
        )
        self._respond_html(page)

    def do_POST(self) -> None:  # noqa: N802
        if self.path != "/run":
            self._respond_html("<h1>Not Found</h1>", status=404)
            return

        length = int(self.headers.get("Content-Length", "0"))
        payload = self.rfile.read(length).decode("utf-8")
        form = urllib.parse.parse_qs(payload)
        goal = form.get("goal", [""])[0].strip()

        if not goal:
            self._respond_html("<h1>Bad Request</h1><p>Goal is required.</p>", status=400)
            return

        result = run_agent_detailed(goal)
        final_answer = html.escape(result["final_answer"])
        scratchpad = html.escape("\n".join(result["scratchpad"]))

        result_block = (
            '<div class="card">'
            "<h2>Final Answer</h2>"
            f"<pre>{final_answer}</pre>"
            "<h2>Agent Trace</h2>"
            f"<pre>{scratchpad}</pre>"
            "</div>"
        )

        page = HTML_TEMPLATE.format(goal=html.escape(goal), result_block=result_block)
        self._respond_html(page)


def main() -> None:
    host, port = "0.0.0.0", 8000
    server = HTTPServer((host, port), AgentUIHandler)
    print(f"Serving Agent UI at http://{host}:{port} (open http://localhost:{port})")
    server.serve_forever()


if __name__ == "__main__":
    main()
