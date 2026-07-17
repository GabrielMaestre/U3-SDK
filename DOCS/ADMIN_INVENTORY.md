# Inventário administrativo

## Uso

- Pressione `F3` durante gameplay para abrir ou fechar.
- Digite nome exibido, nome do asset, ID legado ou GUID na busca.
- Use as abas para filtrar pelo tipo exato (`EItemType`); setas laterais percorrem tipos adicionais.
- Use roda do mouse para trocar páginas: cima volta, baixo avança.
- Clique no item para receber uma unidade.
- `Escape` e botão `Close` fecham painel.
- `Shift+F3` continua reservado para freecam. Seleção do terceiro assento por `F3` continua disponível para jogadores sem acesso ao painel.

## Catálogo e mods

Painel consulta `Assets.find(List<ItemAsset>)` na primeira abertura. Mesma fonte registra itens oficiais, Workshop e mods carregados pelo jogo. Evento `Assets.onAssetsRefreshed` invalida cache após reload ou mudança de mods; abertura seguinte reconstrói catálogo e abas. Nenhuma lista manual de IDs ou tipos precisa ser mantida.

Itens cosméticos Steam (`ItemAsset.isPro`) são omitidos; roupas e equipamentos jogáveis comuns continuam disponíveis. Busca permanece ativa dentro da aba selecionada.

Somente 14 linhas, 14 ícones e 5 abas existem na interface. Paginação reutiliza elementos para qualquer quantidade de itens. Ícone é solicitado apenas para linhas visíveis no tamanho nativo cacheável do `ItemTool`, mas exibido em `24x24`; páginas revisitadas e ícones já usados pelo inventário normal reaproveitam cache quando asset permite. Catálogo inteiro não instancia prefabs nem cria elementos por asset.

Primeiro ícone ainda depende da fila nativa: modelo é instanciado em um frame e capturado no seguinte. Processar vários por frame foi evitado porque pode causar hitch e capturar modelos antes de ficarem prontos.

Assets sem ID legado aparecem, mas ficam desabilitados porque inventário e `ItemTool` ainda usam `ushort` para identificar itens.

## Permissão e segurança

- Singleplayer/host: acesso local.
- Servidor vanilla: `SteamPlayer.isAdmin`/owner, seguindo permissão nativa de comandos.
- RocketMod: conceda permissão do comando `give` ao grupo/usuário. Painel usa mesmo hook `ChatManager.onCheckPermissions`, portanto não depende diretamente de assemblies RocketMod.
- Cliente nunca entrega item sozinho. Servidor valida conexão, permissão e GUID a cada clique, resolve `ItemAsset` no catálogo servidor e chama `ItemTool.tryForceGiveItem`.
- Pedidos de abertura são limitados a `2/s`; entrega, a `10/s`.

## Teste manual

1. Entre em singleplayer, pressione `F3`, pesquise por nome e ID, troque páginas e receba item.
2. Troque abas, pesquise dentro de uma aba e confirme ícones ao paginar.
3. Instale mod com item, reinicie/carregue assets e confirme item, tipo e ícone na busca por nome/GUID.
4. Em servidor vanilla, confirme que admin abre e jogador comum mantém `F3` de assento sem abrir painel.
5. Em RocketMod, conceda/remova `give`, reconecte jogador e confirme acesso/negação.
6. Teste inventário cheio, morte com painel aberto, `Escape`, `Shift+F3` e veículo.

Validação estática: `Assembly-CSharp.csproj` compila com `0` erros; 14 warnings preexistentes permanecem.

## Correção de inicialização — 2026-07-17

Primeiro build não continha metadata gerada dos cinco métodos de rede. `ServerStaticMethod.Get` retornava `null`; pré-check no construtor interrompia `PlayerUI.InitializePlayer` e causava spam posterior em `PlayerLifeUI.updateHotbar`.

Arquivo `PlayerAdminInventoryUI_NetMethods.cs` registra leitura/escrita no `NetReflection`. Invocações também usam null-check para falhar fechadas sem interromper entrada no mundo se metadata voltar a faltar. Teste `NetReflectionTests.AdminInventoryMethodsAreRegistered` cobre os cinco registros.
