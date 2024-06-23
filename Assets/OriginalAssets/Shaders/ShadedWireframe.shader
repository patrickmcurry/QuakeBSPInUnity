// ShadedWireframe.shader
// TODO: description
Shader "Custom/ShadedWireframe"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _WireframeColor ("Wireframe Color", Color) = (0, 0, 0, 1)
        _WireframeThickness ("Wireframe Thickness", Range(0.001, 0.1)) = 0.01
    }
 
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
                float3 normal : NORMAL;
            };
 
            struct v2f
            {
                float4 pos : POSITION;
                float3 normal : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };
 
            float4 _Color;
            float4 _WireframeColor;
            float _WireframeThickness;
 
            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }
 
            half4 frag (v2f i) : SV_Target
            {
                // Calculate wireframe
                float2 d = fwidth(i.screenPos.xy);
                float2 c = 0.5 - abs(i.screenPos.xy - 0.5);
                float2 visible = step(c, d);
                float wireframeFactor = min(visible.x, visible.y);
 
                // Calculate shading
                half3 shading = max(dot(i.normal, _WorldSpaceLightPos0.xyz), 0);
 
                // Combine wireframe and shading
                half4 finalColor = lerp(float4(shading, 1), _WireframeColor, wireframeFactor);
 
                return finalColor * _Color;
            }
            ENDCG
        }
    }
}
