Shader "Hidden/Unturned/GpuHeightfieldTerrain"
{
	Properties
	{
		_LowColor("Low Color", Color) = (0.18, 0.42, 0.12, 1)
		_HighColor("High Color", Color) = (0.42, 0.38, 0.25, 1)
		_SteepColor("Steep Color", Color) = (0.28, 0.25, 0.22, 1)
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Geometry-100"
			"RenderType" = "Opaque"
		}

		Stencil
		{
			Ref 1
			WriteMask 1
			Pass Replace
		}

		CGPROGRAM
		#pragma surface surf StandardSpecular vertex:GpuHeightfieldVert addshadow fullforwardshadows nolightmap nodynlightmap nometa
		#pragma target 4.5
		#include "UnityCG.cginc"

		UNITY_DECLARE_TEX2DARRAY(_GpuHeightmaps);
		UNITY_DECLARE_TEX2DARRAY(_GpuHoles);
		sampler2D _GpuHeightfieldTiles;
		float4 _GpuHeightfieldParams; // tile size, tile height, quads per axis, heightmap texel size
		float _GpuHeightfieldTileDataTexel;
		fixed4 _LowColor;
		fixed4 _HighColor;
		fixed4 _SteepColor;

		struct GpuHeightfieldVertex
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 tangent : TANGENT;
			float2 texcoord : TEXCOORD0;
			uint vertexID : SV_VertexID;
			uint instanceID : SV_InstanceID;
		};

		struct Input
		{
			float2 terrainUv;
			float heightmapSlice;
			float3 worldPos;
			float3 worldNormal;
		};

		float SampleHeight(float2 uv, float slice)
		{
			return UNITY_SAMPLE_TEX2DARRAY_LOD(_GpuHeightmaps, float3(saturate(uv), slice), 0).r;
		}

		float2 GetCorner(uint corner)
		{
			if (corner == 1)
				return float2(0, 1);
			if (corner == 2 || corner == 4)
				return float2(1, 1);
			if (corner == 5)
				return float2(1, 0);
			return float2(0, 0);
		}

		void GpuHeightfieldVert(inout GpuHeightfieldVertex vertex, out Input output)
		{
			UNITY_INITIALIZE_OUTPUT(Input, output);

			uint quads = (uint)_GpuHeightfieldParams.z;
			uint cell = vertex.vertexID / 6;
			uint2 cellCoord = uint2(cell % quads, cell / quads);
			float2 gridCoord = float2(cellCoord) + GetCorner(vertex.vertexID % 6);
			float2 uv = gridCoord / quads;
			float tileDataU = (vertex.instanceID + 0.5) * _GpuHeightfieldTileDataTexel;
			float4 tile = tex2Dlod(_GpuHeightfieldTiles, float4(tileDataU, 0.5, 0, 0));
			float height = SampleHeight(uv, tile.z);

			float texel = _GpuHeightfieldParams.w;
			float heightRight = SampleHeight(uv + float2(texel, 0), tile.z);
			float heightUp = SampleHeight(uv + float2(0, texel), tile.z);
			float worldStep = _GpuHeightfieldParams.x * texel;
			float3 normal = normalize(float3(
				(height - heightRight) * _GpuHeightfieldParams.y,
				worldStep,
				(height - heightUp) * _GpuHeightfieldParams.y));

			vertex.vertex = float4(tile.x + uv.x * _GpuHeightfieldParams.x,
				height * _GpuHeightfieldParams.y - _GpuHeightfieldParams.y * 0.5,
				tile.y + uv.y * _GpuHeightfieldParams.x, 1);
			vertex.normal = normal;
			vertex.tangent = float4(normalize(cross(normal, float3(0, 0, 1))), -1);
			vertex.texcoord = uv;
			output.terrainUv = uv;
			output.heightmapSlice = tile.z;
		}

		void surf(Input input, inout SurfaceOutputStandardSpecular output)
		{
			float hole = UNITY_SAMPLE_TEX2DARRAY(_GpuHoles,
				float3(saturate(input.terrainUv), input.heightmapSlice)).r;
			clip(hole - 0.5);

			float normalizedHeight = saturate((input.worldPos.y + _GpuHeightfieldParams.y * 0.5)
				/ _GpuHeightfieldParams.y);
			float steepness = 1 - saturate(input.worldNormal.y);
			output.Albedo = lerp(lerp(_LowColor.rgb, _HighColor.rgb, normalizedHeight),
				_SteepColor.rgb, steepness);
			output.Specular = 0;
			output.Smoothness = 0;
			output.Alpha = 1;
		}
		ENDCG
	}

	Fallback Off
}
