Shader "Custom/DynamicGrassShader"
{
    Properties
    {
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _WindTex("Wind Texture (R)", 2D) = "gray" {}
        _GrassMask("Grass Mask (R)", 2D) = "white" {}
        _GrassHeight("Grass Height", Range(0.1, 2)) = 0.5
        _GrassWidth("Grass Width", Range(0.01, 0.2)) = 0.05
        _Density("Density", Range(0.1, 3)) = 1
        _RandomScale("Random Scale", Vector) = (0.3,0.5,0.2,0.1)
        _WindSpeed("Wind Speed", Float) = 2
        _WindStrength("Wind Strength", Float) = 0.5
        _WindFrequency("Wind Frequency", Float) = 4
        _Transparency("Transparency", Range(0,1)) = 0.8
        _ShadowIntensity("Shadow Intensity", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { 
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }
        LOD 200
        Cull Off

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"

        sampler2D _MainTex, _WindTex, _GrassMask;
        float4 _MainTex_ST;
        float _GrassHeight, _GrassWidth, _Density;
        float _WindSpeed, _WindStrength, _WindFrequency;
        float _Transparency, _ShadowIntensity;
        float4 _RandomScale;
        
        // Генератор случайных чисел
        float rand(float3 co)
        {
            return frac(sin(dot(co.xyz, float3(12.9898,78.233,45.543))) * 43758.5453);
        }
        ENDCG

        // Основной пасс (ForwardBase)
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 4.0

            // Структура входных данных
            struct appdata_base
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 pos : POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : NORMAL;
                UNITY_LIGHTING_COORDS(2,3)
            };

            v2g vert (appdata_base v)
            {
                v2g o;
                o.pos = v.vertex;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            [maxvertexcount(24)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                UNITY_INITIALIZE_OUTPUT(g2f, o);
                
                float mask = tex2Dlod(_GrassMask, float4(IN[0].uv, 0, 0)).r;
                if (mask < 0.1) return;
                
                float3 worldPos = IN[0].worldPos;
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 right = normalize(cross(viewDir, float3(0,1,0)));
                float3 up = float3(0,1,0);
                
                float randomHeight = _GrassHeight * (1 + rand(worldPos) * _RandomScale.x);
                float randomWidth = _GrassWidth * (1 + rand(worldPos*2) * _RandomScale.y);
                float bendFactor = rand(worldPos*3) * _RandomScale.z;
                
                float windTime = _Time.y * _WindSpeed;
                float2 windUV = worldPos.xz * _WindFrequency + windTime;
                float windSample = tex2Dlod(_WindTex, float4(windUV,0,0)).r * 2 - 1;
                float3 windForce = windSample * _WindStrength * bendFactor;
                
                int segments = 3;
                for (int i = 0; i < segments; i++)
                {
                    float t = i / (float)segments;
                    float tNext = (i+1) / (float)segments;
                    
                    float3 basePos = worldPos;
                    float height = t * randomHeight;
                    float heightNext = tNext * randomHeight;
                    
                    float3 bend = windForce * pow(t, 2);
                    float3 bendNext = windForce * pow(tNext, 2);
                    
                    float3 v0 = basePos + (right * -randomWidth) + up * height + bend;
                    float3 v1 = basePos + (right * randomWidth) + up * height + bend;
                    float3 v2 = basePos + (right * -randomWidth) + up * heightNext + bendNext;
                    float3 v3 = basePos + (right * randomWidth) + up * heightNext + bendNext;
                    
                    float3 normal = normalize(cross(v2 - v0, v1 - v0));
                    
                    // Треугольник 1
                    o.worldPos = v0;
                    o.pos = UnityWorldToClipPos(v0);
                    o.uv = float2(0, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v1;
                    o.pos = UnityWorldToClipPos(v1);
                    o.uv = float2(1, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v2;
                    o.pos = UnityWorldToClipPos(v2);
                    o.uv = float2(0, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    // Треугольник 2
                    o.worldPos = v2;
                    o.pos = UnityWorldToClipPos(v2);
                    o.uv = float2(0, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v1;
                    o.pos = UnityWorldToClipPos(v1);
                    o.uv = float2(1, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v3;
                    o.pos = UnityWorldToClipPos(v3);
                    o.uv = float2(1, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    triStream.RestartStrip();
                }
            }

            fixed4 frag (g2f i) : SV_Target
            {
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(i.normal, lightDir));
                float3 ambient = ShadeSH9(float4(i.normal,1));
                float3 diffuse = _LightColor0.rgb * NdotL;
                
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos)
                float3 lighting = lerp(ambient, diffuse, atten);
                
                fixed4 col = tex2D(_MainTex, i.uv);
                col.a = _Transparency * (1 - i.uv.y);
                clip(col.a - 0.3);
                
                col.rgb *= lighting;
                return col;
            }
            ENDCG
        }

        // Пасс для дополнительного света (ForwardAdd)
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma target 4.0

            // Структура входных данных
            struct appdata_base
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 pos : POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : NORMAL;
                UNITY_LIGHTING_COORDS(2,3)
            };

            v2g vert (appdata_base v)
            {
                v2g o;
                o.pos = v.vertex;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            [maxvertexcount(24)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                UNITY_INITIALIZE_OUTPUT(g2f, o);
                
                float mask = tex2Dlod(_GrassMask, float4(IN[0].uv,0,0)).r;
                if (mask < 0.1) return;
                
                float3 worldPos = IN[0].worldPos;
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 right = normalize(cross(viewDir, float3(0,1,0)));
                float3 up = float3(0,1,0);
                
                float randomHeight = _GrassHeight * (1 + rand(worldPos) * _RandomScale.x);
                float randomWidth = _GrassWidth * (1 + rand(worldPos*2) * _RandomScale.y);
                float bendFactor = rand(worldPos*3) * _RandomScale.z;
                
                float windTime = _Time.y * _WindSpeed;
                float2 windUV = worldPos.xz * _WindFrequency + windTime;
                float windSample = tex2Dlod(_WindTex, float4(windUV,0,0)).r * 2 - 1;
                float3 windForce = windSample * _WindStrength * bendFactor;
                
                int segments = 3;
                for (int i = 0; i < segments; i++)
                {
                    float t = i / (float)segments;
                    float tNext = (i+1) / (float)segments;
                    
                    float3 basePos = worldPos;
                    float height = t * randomHeight;
                    float heightNext = tNext * randomHeight;
                    
                    float3 bend = windForce * pow(t, 2);
                    float3 bendNext = windForce * pow(tNext, 2);
                    
                    float3 v0 = basePos + (right * -randomWidth) + up * height + bend;
                    float3 v1 = basePos + (right * randomWidth) + up * height + bend;
                    float3 v2 = basePos + (right * -randomWidth) + up * heightNext + bendNext;
                    float3 v3 = basePos + (right * randomWidth) + up * heightNext + bendNext;
                    
                    float3 normal = normalize(cross(v2 - v0, v1 - v0));
                    
                    o.worldPos = v0;
                    o.pos = UnityWorldToClipPos(v0);
                    o.uv = float2(0, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v1;
                    o.pos = UnityWorldToClipPos(v1);
                    o.uv = float2(1, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v2;
                    o.pos = UnityWorldToClipPos(v2);
                    o.uv = float2(0, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v2;
                    o.pos = UnityWorldToClipPos(v2);
                    o.uv = float2(0, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v1;
                    o.pos = UnityWorldToClipPos(v1);
                    o.uv = float2(1, t);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    o.worldPos = v3;
                    o.pos = UnityWorldToClipPos(v3);
                    o.uv = float2(1, tNext);
                    o.normal = normal;
                    UNITY_TRANSFER_LIGHTING(o, o.pos);
                    triStream.Append(o);
                    
                    triStream.RestartStrip();
                }
            }

            fixed4 frag (g2f i) : SV_Target
            {
                float3 lightDir;
                if (_WorldSpaceLightPos0.w == 0.0)
                    lightDir = normalize(_WorldSpaceLightPos0.xyz);
                else
                    lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                
                float NdotL = saturate(dot(i.normal, lightDir));
                float3 diffuse = _LightColor0.rgb * NdotL;
                
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos)
                diffuse *= atten;
                
                fixed4 col = tex2D(_MainTex, i.uv);
                col.a = _Transparency * (1 - i.uv.y);
                clip(col.a - 0.3);
                
                col.rgb *= diffuse;
                return col;
            }
            ENDCG
        }

        // Пасс для теней (ShadowCaster)
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma target 3.0

            // Структура входных данных
            struct appdata_base
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_SHADOW_CASTER_NORMAL(o)
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float mask = tex2D(_GrassMask, i.uv).r;
                clip(mask - 0.1);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}