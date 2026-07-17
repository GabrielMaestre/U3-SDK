Shader "Hidden/Custom/SunShafts"
{
	HLSLINCLUDE

		#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

		TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
		TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
		TEXTURE2D_SAMPLER2D(_SunShaftsTexture, sampler_SunShaftsTexture);

		uniform float4 _SunPosition;
		uniform float3 _SunColor;
		uniform float _Intensity;
		uniform float _BlurStep;

		float4 FragMask(VaryingsDefault input) : SV_Target
		{
			float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, input.texcoord);
			float sky = step(0.9999, Linear01Depth(rawDepth));
			float distanceFromSun = distance(input.texcoord, _SunPosition.xy);
			float radialMask = saturate(1.0 - distanceFromSun / 0.75);
			radialMask *= radialMask;

			float3 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord).rgb;
			float brightness = max(source.r, max(source.g, source.b));
			float brightSky = saturate((brightness - 0.5) * 2.0);
			return sky * radialMask * brightSky;
		}

		float4 FragRadialBlur(VaryingsDefault input) : SV_Target
		{
			float2 uv = input.texcoord;
			float2 stepToSun = (_SunPosition.xy - uv) * _BlurStep;
			float3 sum = 0.0;

			[unroll]
			for (int index = 0; index < 8; ++index)
			{
				sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
				uv += stepToSun;
			}

			return float4(sum * 0.125, 1.0);
		}

		float4 FragComposite(VaryingsDefault input) : SV_Target
		{
			float4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
			float3 shafts = SAMPLE_TEXTURE2D(_SunShaftsTexture, sampler_SunShaftsTexture, input.texcoord).rgb;
			source.rgb += shafts * _SunColor * _Intensity;
			return source;
		}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM
				#pragma vertex VertDefault
				#pragma fragment FragMask
			ENDHLSL
		}

		Pass
		{
			HLSLPROGRAM
				#pragma vertex VertDefault
				#pragma fragment FragRadialBlur
			ENDHLSL
		}

		Pass
		{
			HLSLPROGRAM
				#pragma vertex VertDefault
				#pragma fragment FragComposite
			ENDHLSL
		}
	}
}
