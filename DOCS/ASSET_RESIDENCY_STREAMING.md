# Asset Residency Streaming

Status: proposta para branch `asset-residency-streaming`. Data: 2026-07-20.

## Objetivo

Manter em memória somente recursos Unity necessários para sessão ativa:

- core necessário para menu, jogador e protocolos;
- mapa selecionado;
- Workshop anunciado pelo servidor;
- bundles locais do servidor que o servidor anuncia;
- dependências transitivas de conteúdo acima;
- entidades já vivas e conteúdo que pode spawnar durante sessão.

Metadados leves permanecem residentes: GUID, ID, tipo, nome, origem, dependências e caminho no bundle. Prefabs, meshes, materiais, texturas, áudio e efeitos permanecem carregados sob demanda.

## Estado atual

- `Assets.LoadAllAssets` pesquisa core, UGC cliente habilitado, Sandbox e bundles de todos mapas na inicialização.
- Cada `MasterBundleConfig` abre seu AssetBundle com `LoadFromFileAsync`.
- `ApplyServerAssetMapping` já restringe buscas de assets ao core, mapa e IDs Workshop solicitados pelo servidor.
- Mapping não reduz residência: definições de assets e referências Unity de origens não selecionadas ainda podem existir.
- `Bundle.unload()` e `MasterBundleConfig.unload()` usam `AssetBundle.Unload(false)`. Objetos Unity vivos continuam válidos e ocupam memória até suas referências serem removidas e `Resources.UnloadUnusedAssets()` executar.

## Regra de segurança

Nunca decidir por "item equipado". Conteúdo pode ser solicitado por loot, spawn, crafting, NPC, veículo, quest, evento, comando administrativo, plugin ou mod.

O manifesto de sessão inclui todas origens e dependências que o servidor/mapa pode usar. Pedido fora do manifesto deve carregar sob demanda somente se origem foi autorizada pela sessão; caso contrário, falha com diagnóstico claro.

Não chamar `AssetBundle.Unload(true)` em massa. Ele pode invalidar material, mesh ou prefab referenciado por mundo/UI e causar conteúdo invisível ou exceção.

## Ciclo de sessão

1. Resolver mapa, IDs Workshop do servidor e bundles locais anunciados.
2. Criar manifesto de origens permitidas e dependências de bundles.
3. Carregar metadados e bundles do manifesto. Referências visuais usam o lazy-load existente.
4. Durante jogo, manter referência enquanto prefab/material/mesh está em uso.
5. Ao sair do mapa/servidor, destruir instâncias do mapa, limpar caches visuais da sessão, descarregar bundles sem referências e executar `Resources.UnloadUnusedAssets()`.
6. Voltar ao mapping padrão de menu sem depender de assets destruídos da sessão anterior.

## Implementação por etapas

### PR 1 — Inventário e contrato

- Registrar origens, master bundles, dependências e quantidade/tamanho carregado por sessão.
- Separar metadata residente de referência visual carregada.
- Adicionar testes de Russia -> menu -> Germany e disconnect/reconnect.
- Nenhum unload agressivo nesta etapa.

### PR 2 — Carregamento seletivo

- Não pesquisar todos os mapas/UGC cliente no boot.
- Carregar origem de mapa e Workshop somente após seleção/handshake do servidor.
- Preservar singleplayer, editor, Sandbox e fallback para conteúdo dinâmico.

### PR 3 — Liberação visual

- Limpar campos lazy já carregados de assets fora da sessão.
- Liberar somente bundles cujas dependências e instâncias chegaram a zero.
- Recarregar sob demanda por caminho/GUID preservado no metadata.

### PR 4 — Servidor dedicado

- Usar manifestos de `WorkshopDownloadConfig`, mapa e `Servers/<ID>/Bundles`.
- Não carregar prefabs/texturas exclusivamente client-side no dedicated server.
- Manter dados necessários para spawn, validação, save e rede.

## Critérios de aceite

- Mesmo mapa, Workshop e mods: login, spawn, loot, craft, veículo, NPC, admin give, plugin e reconnect funcionam.
- Conteúdo não selecionado não aparece em `currentAssetMapping` nem mantém recursos visuais da sessão.
- Memory Profiler reduz `Mesh`, `Texture2D` ou `Shader` após troca de mapa/desconexão.
- Sem material rosa, objeto invisível, referência nula ou quebra de hash/protocolo.
- Caso de fallback sob demanda registra origem, asset e motivo; não falha silenciosamente.

## Medição obrigatória

Capturar Memory Profiler em quatro pontos com mesmo cliente:

1. menu após boot;
2. Russia com Workshop/mods selecionados;
3. menu após disconnect;
4. Germany com conjunto diferente.

Comparar quantidade e `Native Size` de Mesh, Texture2D, Shader, RenderTexture e MeshRenderer. Não aceitar maior tempo de entrada, stutter ou regressão visual sem ganho mensurável de memória.
