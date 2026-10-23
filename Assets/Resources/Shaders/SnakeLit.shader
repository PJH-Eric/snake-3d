// 3D貪吃蛇用的簡單受光材質：Lambert 光照 + 貼圖 + 自發光，支援 GPU Instancing。
// 放在 Resources 底下，打包時一定會被包進去；若這支編譯失敗會退回 Legacy Shaders/Diffuse。
Shader "Snake3D/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emis ("Emission", Color) = (0,0,0,0)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert addshadow fullforwardshadows
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _Emis;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Emission = _Emis.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Diffuse"
}
