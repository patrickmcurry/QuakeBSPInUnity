// White.shader
Shader "Custom/White"
{
    SubShader
    {
        Tags { "Queue"="Transparent" }
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
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
            };
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            half4 frag () : SV_Target
            {
                // Output a fixed white color (1, 1, 1, 1)
                return half4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
