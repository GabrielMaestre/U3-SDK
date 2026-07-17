# Unturned: Reforger

Fork experimental do SDK open-source de [Unturned](https://smartlydressedgames.com/unturned/), focado em **otimizações, correções, fixes e melhorias de qualidade de vida (QoL)**.

Projeto não oficial e em desenvolvimento. Objetivo: modernizar jogo sem descaracterizar visual ou quebrar gameplay, mapas, mods e servidores.

## Objetivos

- Aumentar FPS e reduzir frame time.
- Reduzir consumo de CPU, RAM, GPU, VRAM e disco.
- Acelerar boot, primeiro carregamento e entrada em mapas.
- Melhorar renderização, LOD, foliage, terreno, água e iluminação.
- Reduzir custo server-side de IA, rede e simulação distante.
- Corrigir bugs e adicionar ferramentas administrativas e QoL.
- Preparar base para pathfinding, anticheat e customizações futuras.

## Melhorias aplicadas

### Boot, loading e memória

- Carregamento assíncrono e streaming direto dos master bundles.
- Asset worker sem busy-spin e descoberta de módulos sob demanda.
- Hash de arquivos por stream, com menos cópias e alocações.
- Carregamento lazy/deferred de itens, efeitos, skins, veículos, projéteis, mythics e skyboxes.
- Menos uploads temporários de terrain e cópias de foliage durante loading.
- Redução de arrays, iteradores e alocações em caminhos frequentes.
- Remoção de dependências, shaders e processamento sem uso.

### CPU, física e lógica

- Cache de tempo e resultados repetidos em zombies, animais, sentries, iluminação e animações.
- Distâncias ao quadrado e remoção de normalizações e raízes desnecessárias.
- Filas de itens, barricadas e estruturas processadas sem deslocamentos caros.
- Limpeza regional limitada às áreas anteriormente carregadas.
- Otimizações em buoyancy, água, clima, balística e animações fora da câmera.
- Atualizações pesadas distribuídas entre frames para reduzir picos.

### Renderização e GPU

- `World_Chunk_Radius` para limitar terreno, árvores, objetos, estradas e visibilidade regional.
- Terreno distante ocultado e LOD externo simplificado sem remover colisão.
- Foliage decorativo limitado por preset e distância regional.
- LOD antecipado, pausa de `LODGroup` invisível e sombras distantes reduzidas seletivamente.
- Distância de sombras vinculada à configuração gráfica e ao far clip.
- GPU Instancing habilitado em materiais Standard compatíveis.
- Menos amostras no shader de terrain quando neve não está ativa.
- Reflection probes preservados nas qualidades médias/altas.
- Sun Shafts restaurado com presets escaláveis.
- Água Ultra com onda procedural, reflexo ambiente, transparência e tonalidade azul; demais presets mantêm caminho leve.
- Modo opcional para reduzir renderização distante dentro do Unity Editor.

### Servidor e rede

- Zombies e animais distantes pausam simulação quando não existem jogadores próximos.
- Respawn normal distante suspenso; Horde e beacon preservados.
- Budgets configuráveis por frame para ticks de zombies e animais.
- Limpeza de relevância regional otimizada para itens, objetos, recursos, barricadas e estruturas.
- Permissões e ações administrativas validadas pelo servidor.
- Raio de chunks replicado para manter cliente e servidor consistentes.

### Administração e QoL

- Comandos restritos a admin/owner: `/fly`, `/noclip`, `/god`, `/heal` e `/speed 1-50`.
- Inventário administrativo em `F3` com busca, categorias, paginação, scroll e ícones.
- Catálogo automático de itens vanilla e mods, sem cosméticos.
- Compatibilidade de permissão com RocketMod através do comando `give`.
- Visualização administrativa dos chunks com `/drawchunks`.
- Opção client-side para ligar ou desligar fog da barreira de chunks.

### Unity 6.3, estabilidade e ferramentas

- Projeto migrado para Unity 6.3 LTS `6000.3.19f1`.
- APIs removidas e obsoletas atualizadas para Unity 6.
- Física, shaders, Post Processing e Build Tool ajustados para nova engine.
- Correção de textos TMP exibidos como blocos brancos ou coloridos.
- Correção do erro IMGUI `EndLayoutGroup` no Build Test.
- Windows usa D3D11 por padrão; DX12 permanece opt-in com `-force-d3d12`.
- Captura de performance standalone em CSV com frame, CPU, GPU, GC e memória.
- Cenários de stress, ranking de gargalos e guia de profiling documentados.
- Integração com MCP oficial da Unity e bridge local de fallback para diagnóstico do Editor.

## Estado e limitações

- Cliente e servidor devem usar a mesma build desta fork porque protocolo de configuração foi ampliado.
- Dedicated Release compila; Dedicated Development/Profile possui correção pendente nos guards de `StructureManager`.
- Build headless e smoke test completo de multiplayer, RocketMod, Workshop e saves ainda são obrigatórios antes de release.
- Pathfinding novo, anticheat e hitboxes customizadas continuam planejados, não concluídos.

## Como executar

1. Clone este repositório.
2. Instale Unity 6.3 LTS `6000.3.19f1` pelo [Unity Hub](https://unity.com/download).
3. Mantenha Steam aberto e [Unturned](https://store.steampowered.com/app/304930/Unturned/) instalado; bundles binários são carregados da instalação do jogo.
4. Abra projeto na Unity.
5. Abra cena `Assets/GameStartup.unity`.
6. Execute pelo Editor ou use `Window > Unturned > Build Tool`.

## Documentação

- [Visão geral e histórico técnico](DOCS/README.md)
- [Backlog](DOCS/TODO.md)
- [Top gargalos](DOCS/TOPLAG.md)
- [Profiling e testes](DOCS/PERFORMANCE_TESTING.md)
- [Auditoria server-side](DOCS/SERVER_SIDE.md)
- [Inventário administrativo](DOCS/ADMIN_INVENTORY.md)
- [Pesquisa de mesh e terreno](DOCS/MESH_TERRAIN_RESEARCH.md)
- [Unity MCP](DOCS/UNITY_MCP.md)

## Referências

- [Documentação oficial do U3 SDK](https://docs.smartlydressedgames.com/en/stable/u3-sdk/)
- [Documentação de modding do Unturned](https://docs.smartlydressedgames.com/en/stable/)
- [Licença](LICENSE.txt)
