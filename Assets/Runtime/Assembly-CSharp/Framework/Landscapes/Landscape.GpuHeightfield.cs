////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
#if DEDICATED_SERVER
namespace SDG.Framework.Landscapes
{
	public partial class Landscape
	{
		private bool TryPrepareGpuHeightfieldRenderer() => false;
		private void BeginGpuHeightfieldVisibilityUpdate() { }
		private void AddVisibleGpuHeightfieldTile(LandscapeTile tile) { }
		private void FinishGpuHeightfieldVisibilityUpdate() { }
		private void RenderGpuHeightfieldTerrain() { }
		private void DisposeGpuHeightfieldRenderer() { }
	}
}
#else
using SDG.Unturned;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Framework.Landscapes
{
	public partial class Landscape
	{
		private static readonly CommandLineFlag enableGpuHeightfieldTerrain = new CommandLineFlag(false, "-GpuHeightfieldTerrain");
		private static readonly CommandLineInt gpuHeightfieldQuadsArgument = new CommandLineInt("-GpuHeightfieldQuads");

		private const int DEFAULT_GPU_HEIGHTFIELD_QUADS = 128;
		private const int MIN_GPU_HEIGHTFIELD_QUADS = 32;
		private const int MAX_GPU_HEIGHTFIELD_QUADS = 256;

		private Material gpuHeightfieldMaterial;
		private Texture2DArray gpuHeightfieldHeightmaps;
		private Texture2DArray gpuHeightfieldHoles;
		private Texture2D gpuHeightfieldTileData;
		private Dictionary<LandscapeTile, int> gpuHeightfieldTileSlices;
		private Vector4[] gpuHeightfieldVisibleTiles;
		private Bounds gpuHeightfieldVisibleBounds;
		private bool hasGpuHeightfieldVisibleBounds;
		private bool isGpuHeightfieldUnavailable;
		private int gpuHeightfieldVisibleTileCount;
		private int gpuHeightfieldQuads;

		private bool TryPrepareGpuHeightfieldRenderer()
		{
			if (!enableGpuHeightfieldTerrain || isGpuHeightfieldUnavailable)
				return false;

			if (gpuHeightfieldMaterial != null && gpuHeightfieldTileSlices.Count == tiles.Count)
				return true;

			if (!SystemInfo.supports2DArrayTextures
				|| !SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)
				|| !SystemInfo.SupportsTextureFormat(TextureFormat.R8)
				|| SystemInfo.graphicsShaderLevel < 45)
			{
				UnturnedLog.warn("GPU heightfield terrain is unsupported by this graphics device; using Unity Terrain");
				isGpuHeightfieldUnavailable = true;
				return false;
			}

			if (tiles.Count < 1)
				return false;

			try
			{
				BuildGpuHeightfieldResources();
				return true;
			}
			catch (Exception exception)
			{
				UnturnedLog.exception(exception, "Unable to initialize experimental GPU heightfield terrain; using Unity Terrain");
				DisposeGpuHeightfieldRenderer();
				isGpuHeightfieldUnavailable = true;
				return false;
			}
		}

		private void BuildGpuHeightfieldResources()
		{
			DisposeGpuHeightfieldRenderer();

			Shader shader = Resources.Load<Shader>("Shaders/GpuHeightfieldTerrain");
			if (shader == null || !shader.isSupported)
				throw new InvalidOperationException("GpuHeightfieldTerrain shader is missing or unsupported");

			gpuHeightfieldQuads = DEFAULT_GPU_HEIGHTFIELD_QUADS;
			if (gpuHeightfieldQuadsArgument.hasValue)
			{
				int requestedQuads = Mathf.Clamp(gpuHeightfieldQuadsArgument.value,
					MIN_GPU_HEIGHTFIELD_QUADS, MAX_GPU_HEIGHTFIELD_QUADS);
				gpuHeightfieldQuads = Mathf.ClosestPowerOfTwo(requestedQuads);
			}

			int tileCount = tiles.Count;
			gpuHeightfieldHeightmaps = new Texture2DArray(HEIGHTMAP_RESOLUTION, HEIGHTMAP_RESOLUTION,
				tileCount, TextureFormat.RFloat, false, true)
			{
				name = "GPU Heightfield Heightmaps",
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			gpuHeightfieldHoles = new Texture2DArray(HOLES_RESOLUTION, HOLES_RESOLUTION,
				tileCount, TextureFormat.R8, false, true)
			{
				name = "GPU Heightfield Holes",
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};

			float[] heightUpload = new float[HEIGHTMAP_RESOLUTION * HEIGHTMAP_RESOLUTION];
			byte[] holesUpload = new byte[HOLES_RESOLUTION * HOLES_RESOLUTION];
			gpuHeightfieldTileSlices = new Dictionary<LandscapeTile, int>(tileCount);
			int slice = 0;
			foreach (LandscapeTile tile in tiles.Values)
			{
				for (int z = 0; z < HEIGHTMAP_RESOLUTION; ++z)
				{
					int rowOffset = z * HEIGHTMAP_RESOLUTION;
					for (int x = 0; x < HEIGHTMAP_RESOLUTION; ++x)
						heightUpload[rowOffset + x] = tile.heightmap[z, x];
				}

				for (int z = 0; z < HOLES_RESOLUTION; ++z)
				{
					int rowOffset = z * HOLES_RESOLUTION;
					for (int x = 0; x < HOLES_RESOLUTION; ++x)
						holesUpload[rowOffset + x] = tile.holes[z, x] ? byte.MaxValue : byte.MinValue;
				}

				gpuHeightfieldHeightmaps.SetPixelData(heightUpload, 0, slice);
				gpuHeightfieldHoles.SetPixelData(holesUpload, 0, slice);
				gpuHeightfieldTileSlices.Add(tile, slice);
				++slice;
			}

			gpuHeightfieldHeightmaps.Apply(false, true);
			gpuHeightfieldHoles.Apply(false, true);

			gpuHeightfieldMaterial = new Material(shader)
			{
				name = "Experimental GPU Heightfield Terrain",
				enableInstancing = true
			};
			gpuHeightfieldTileData = new Texture2D(tileCount, 1, TextureFormat.RGBAFloat, false, true)
			{
				name = "GPU Heightfield Visible Tiles",
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			gpuHeightfieldVisibleTiles = new Vector4[tileCount];
			gpuHeightfieldMaterial.SetTexture("_GpuHeightmaps", gpuHeightfieldHeightmaps);
			gpuHeightfieldMaterial.SetTexture("_GpuHoles", gpuHeightfieldHoles);
			gpuHeightfieldMaterial.SetTexture("_GpuHeightfieldTiles", gpuHeightfieldTileData);
			gpuHeightfieldMaterial.SetVector("_GpuHeightfieldParams", new Vector4(
				TILE_SIZE, TILE_HEIGHT, gpuHeightfieldQuads, 1f / HEIGHTMAP_RESOLUTION_MINUS_ONE));
			gpuHeightfieldMaterial.SetFloat("_GpuHeightfieldTileDataTexel", 1f / tileCount);

			UnturnedLog.info("Experimental GPU heightfield terrain enabled: {0} tiles, {1}x{1} quads",
				tileCount, gpuHeightfieldQuads);
		}

		private void BeginGpuHeightfieldVisibilityUpdate()
		{
			gpuHeightfieldVisibleTileCount = 0;
			hasGpuHeightfieldVisibleBounds = false;
		}

		private void AddVisibleGpuHeightfieldTile(LandscapeTile tile)
		{
			if (!gpuHeightfieldTileSlices.TryGetValue(tile, out int slice))
				return;

			gpuHeightfieldVisibleTiles[gpuHeightfieldVisibleTileCount++] = new Vector4(
				tile.coord.x * TILE_SIZE, tile.coord.y * TILE_SIZE, slice, 0f);

			if (hasGpuHeightfieldVisibleBounds)
				gpuHeightfieldVisibleBounds.Encapsulate(tile.worldBounds);
			else
			{
				gpuHeightfieldVisibleBounds = tile.worldBounds;
				hasGpuHeightfieldVisibleBounds = true;
			}
		}

		private void FinishGpuHeightfieldVisibilityUpdate()
		{
			if (gpuHeightfieldVisibleTileCount > 0)
			{
				gpuHeightfieldTileData.SetPixelData(gpuHeightfieldVisibleTiles, 0);
				gpuHeightfieldTileData.Apply(false, false);
			}
		}

		private void RenderGpuHeightfieldTerrain()
		{
			if (gpuHeightfieldVisibleTileCount < 1 || !hasGpuHeightfieldVisibleBounds)
				return;

			RenderParams renderParams = new RenderParams(gpuHeightfieldMaterial)
			{
				camera = MainCamera.instance,
				layer = LayerMasks.GROUND,
				worldBounds = gpuHeightfieldVisibleBounds,
				shadowCastingMode = ShadowCastingMode.On,
				receiveShadows = true,
				reflectionProbeUsage = ReflectionProbeUsage.Off
			};
			int vertexCount = gpuHeightfieldQuads * gpuHeightfieldQuads * 6;
			Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, vertexCount,
				gpuHeightfieldVisibleTileCount);
		}

		private void DisposeGpuHeightfieldRenderer()
		{
			if (gpuHeightfieldMaterial != null)
				UnityEngine.Object.Destroy(gpuHeightfieldMaterial);
			if (gpuHeightfieldHeightmaps != null)
				UnityEngine.Object.Destroy(gpuHeightfieldHeightmaps);
			if (gpuHeightfieldHoles != null)
				UnityEngine.Object.Destroy(gpuHeightfieldHoles);
			if (gpuHeightfieldTileData != null)
				UnityEngine.Object.Destroy(gpuHeightfieldTileData);

			gpuHeightfieldMaterial = null;
			gpuHeightfieldHeightmaps = null;
			gpuHeightfieldHoles = null;
			gpuHeightfieldTileData = null;
			gpuHeightfieldTileSlices = null;
			gpuHeightfieldVisibleTiles = null;
			gpuHeightfieldVisibleTileCount = 0;
			hasGpuHeightfieldVisibleBounds = false;
		}
	}
}
#endif // !DEDICATED_SERVER
