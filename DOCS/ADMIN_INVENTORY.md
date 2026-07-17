# Inventário administrativo

## Uso

- Pressione `F3` durante gameplay para abrir ou fechar.
- Digite nome exibido, nome do asset, ID legado ou GUID na busca.
- Clique no item para receber uma unidade.
- `Escape` e botão `Close` fecham painel.
- `Shift+F3` continua reservado para freecam. Seleção do terceiro assento por `F3` continua disponível para jogadores sem acesso ao painel.

## Catálogo e mods

Painel consulta `Assets.find(List<ItemAsset>)` na primeira abertura. Mesma fonte registra itens oficiais, Workshop e mods carregados pelo jogo. Evento `Assets.onAssetsRefreshed` invalida cache após reload ou mudança de mods; abertura seguinte reconstrói catálogo. Nenhuma lista manual de IDs precisa ser mantida.

Somente 16 botões existem na interface. Paginação reutiliza esses elementos para qualquer quantidade de itens; busca percorre índice textual criado uma vez por abertura. Ícones e prefabs não são carregados, evitando custo de textura, mesh e milhares de elementos UI.

Assets sem ID legado aparecem, mas ficam desabilitados porque inventário e `ItemTool` ainda usam `ushort` para identificar itens.

## Permissão e segurança

- Singleplayer/host: acesso local.
- Servidor vanilla: `SteamPlayer.isAdmin`/owner, seguindo permissão nativa de comandos.
- RocketMod: conceda permissão do comando `give` ao grupo/usuário. Painel usa mesmo hook `ChatManager.onCheckPermissions`, portanto não depende diretamente de assemblies RocketMod.
- Cliente nunca entrega item sozinho. Servidor valida conexão, permissão e GUID a cada clique, resolve `ItemAsset` no catálogo servidor e chama `ItemTool.tryForceGiveItem`.
- Pedidos de abertura são limitados a `2/s`; entrega, a `10/s`.

## Teste manual

1. Entre em singleplayer, pressione `F3`, pesquise por nome e ID, troque páginas e receba item.
2. Instale mod com item, reinicie/carregue assets e confirme item na busca por nome/GUID.
3. Em servidor vanilla, confirme que admin abre e jogador comum mantém `F3` de assento sem abrir painel.
4. Em RocketMod, conceda/remova `give`, reconecte jogador e confirme acesso/negação.
5. Teste inventário cheio, morte com painel aberto, `Escape`, `Shift+F3` e veículo.

Validação estática: `Assembly-CSharp.csproj` compila com `0` erros; 14 warnings preexistentes permanecem.

## Correção de inicialização — 2026-07-17

Primeiro build não continha metadata gerada dos cinco métodos de rede. `ServerStaticMethod.Get` retornava `null`; pré-check no construtor interrompia `PlayerUI.InitializePlayer` e causava spam posterior em `PlayerLifeUI.updateHotbar`.

Arquivo `PlayerAdminInventoryUI_NetMethods.cs` registra leitura/escrita no `NetReflection`. Invocações também usam null-check para falhar fechadas sem interromper entrada no mundo se metadata voltar a faltar. Teste `NetReflectionTests.AdminInventoryMethodsAreRegistered` cobre os cinco registros.
