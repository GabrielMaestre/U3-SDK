Renderização CPU — maior ofensor
FinishFrameRendering ≈ 5,67 ms. Muitas submissões para GPU: 352 SetPass, 2,4k Draw Calls, batches e culling.
Exemplo: muitas árvores, casas e sombras visíveis. CPU prepara cada grupo antes da GPU desenhar.
Sombras + Deferred Rendering
Render.OpaqueGeometry, GBuffer e Deferred Lighting dominam render. Havia 773 Shadow Casters.
Exemplo: árvore distante ainda projeta sombra. Ela custa render normal + render no mapa de sombra.
Scripts/ticks por frame
ScriptRunBehaviourUpdate ≈ 2,20 ms. IA, players, luzes, UI e entidades ativas acumulam custo.
Exemplo: zombie/animal/luz distante ainda calcula estado mesmo sem jogador perto.
Resumo: jogo está mais limitado por CPU preparando render e muitos objetos/sombras do que por GPU. Reescrever terreno ou criar Update Manager global não é maior ganho atual.