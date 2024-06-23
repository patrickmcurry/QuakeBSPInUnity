// LightmapAsDiffuse.shader
// Renders the world using only the lightmap atlas as the diffuse texture
// AKA "lighting only" render of the world
Shader "Custom/LightmapAsDiffuse"
{
    Properties
    {
        _LightMap ("Diffuse Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD1;
            };
            
            struct v2f
            {
                float2 texcoord : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _LightMap;
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                // Sample the main texture and return it as the final color
                return tex2D(_LightMap, i.texcoord);
            }
            ENDCG
        }
    }
}
