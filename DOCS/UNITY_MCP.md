# Unity MCP local

Fallback local para controlar Unity Editor quando bridge oficial de `com.unity.ai.assistant` falhar no Windows.

## Arquitetura

- `CustomUnityMcpBridge.cs`: bridge editor-only em TCP loopback com porta aleatória e token novo por sessão.
- `unity_mcp_server.py`: servidor MCP `stdio` sem dependências externas.
- Estado efêmero: `UserSettings/CustomUnityMcpBridge.json`, ignorado pelo Git.
- Nenhum código entra no build do jogo porque bridge fica em `Assets/Editor`.

## Ferramentas

- status do Editor;
- últimas linhas do Console/`Editor.log`;
- lista de cenas e hierarchy ativa;
- pesquisa, informação e seleção de assets/objetos;
- execução de menu do Editor;
- play, stop, pause e resume;
- refresh de assets e save de cenas.

Leitura e edição normal de scripts continuam pelas ferramentas nativas do Codex. Duplicar isso no MCP não agrega capacidade.

## Uso

1. Abra projeto na Unity e aguarde compilação.
2. Confirme mensagem `Custom Unity MCP bridge listening` no Console. Se necessário: `Tools > Custom Unity MCP > Start`.
3. Reinicie Codex depois de configurar servidor `unity_local`.
4. Teste pedindo status ou mensagens do Console da Unity.

## Segurança e limites

- Escuta somente `127.0.0.1`.
- Cada domínio do Editor gera porta aleatória e token de sessão.
- `unity_execute_menu_item`, `unity_refresh_assets`, `unity_save_scenes` e play mode alteram estado; usar somente quando solicitado.
- Primeira versão não cria/deleta GameObjects nem executa C# arbitrário. Adicionar somente quando caso real exigir.
