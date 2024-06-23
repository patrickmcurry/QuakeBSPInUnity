// DepthMask.shader
// "Renders" polygons as if they are empty, letting the skybox shine through, kinda portal-ish
// From: https://answers.unity.com/questions/680696/shader-that-only-renders-the-scenes-background.html
Shader "Custom/DepthMask" {
    SubShader {
		Tags {"Queue" = "Geometry-10" }       
		Lighting Off
		ZTest LEqual
		ZWrite On
		ColorMask 0
		Pass {}
    }
}
