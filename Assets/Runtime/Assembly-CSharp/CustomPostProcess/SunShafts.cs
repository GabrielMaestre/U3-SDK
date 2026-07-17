////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace SDG.Unturned
{
	[Serializable]
	[PostProcess(typeof(SunShaftsRenderer), PostProcessEvent.BeforeStack, "Custom/Sun Shafts")]
	public sealed class SunShafts : PostProcessEffectSettings
	{
		public Vector3Parameter sunWorldPosition = new Vector3Parameter();
		public ColorParameter sunColor = new ColorParameter { value = Color.white };
		public FloatParameter intensity = new FloatParameter { value = 1.0f };
		public IntParameter downsample = new IntParameter { value = 2 };
		public IntParameter iterations = new IntParameter { value = 2 };

		public override bool IsEnabledAndSupported(PostProcessRenderContext context)
		{
			return enabled.value && Level.isLoaded && intensity.value > 0.001f && sunColor.value.maxColorComponent > 0.001f;
		}
	}

	public sealed class SunShaftsRenderer : PostProcessEffectRenderer<SunShafts>
	{
		public override void Init()
		{
			base.Init();

			shader = Shader.Find("Hidden/Custom/SunShafts");
			temporaryAId = Shader.PropertyToID("_SunShaftsTemporaryA");
			temporaryBId = Shader.PropertyToID("_SunShaftsTemporaryB");
			sunShaftsTextureId = Shader.PropertyToID("_SunShaftsTexture");
			sunPositionId = Shader.PropertyToID("_SunPosition");
			sunColorId = Shader.PropertyToID("_SunColor");
			intensityId = Shader.PropertyToID("_Intensity");
			blurStepId = Shader.PropertyToID("_BlurStep");
		}

		public override DepthTextureMode GetCameraFlags()
		{
			return DepthTextureMode.Depth;
		}

		public override void Render(PostProcessRenderContext context)
		{
			Vector3 sunPosition = context.camera.WorldToViewportPoint(settings.sunWorldPosition.value);
			const float maxRadius = 0.75f;
			if (sunPosition.z <= 0.0f || sunPosition.x < -maxRadius || sunPosition.x > 1.0f + maxRadius
				|| sunPosition.y < -maxRadius || sunPosition.y > 1.0f + maxRadius)
			{
				context.command.BlitFullscreenTriangle(context.source, context.destination);
				return;
			}

			int downsample = Mathf.Clamp(settings.downsample.value, 1, 4);
			int width = Mathf.Max(1, context.width / downsample);
			int height = Mathf.Max(1, context.height / downsample);
			CommandBuffer command = context.command;
			context.GetScreenSpaceTemporaryRT(command, temporaryAId, 0, RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.Linear, FilterMode.Bilinear, width, height);
			context.GetScreenSpaceTemporaryRT(command, temporaryBId, 0, RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.Linear, FilterMode.Bilinear, width, height);

			PropertySheet sheet = context.propertySheets.Get(shader);
			sheet.properties.SetVector(sunPositionId, sunPosition);
			sheet.properties.SetColor(sunColorId, settings.sunColor.value);
			sheet.properties.SetFloat(intensityId, settings.intensity.value);

			command.BlitFullscreenTriangle(context.source, temporaryAId, sheet, 0);

			int source = temporaryAId;
			int destination = temporaryBId;
			int iterations = Mathf.Clamp(settings.iterations.value, 1, 3);
			for (int index = 0; index < iterations; ++index)
			{
				// Match legacy effect's widening steps while keeping work at half/quarter resolution.
				sheet.properties.SetFloat(blurStepId, (2.5f / 768.0f) * ((index * 2) + 1) * 6.0f);
				command.BlitFullscreenTriangle(source, destination, sheet, 1);
				int swap = source;
				source = destination;
				destination = swap;
			}

			command.SetGlobalTexture(sunShaftsTextureId, source);
			command.BlitFullscreenTriangle(context.source, context.destination, sheet, 2);
			command.ReleaseTemporaryRT(temporaryAId);
			command.ReleaseTemporaryRT(temporaryBId);
		}

		private Shader shader;
		private int temporaryAId;
		private int temporaryBId;
		private int sunShaftsTextureId;
		private int sunPositionId;
		private int sunColorId;
		private int intensityId;
		private int blurStepId;
	}

	public partial class GraphicsSettings
	{
		static partial void ApplySunShaftsSettings()
		{
			UnturnedPostProcess.instance?.syncSunShafts();
		}
	}

	public partial class LevelLighting
	{
		static partial void UpdateSunShafts(Transform sunTransform, Color sunColor)
		{
			UnturnedPostProcess.instance?.updateSunShafts(sunTransform, sunColor);
		}

		static partial void UpdateSunShaftsIntensity(float intensity)
		{
			UnturnedPostProcess.instance?.updateSunShaftsIntensity(intensity);
		}
	}
}
