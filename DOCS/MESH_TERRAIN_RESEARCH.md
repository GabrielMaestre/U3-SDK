# Estudo de mesh e terrain inspirado por Vercidium

Status: LOD nativo aplicado em `2026-07-16`; spike client-side `GPU Heightfield Terrain Renderer` criado em branch separada em `2026-07-17`.

Fontes principais:

- [Vídeo: When Your Game Is Bad But Your Optimisation Is Genius](https://www.youtube.com/watch?v=5zlfJW2VGLM)
- [`vercidium-patreon/glvertexid`](https://github.com/vercidium-patreon/glvertexid): heightmap renderizado com dados mínimos e posição gerada no vertex shader.
- [`vercidium-patreon/meshing`](https://github.com/vercidium-patreon/meshing): greedy meshing para mundo voxel.
- [Unity 6.3: Terrain.drawInstanced](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Terrain-drawInstanced.html)
- [Unity 6.3: Terrain.heightmapMaximumLOD](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Terrain-heightmapMaximumLOD.html)
- [Unity 6.3: Graphics.RenderMeshIndirect](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html)
- [Unity: compressão de mesh](https://docs.unity3d.com/2023.2/Documentation/Manual/mesh-compression.html)

## Conclusão

Conceitos são válidos, mas renderer do Vercidium não deve ser transplantado. Ele parte de voxels em grade 3D e renderer OpenGL próprio. Unturned usa Unity Terrain como heightfield, meshes importadas, Built-in Render Pipeline, `LevelBatching` e foliage já instanciado.

Melhores candidatos para Unturned:

1. LOD nativo mais agressivo somente em tiles de terreno inteiramente distantes.
2. Comparar static batching e GPU instancing para uma categoria repetida por vez.
3. Auditar meshes importadas: `Read/Write Enabled`, canais de vértice sem uso, Vertex Compression e Optimize Mesh.
4. Considerar indirect rendering somente depois de provar custo alto de submissão CPU e necessidade de culling na GPU.

Greedy meshing não serve para terreno atual: heightfield já contém apenas superfície, sem faces internas de cubos para remover. Aplicar em árvores, casas ou modelos importados alteraria topologia, UV, materiais, iluminação e compatibilidade com bundles.

## Aplicado

- `Landscape` mantém LOD original nos 75% próximos da distância visual.
- Tile cujo ponto mais próximo já está no anel externo de 25% recebe `Terrain.heightmapMaximumLOD = 1`; segundo Unity, limite máximo de triângulos cai para um quarto.
- Cálculo ocorre no update regional existente, não por frame.
- Cinematic Mode força LOD original. Tile invisível continua sem heightmap.
- Colisão, `TerrainData`, holes, splatmaps, materiais, FOV, câmera e gameplay não mudaram.
- Teste cobre lado próximo, limite exato e lado distante. Assemblies Unity compilaram e método compilado passou por reflexão (`near=0`, `far=1`).

## Spike: GPU Heightfield Terrain Renderer

Branch: `GpuHeightfieldTerrainRenderer`. Ativação exclusiva para teste standalone: `-GpuHeightfieldTerrain`.

- Renderer é desligado por padrão e volta automaticamente ao Unity Terrain quando shader, `Texture2DArray` ou Shader Model 4.5 não estiver disponível.
- Heightmaps e holes são lidos diretamente dos arrays carregados pelo formato atual. Mapas oficiais e Workshop não exigem conversão, arquivo novo ou ajuste de protocolo.
- `TerrainData`, `TerrainCollider`, colisão, física, nav, foliage e queries de altura continuam nativos. Somente desenho do heightfield muda no cliente.
- Servidor dedicado compila stubs sem código gráfico. Nenhum pacote, RPC, hash de mapa ou estado replicado foi alterado.
- Tiles visíveis usam `Graphics.RenderPrimitives`, `SV_VertexID`, `SV_InstanceID`, texture arrays e um único envio de primitivas para reduzir submissão na CPU. Lista de tiles só é atualizada ao cruzar região ou trocar raio.
- `-GpuHeightfieldQuads=32|64|128|256` controla densidade. Padrão experimental: `128`; valores intermediários são limitados e arredondados para potência de dois.
- Shader atual preserva relevo, holes, iluminação Deferred/Forward e sombras, mas usa cores diagnósticas. Splatmaps, oito layers, chuva/neve, reflexos planares e paridade visual ainda não estão prontos.
- Lightmaps, Dynamic GI e Meta pass ficam explicitamente desativados no spike porque geometria procedural não fornece UV1/UV2; iluminação dinâmica e sombras continuam ativas, sem warnings falsos no build.
- Um bounds agregado cobre tiles visíveis. Isso reduz custo de submissão, mas transfere granularidade de culling para lista regional; compute/indirect culling permanece fora do primeiro spike.

Critério antes de evoluir: comparar build sem argumento contra build com argumento, mesma rota e preset. Medir CPU `FinishFrameRendering`, Render Thread, SetPass, draws, GPU, p95/p99, RAM/VRAM, holes e seams. Não tornar padrão sem ganho repetível e paridade visual.

## Estado atual do Unturned

- `LandscapeTile` usa Unity `Terrain`, tiles de `1024 m`, heightmap `257×257`, splatmap `256×256` e `drawInstanced` quando hardware suporta.
- `Terrain.heightmapPixelError` já varia por qualidade: Low `64`, Medium `32`, High `16`, Ultra `8`.
- Tiles fora do raio visual já desligam `drawHeightmap`, mas granularidade lógica é tile de `1024 m`; Unity mantém culling e LOD internos dentro do tile visível.
- Foliage já usa `Graphics.DrawMeshInstanced`.
- Objetos, recursos e estradas usam `LevelBatching`/static batching. Unity prioriza static batching sobre GPU instancing, portanto ativar ambos não gera soma automática.
- Captura recente mostrou `2.354` draw calls e `528` batches, porém GPU não foi capturada. Triângulos totais de `254 mil` não justificam reescrever terreno sem nova evidência.

## Avaliação das técnicas

### Greedy meshing e remoção de faces internas — não aplicar

Excelente para voxels sólidos adjacentes. Sem ganho equivalente em heightfield ou meshes artísticas. Possível somente se surgir subsistema voxel separado no futuro.

### Posição por vertex ID e remoção de UV — protótipo distante

Conceito pode gerar grade do terreno no shader e buscar altura em texture/buffer. Em Unity/D3D seria shader compatível com `SV_VertexID`, não cópia direta de `gl_VertexID`/OpenGL.

Substituir Unity Terrain exigiria reproduzir LOD, costura de tiles, holes, oito layers, normais, sombras, fog, reflexão, satellite capture, edição, mapas legados e mods. Risco alto. `Terrain.drawInstanced` já busca objetivo parecido por caminho nativo, embora implementação interna não seja assumida como igual.

### Compressão de vértices — aplicar após auditoria de assets

Faz sentido para meshes estáticas. Ordem segura:

1. Desligar `Read/Write Enabled` onde runtime não acessa vértices; evita cópia CPU adicional.
2. Confirmar `Optimize Mesh` no importador; Unity reordena índices/vértices para cache de GPU.
3. Remover tangents, colors ou UV sets apenas quando shader, lightmap e gameplay não usam esses canais.
4. Testar Vertex Compression em normals/tangents/UVs. Não comprimir position ou lightmap UV globalmente sem inspeção visual.

Core models/textures estão no `core.masterbundle`, fora deste repositório. Auditoria real precisa pipeline fonte/exportável; modificar bundle compilado não é fluxo seguro.

Auditoria local encontrou somente `28` FBX de suporte, personagens e editor, não catálogo core. Muitos estão `isReadable: 1`, mas `LevelBatching` exige mesh legível para copiar UVs e nav/clip/collision também dependem de leitura. Desligar em lote foi rejeitado; mudança precisa ser por asset após `-ValidateAssets` e teste de batching/colisão.

### GPU instancing — testar seletivamente

Bom para árvores, pedras e objetos com mesmo mesh/material. Não serve diretamente para `SkinnedMeshRenderer` de players/zombies. Primeiro usar Frame Debugger para localizar par mesh/material repetido; depois comparar categoria isolada com `LevelBatching` versus instancing. Preservar lightmaps, probes, sombras, materiais por instância, seleção, interação e culling regional.

### Draw indireto, buffer de chunks e draw call único — adiar

Unity 6.3 oferece `Graphics.RenderMeshIndirect`, mas ele exige mesmo mesh, shader customizado, compute support e bounds corretos. Um único bounds para lote grande piora culling. Vantagem aparece quando GPU também decide visibilidade/quantidade; com lista já conhecida pela CPU, instancing normal é menor e mais seguro.

Um draw call para mundo inteiro não é meta realista: materiais, passes Deferred, sombras, transparência e lightmaps separam draws.

### Seis meshes por direção — não aplicar

Útil em faces de voxels alinhadas aos seis eixos. Mesh comum já usa backface culling na GPU. Separar árvores/casas em seis submeshes aumenta estado, draws e complexidade; folhas two-sided perderiam visual.

### Triangle strips — não priorizar

Economia depende de topologia regular e renderer customizado. Unity importa meshes trianguladas e já otimiza ordem de índices. Ganho potencial menor que risco de trocar pipeline e shaders.

### LOD e sinking — usar somente LOD nativo

LOD faz sentido. Técnica de afundar meshes cria movimento visual, overlap e overdraw; inadequada para High/Ultra e sem necessidade no Unity Terrain. Preferir `heightmapPixelError`, `heightmapMaximumLOD`, `LODGroup` e transições existentes.

## Primeiro experimento proposto

Nenhum código antes desta validação:

1. Capturar standalone em High com CPU, GPU Usage, Rendering e Frame Debugger no mesmo ponto de Russia.
2. Confirmar custo e triângulos de cada `Terrain` e distância do tile.
3. Confirmar que `heightmapMaximumLOD = 1` aparece somente em tiles inteiramente distantes; valor `1` limita triângulos máximos a um quarto segundo Unity.
4. Atualizar apenas ao cruzar região, com histerese. Nunca recalcular por frame.
5. Validar seams, holes, cavernas, scope, veículo, teleporte, planar reflection, satellite capture, High/Ultra e colisão.
6. Aceitar somente com redução medida de `RenderDeferred.GBuffer`, `ExecuteRenderQueueJob`, triângulos ou GPU sem mudança visual próxima nem regressão de p95/p99.

Segundo experimento independente: escolher um único mesh/material repetido no Frame Debugger e comparar static batching contra GPU instancing. Não alterar sistema global.

## Critério para renderer customizado

Renderer de terrain por `SV_VertexID` só entra em protótipo separado se:

- Terrain continuar entre principais custos após LOD nativo;
- GPU capture confirmar vertex/bandwidth como limite;
- ganho esperado superar manutenção de shader, editor, holes, materiais e mods;
- Unity Terrain invisível puder continuar como collider sem duplicar memória de forma pior;
- comparação visual automatizada e rollback existirem.

Até lá: não reescrever Terrain, não converter mundo para voxels, não copiar SSBO/OpenGL, não impor DX12.
