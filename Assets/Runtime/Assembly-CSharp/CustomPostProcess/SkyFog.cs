////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using SDG.Framework.Devkit;
using SDG.Framework.Water;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace SDG.Unturned
{
	[Serializable]
	[PostProcess(typeof(SkyFogRenderer), PostProcessEvent.BeforeTransparent, "Custom/SkyFog")]
	public sealed class SkyFog : PostProcessEffectSettings
	{

	}

	public sealed class SkyFogRenderer : PostProcessEffectRenderer<SkyFog>
	{
		public override void Init()
		{
			base.Init();

			shader = Shader.Find("Hidden/Custom/SkyFog");
			fogColorId = Shader.PropertyToID("_FogColor");
			skyColorId = Shader.PropertyToID("_SkyColor");
			equatorColorId = Shader.PropertyToID("_EquatorColor");
			groundColorId = Shader.PropertyToID("_GroundColor");
			inverseProjectionMatrixId = Shader.PropertyToID("_InverseProjectionMatrix");
			cameraToWorldMatrixId = Shader.PropertyToID("_CameraToWorld");
			distanceFogEnabledId = Shader.PropertyToID("_DistanceFogEnabled");

			waterColorId = Shader.PropertyToID("_WaterColor");
			isCameraUnderwaterId = Shader.PropertyToID("_IsCameraUnderwater");
			waterCountId = Shader.PropertyToID("_WaterCount");
			waterMatricesId = Shader.PropertyToID("_WaterMatrices");
		}

		public override void Render(PostProcessRenderContext context)
		{
			PropertySheet sheet = context.propertySheets.Get(shader);

			// _FogColor uniform is declared in Fog.hlsl
			sheet.properties.SetColor(fogColorId, RenderSettings.fogColor);

			sheet.properties.SetColor(skyColorId, RenderSettings.skybox.GetColor(skyColorId));
			sheet.properties.SetColor(equatorColorId, RenderSettings.skybox.GetColor(equatorColorId));
			sheet.properties.SetColor(groundColorId, RenderSettings.skybox.GetColor(groundColorId));

			sheet.properties.SetMatrix(inverseProjectionMatrixId, context.camera.projectionMatrix.inverse);
			sheet.properties.SetMatrix(cameraToWorldMatrixId, context.camera.cameraToWorldMatrix);
			sheet.properties.SetFloat(distanceFogEnabledId, GraphicsSettings.IsWorldChunkFogEnabled && !LevelLighting.isSea ? 1.0f : 0.0f);

			FindRelevantWaterVolumes(context.camera.transform.position);
			int waterCount = LevelLighting.enableUnderwaterEffects ? Mathf.Min(relevantWaterVolumes.Count, MAX_WATER_COUNT) : 0;
			// Disable underwater effects if we do not have any water, otherwise values from level may affect the menu.
			bool isCameraUnderwater = LevelLighting.isSea && waterCount > 0;
			sheet.properties.SetColor(waterColorId, LevelLighting.getSeaColor("_BaseColor"));
			sheet.properties.SetFloat(isCameraUnderwaterId, isCameraUnderwater ? 1.0f : 0.0f);
			sheet.properties.SetInt(waterCountId, waterCount);
			for (int waterIndex = 0; waterIndex < waterCount; ++waterIndex)
			{
				waterMatrices[waterIndex] = relevantWaterVolumes[waterIndex].volume.transform.worldToLocalMatrix;
			}
			sheet.properties.SetMatrixArray(waterMatricesId, waterMatrices);

			context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
		}

		private void FindRelevantWaterVolumes(Vector3 viewPosition)
		{
			UnityEngine.Profiling.Profiler.BeginSample("FindRelevantWaterVolumes");
			relevantWaterVolumes.Clear();
			List<WaterVolume> volumesToTest = WaterVolumeManager.Get().GetOverlapTestVolumes(viewPosition);
			if (volumesToTest == null)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			foreach (WaterVolume volume in volumesToTest)
			{
				Vector3 closestPoint = volume.GetClosestWorldPosition(viewPosition);
				float sqrDistance = (viewPosition - closestPoint).sqrMagnitude;
				if (sqrDistance < 4.0f)
				{
					relevantWaterVolumes.Add(new VolumeAlphaPair<WaterVolume>(volume, sqrDistance));
				}
			}

			if (relevantWaterVolumes.Count > MAX_WATER_COUNT)
			{
				relevantWaterVolumes.Sort(volumeComparison);
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private Shader shader;
		private int fogColorId;
		private int skyColorId;
		private int equatorColorId;
		private int groundColorId;
		private int inverseProjectionMatrixId;
		private int cameraToWorldMatrixId;
		private int distanceFogEnabledId;

		private int waterColorId;
		private int isCameraUnderwaterId;
		private int waterCountId;
		private int waterMatricesId;

		private const int MAX_WATER_COUNT = 3;
		private static Matrix4x4[] waterMatrices = new Matrix4x4[MAX_WATER_COUNT];
		private static List<VolumeAlphaPair<WaterVolume>> relevantWaterVolumes = new List<VolumeAlphaPair<WaterVolume>>();
		private static System.Comparison<VolumeAlphaPair<WaterVolume>> volumeComparison = CompareVolumes;
		private static int CompareVolumes(VolumeAlphaPair<WaterVolume> lhs, VolumeAlphaPair<WaterVolume> rhs)
		{
			return lhs.alpha.CompareTo(rhs.alpha);
		}
	}
}
