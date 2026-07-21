# Observações de renderização e granularidade — 2026-07-20

## Origem

Inspeção visual no Unity Editor, jogo parado, Scene View em wireframe e Statistics. Não houve alteração de código nesta rodada.

Última Statistics capturada no local: `105,5 FPS` (`9,5 ms`), CPU main `9,5 ms`, render thread `5,9 ms`, `344` batches, `182` SetPass calls, `405` shadow casters, `279,9k` triângulos e `584,7k` vértices.

## Observado

- Árvores, mato/clutter e pedras têm muita geometria visível no wireframe.
- Estradas aparecem como meshes extensos; precisam de Frame Debugger para confirmar se um mesh inteiro é submetido quando apenas trecho está visível.
- Veículos distantes apareceram na Scene View. Isso não prova render no Player: Scene View possui câmera, distância e carregamento próprios.
- Terreno e água mostram superfícies amplas carregadas no Editor. Terrain atual usa tiles de `1024 m`; região de gameplay/rede mede `128 m`.

## Estado atual no código

- Foliage usa instancing e limite client-side de uma região radial.
- Objetos/árvores/terrain seguem `World_Chunk_Radius`; terrain distante desliga `drawHeightmap` e último anel usa LOD nativo menor.
- `LevelObject` desliga renderer e `LODGroup` fora de visibilidade regional; último LOD exclusivo não projeta sombra.
- Regiões são grade `64 x 64`, tamanho fixo `128 m`, usadas por itens, objetos, recursos, barricadas, estruturas, IA, rede e saves.

## Chunks menores?

Faz sentido para **render client-side**, não para substituir regiões globais agora.

Vantagem: célula visual de `32–64 m` pode ocultar estrada, árvore, veículo e estrutura parcialmente distantes. Menos triângulos, shadows e draws submetidos.

Desvantagem: mudar região global de `128 m` para `64 m` quadruplica células (`4096` para `16384`) e toca protocolos, arrays, saves, streaming, teleporte e multiplayer. Alto risco, ganho não comprovado.

Direção segura: manter região de simulação/rede em `128 m`; testar subcélulas visuais somente para grupos caros já medidos. Não dividir Terrain, estrada ou veículo antes de medir custo individual.

## Próxima medição obrigatória

1. Build standalone, mesma posição e qualidade Ultra; Scene View não serve como baseline.
2. Frame Debugger: selecionar uma árvore, mato, estrada e veículo distante. Registrar mesh, material, triângulos, draw, batch e motivo de não culling.
3. Comparar câmera olhando para/contra área densa. Registrar `Camera.Render`, `Culling`, `Render.OpaqueGeometry`, `SetPass`, triângulos e shadow casters.
4. Só criar protótipo de subcélula visual se estrada, árvore ou veículo aparecer como custo dominante no frame medido.

## Decisão

Nenhuma mudança aplicada. Hipótese prioritária: granularidade visual de foliage/estradas/veículos, validada primeiro por Frame Debugger e Profiler standalone.
