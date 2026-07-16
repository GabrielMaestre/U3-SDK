#!/usr/bin/env python3
"""Dependency-free MCP stdio server for this Unity project."""

import argparse
import json
import os
import socket
import sys
from pathlib import Path


TOOLS = [
    {"name": "unity_status", "description": "Read Unity Editor and active scene status.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_read_console", "description": "Read recent Unity Editor log lines.", "inputSchema": {"type": "object", "properties": {"lines": {"type": "integer", "minimum": 1, "maximum": 1000}, "contains": {"type": "string"}}}},
    {"name": "unity_list_scenes", "description": "List scene assets in project.", "inputSchema": {"type": "object", "properties": {"max_results": {"type": "integer", "minimum": 1, "maximum": 500}}}},
    {"name": "unity_scene_hierarchy", "description": "Read active scene GameObject hierarchy.", "inputSchema": {"type": "object", "properties": {"max_depth": {"type": "integer", "minimum": 0, "maximum": 12}}}},
    {"name": "unity_find_assets", "description": "Search AssetDatabase using Unity query syntax.", "inputSchema": {"type": "object", "properties": {"query": {"type": "string"}, "max_results": {"type": "integer", "minimum": 1, "maximum": 500}}, "required": ["query"]}},
    {"name": "unity_asset_info", "description": "Read type and dependencies for asset path.", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_select_object", "description": "Select and ping asset path or scene GameObject name.", "inputSchema": {"type": "object", "properties": {"path": {"type": "string"}}, "required": ["path"]}},
    {"name": "unity_execute_menu_item", "description": "Execute exact Unity Editor menu item path.", "inputSchema": {"type": "object", "properties": {"menu_item": {"type": "string"}}, "required": ["menu_item"]}},
    {"name": "unity_set_play_mode", "description": "Set Unity play mode state.", "inputSchema": {"type": "object", "properties": {"mode": {"type": "string", "enum": ["play", "stop", "pause", "resume"]}}, "required": ["mode"]}},
    {"name": "unity_refresh_assets", "description": "Refresh Unity AssetDatabase.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "unity_save_scenes", "description": "Save all open Unity scenes.", "inputSchema": {"type": "object", "properties": {}}},
]


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-path", required=True)
    return parser.parse_args()


def read_bridge_config(project_path):
    path = Path(project_path) / "UserSettings" / "CustomUnityMcpBridge.json"
    if not path.exists():
        raise RuntimeError("Custom Unity MCP bridge is not running. Open Unity or use Tools > Custom Unity MCP > Start.")
    return json.loads(path.read_text(encoding="utf-8"))


def call_unity(project_path, action, **kwargs):
    config = read_bridge_config(project_path)
    payload = {"token": config["token"], "action": action, **kwargs}
    with socket.create_connection(("127.0.0.1", int(config["port"])), timeout=12) as client:
        client.sendall((json.dumps(payload, separators=(",", ":")) + "\n").encode("utf-8"))
        with client.makefile("r", encoding="utf-8") as reader:
            line = reader.readline()
    if not line:
        raise RuntimeError("Unity bridge closed connection without response.")
    response = json.loads(line)
    if not response.get("ok"):
        raise RuntimeError(response.get("error") or "Unity bridge returned unknown error.")
    return response.get("result", "")


def tail_editor_log(line_count, contains):
    path = Path(os.environ.get("LOCALAPPDATA", "")) / "Unity" / "Editor" / "Editor.log"
    if not path.exists():
        raise RuntimeError(f"Unity Editor log not found: {path}")
    wanted = max(1, min(int(line_count), 1000))
    data = bytearray()
    with path.open("rb") as stream:
        stream.seek(0, os.SEEK_END)
        position = stream.tell()
        while position > 0 and data.count(b"\n") <= max(wanted * 4, 200) and len(data) < 1_048_576:
            size = min(8192, position)
            position -= size
            stream.seek(position)
            data[:0] = stream.read(size)
    lines = data.decode("utf-8", errors="replace").splitlines()
    if contains:
        needle = contains.casefold()
        lines = [line for line in lines if needle in line.casefold()]
    return "\n".join(lines[-wanted:]) or "No matching console lines."


def call_tool(project_path, name, arguments):
    arguments = arguments or {}
    if name == "unity_read_console":
        return tail_editor_log(arguments.get("lines", 100), arguments.get("contains", ""))
    mapping = {
        "unity_status": ("status", {}),
        "unity_list_scenes": ("list_scenes", {"maxResults": arguments.get("max_results", 100)}),
        "unity_scene_hierarchy": ("scene_hierarchy", {"maxDepth": arguments.get("max_depth", 4)}),
        "unity_find_assets": ("find_assets", {"query": arguments.get("query", ""), "maxResults": arguments.get("max_results", 100)}),
        "unity_asset_info": ("asset_info", {"path": arguments.get("path", "")}),
        "unity_select_object": ("select_object", {"path": arguments.get("path", "")}),
        "unity_execute_menu_item": ("execute_menu_item", {"menuItem": arguments.get("menu_item", "")}),
        "unity_set_play_mode": ("set_play_mode", {"mode": arguments.get("mode", "")}),
        "unity_refresh_assets": ("refresh_assets", {}),
        "unity_save_scenes": ("save_scenes", {}),
    }
    if name not in mapping:
        raise RuntimeError(f"Unknown tool: {name}")
    action, payload = mapping[name]
    return call_unity(project_path, action, **payload)


def send(message):
    sys.stdout.write(json.dumps(message, separators=(",", ":")) + "\n")
    sys.stdout.flush()


def result(request_id, value):
    send({"jsonrpc": "2.0", "id": request_id, "result": value})


def main():
    args = parse_args()
    for raw_line in sys.stdin:
        try:
            raw_line = raw_line.lstrip("\ufeffï»¿").strip()
            if not raw_line:
                continue
            request = json.loads(raw_line)
            request_id = request.get("id")
            method = request.get("method")
            if method == "initialize":
                result(request_id, {"protocolVersion": request.get("params", {}).get("protocolVersion", "2025-03-26"), "capabilities": {"tools": {"listChanged": False}}, "serverInfo": {"name": "u3-unity-local", "version": "1.0.0"}})
            elif method == "tools/list":
                result(request_id, {"tools": TOOLS})
            elif method == "tools/call":
                params = request.get("params", {})
                try:
                    text = call_tool(args.project_path, params.get("name", ""), params.get("arguments", {}))
                    result(request_id, {"content": [{"type": "text", "text": text}], "isError": False})
                except Exception as exception:
                    result(request_id, {"content": [{"type": "text", "text": str(exception)}], "isError": True})
            elif method == "ping":
                result(request_id, {})
            elif request_id is not None:
                send({"jsonrpc": "2.0", "id": request_id, "error": {"code": -32601, "message": f"Method not found: {method}"}})
        except Exception as exception:
            send({"jsonrpc": "2.0", "id": None, "error": {"code": -32603, "message": str(exception)}})


if __name__ == "__main__":
    main()
