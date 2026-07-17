Shader "Custom/Water_Fallback"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
		[HideInInspector] _WaterQuality ("Water Quality", Float) = 1
    }
    SubShader
    {
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

        LOD 200
		Cull Off

        CGPROGRAM

        #pragma surface surf Standard alpha
        #pragma target 3.0

        struct Input
        {
			float3 viewDir;
			float3 worldPos;
        };

        fixed4 _BaseColor;
		half _WaterQuality;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			half isUltra = step(3.5h, _WaterQuality);
			o.Albedo = _BaseColor.rgb;
			o.Metallic = 0;
			o.Smoothness = lerp(0.35h, 0.9h, isUltra);
			if (isUltra > 0.5h)
			{
				float2 position = IN.worldPos.xz * 0.035f;
				float time = _Time.y * 0.45f;
				half2 wave = half2(sin(position.x + time), cos(position.y - time));
				o.Normal = normalize(half3(wave * 0.08h, 1.0h));
			}
			o.Alpha = 0.9;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
