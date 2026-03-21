#!/usr/bin/env python3
import json
import sys


def send(payload):
    sys.stdout.write(payload + "\n")
    sys.stdout.flush()


def handle(message):
    method = message.get("method")
    identifier = message.get("id")

    if method == "initialize":
        send(json.dumps({
            "jsonrpc": "2.0",
            "id": identifier,
            "result": {
                "isAuthenticated": False,
                "authMethods": [
                    {
                        "id": "token",
                        "name": "Token"
                    }
                ],
                "agentCapabilities": {
                    "sessionPrompt": True
                }
            }
        }))
        return

    if method == "authenticate":
        send(json.dumps({
            "jsonrpc": "2.0",
            "id": identifier,
            "result": {
                "accepted": True
            }
        }))
        return

    if method == "session/new":
        send(json.dumps({
            "jsonrpc": "2.0",
            "id": identifier,
            "result": {
                "sessionId": "adapter-session"
            }
        }))
        return

    if method == "session/prompt":
        params = message.get("params", {})
        session_id = params.get("sessionId", "adapter-session")
        prompt_blocks = params.get("prompt", [])
        prompt_text = ""
        if prompt_blocks:
            prompt_text = prompt_blocks[0].get("text", "")
        response_text = "adapter ok" if "adapter ok" in prompt_text else "mock kimi response"
        send(json.dumps({
            "jsonrpc": "2.0",
            "method": "session/update",
            "params": {
                "sessionId": session_id,
                "update": {
                    "kind": "assistant",
                    "text": response_text
                }
            }
        }))
        send(json.dumps({
            "jsonrpc": "2.0",
            "id": identifier,
            "result": {
                "stopReason": "end_turn",
                "content": [
                    {
                        "type": "text",
                        "text": response_text
                    }
                ]
            }
        }))
        return

    send(json.dumps({
        "jsonrpc": "2.0",
        "id": identifier,
        "error": {
            "code": -32601,
            "message": f"Unsupported method: {method}"
        }
    }))


def main():
    send("//ready")
    for raw_line in sys.stdin:
        raw_line = raw_line.strip()
        if not raw_line:
            continue
        handle(json.loads(raw_line))


if __name__ == "__main__":
    main()
