Shader "Motion/Player" {
Properties {
	[Header(Texture)]
	[NoScaleOffset]
	_MainTex ("Albedo", 2D) = "white" {}
	_Color ("Color", Color) = (1,1,1,1)

	[Header(Clipping)]
	[Enum(UnityEngine.Rendering.CullMode)] _Cull("Face Culling", Float) = 2
	[Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha Test", Float) = 0
	_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
	_NearClip ("NearClip", Float) = 0

	[Header(Motion)]
	[NoScaleOffset]_Armature ("Armature", 2D) = "black" {}
	[Toggle(_ALPHAPREMULTIPLY_ON)] _Decoded ("Decoded", Float) = 0
	_Id ("Id", Float) = 0
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
CGPROGRAM
#pragma exclude_renderers gles
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature _ALPHATEST_ON
#pragma multi_compile _ _ALPHAPREMULTIPLY_ON
#pragma multi_compile_instancing
#include <UnityCG.cginc>
#include <Lighting.cginc>
#include "MotionPlayer.hlsl"
#include "Frag.hlsl"

void vert(VertInputPlayer i, out FragInput o) {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	
	float3 vertex, normal;
	SkinVertex(i, vertex, normal);
	
	o.vertex = mul(unity_ObjectToWorld, float4(vertex, 1));
	o.normal = mul(unity_ObjectToWorld, float4(normal, 0));
	o.pos = UnityWorldToClipPos(o.vertex);
	o.tex = i.GetUV();
	o.color = _Color;
}
ENDCG
	}
}
}