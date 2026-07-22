# LDM / Rocket — Contexto de Integração

Atualizado: 2026-07-21.

Documento-base para trabalho isolado em LDM (Legally Distinct Missile), fork mantida de Rocket para Unturned. Escopo: plugins, compatibilidade, desempenho de servidor e relação com alterações de rede desta fork.

## Estado atual

- LDM/Rocket não está incluída como código-fonte neste repositório. É módulo externo carregado pelo servidor.
- O jogo procura módulo `Rocket.Unturned` durante startup do dedicado.
- A integração existente preserva hooks legados, incluindo `RocketLegacyOnDeath`, publicidade de plugins e permissões de comandos.
- Código recomenda LDM/fork mantida por compatibilidade e correções de exceções de multithread e exploits de teleporte.
- Antes de alterar integração, confirmar versão instalada em `Modules/Rocket.Unturned` e testar com build dedicado desta fork.

## Compatibilidade obrigatória

1. Cliente e servidor devem usar mesma fork. `ReplicateConfig` recebeu `World_Chunk_Radius` no início da mensagem. Cliente vanilla ou build antiga é incompatível.
2. Plugin Rocket/LDM roda somente no servidor. Nunca distribuir assembly de plugin ao cliente.
3. Plugins devem consumir APIs públicas e hooks existentes. Não injetar, reordenar ou escrever diretamente mensagens `NetMessages`/`ReplicateConfig`.
4. Não modificar saves, GUIDs de assets ou formatos de inventário sem migração e rollback.
5. O código compara versão mínima `4.9.3.1`, mas mensagem exibida cita `4.9.3.3+`. Confirmar versão do módulo distribuído antes de corrigir esse desencontro.

## Alterações de rede já aplicadas

| Alteração | Estado | Efeito |
| --- | --- | --- |
| Limpeza de flags carregadas | Concluída | Itens, objetos, recursos, barricadas e estruturas limpam somente anel regional anterior por jogador. |
| Cache de visibilidade global | Concluída | Permissão é calculada uma vez por destinatário durante snapshot de jogadores. |
| Relevância espacial | Concluída parcialmente | Itens usam raio `1`; objetos, recursos, barricadas e estruturas usam raio `2`. Zombies e animais usam interesse regional. |
| `World_Chunk_Radius` | Concluída | Limita janela visual e simulação distante; alterou protocolo `ReplicateConfig`. |
| Lookup de conexão SteamNetworkingSockets | Concluída | `HSteamNetConnection -> TransportConnection` usa `Dictionary`; cada pacote evita busca linear em conexões. |

Transporte padrão é `SteamNetworkingSockets`. Não trocar para `SystemSockets` sem benchmark: implementação alternativa percorre conexões e mantém fila de buffers por update.

## Regras para plugins LDM

- Nunca executar banco de dados, HTTP, arquivo grande ou espera bloqueante no thread Unity.
- Trabalho externo pode ocorrer fora do thread principal; resultado que acessa Unity/Unturned retorna ao thread do jogo.
- Não criar `Update` por jogador/entidade. Usar evento, timer com frequência limitada ou região ativa.
- Cachear permissões, configuração e lookup de assets. Invalidar cache quando plugin recarrega ou permissão muda.
- Evitar LINQ, `Find`, `GetComponent`, serialização e alocação dentro de hooks frequentes como movimento, dano, chat e snapshots.
- Validar toda entrada remota: tamanho, estado, permissão, owner, distância e rate limit. Plugin não substitui autoridade do servidor.
- Um plugin não deve alterar tick, posição ou inventário fora de API autorizada. Resultados precisam ser reproduzíveis após reconnect e restart.

## Ordem de investigação de performance

1. Medir `Provider.Update`, `listenServer`, `NetMessages.ReceiveMessageFromClient`, bytes e pacotes por segundo.
2. Medir handlers LDM por plugin: tempo, chamadas, GC alloc e exceções.
3. Desativar metade dos plugins em servidor de teste para isolar agressor; repetir até identificar plugin/mensagem.
4. Só então alterar payload, frequência, cache ou scheduler.

Não criar compressão, fila global, thread extra, novo transporte ou migração para outro framework sem captura que prove gargalo.

## Checklist de smoke test

1. Build dedicado Release em `Builds/Windows64_Headless/Unturned.exe`.
2. Iniciar com `+LANServer/LDMTest` e carregar mapa vanilla.
3. Instalar LDM e plugins de teste; confirmar carregamento sem erro no console.
4. Conectar dois clientes desta fork, usar comandos admin e confirmar negação para não-admin.
5. Testar chat, morte, inventário, item, veículo, estrutura, teleport permitido e reconnect.
6. Afastar jogadores para validar chunks, zombies e animais; retornar à região.
7. Reiniciar servidor; confirmar save, plugins, permissões e ausência de duplicação.
8. Executar 30 minutos e registrar CPU p95/p99, memória, GC, bytes/pacotes, disconnects e erros.

## Pendências

- Instrumentar bytes, mensagens, serialização e CPU por jogador/sistema.
- Criar budgets/backpressure por conexão somente após load test.
- Mapear hooks LDM ativos e custo por plugin.
- Revisar divergência de versão mínima LDM exibida no startup.
- Testar Workshop, Rocket/LDM e saves com servidor dedicado Release.

## Arquivos de referência

- `Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs`: detecção e requisito de Rocket/LDM.
- `Assets/Runtime/Assembly-CSharp/Framework/Modules/ModuleHook.cs`: carregamento de módulos no dedicado.
- `Assets/Runtime/Assembly-CSharp/Unturned/Provider/Provider.cs`: recebimento de rede e conexão.
- `Assets/Runtime/Assembly-CSharp/NetTransport_SteamNetworkingSockets/ServerTransport_SteamNetworkingSockets.cs`: transporte padrão e índice de conexões.
- `DOCS/SERVER_SIDE.md`: riscos e smoke test geral do servidor.
- `DOCS/TODO.md`: backlog de rede e servidor dedicado.
