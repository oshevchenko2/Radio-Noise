Shader "Custom/HDRP/BlendBiomesShader"
{
    Properties
    {
        _DesertTex ("Desert Texture", 2D) = "white" {}
        _PlainsTex ("Plains Texture", 2D) = "white" {}
        _ForestTex ("Forest Texture", 2D) = "white" {}
        _SwampTex ("Swamp Texture", 2D) = "white" {}
        _MountainsTex ("Mountains Texture", 2D) = "white" {}
        _BlendFactor ("Blend Factor", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma target 5.0
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.highdefinition/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_DesertTex);
            TEXTURE2D(_PlainsTex);
            TEXTURE2D(_ForestTex);
            TEXTURE2D(_SwampTex);
            TEXTURE2D(_MountainsTex);

            float _BlendFactor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
            };

            float4 BlendTextures(float2 uv)
            {
                float blend = _BlendFactor;

                float4 desertColor = tex2D(_DesertTex, uv);
                float4 plainsColor = tex2D(_PlainsTex, uv);
                float4 forestColor = tex2D(_ForestTex, uv);
                float4 swampColor = tex2D(_SwampTex, uv);
                float4 mountainsColor = tex2D(_MountainsTex, uv);

                float4 color = lerp(desertColor, plainsColor, blend);
                color = lerp(color, forestColor, blend);
                color = lerp(color, swampColor, blend);
                color = lerp(color, mountainsColor, blend);

                return color;
            }

            float4 Frag(Attributes IN) : SV_Target
            {
                float4 mixedColor = BlendTextures(IN.uv0);

                return mixedColor;
            }
            ENDHLSL
        }
    }
    FallBack "HDRP/Unlit"
}
