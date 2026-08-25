Shader "AtlasBoard/Stylized Water BuiltIn"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.05, 0.70, 0.82, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.22, 0.42, 1)

        _Opacity ("Opacity", Range(0.2, 1.0)) = 0.82

        _FresnelStrength ("Fresnel Strength", Range(0.0, 1.5)) = 0.55
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.5

        _HighlightStrength ("Highlight Strength", Range(0.0, 1.0)) = 0.18
        _HighlightScale ("Highlight Scale", Range(0.1, 10.0)) = 2.2

        _ColorVariation ("Color Variation", Range(0.0, 1.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _ShallowColor;
            fixed4 _DeepColor;

            float _Opacity;

            float _FresnelStrength;
            float _FresnelPower;

            float _HighlightStrength;
            float _HighlightScale;

            float _ColorVariation;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 world =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex);

                o.pos =
                    UnityObjectToClipPos(
                        v.vertex);

                o.worldPos =
                    world.xyz;

                o.worldNormal =
                    UnityObjectToWorldNormal(
                        v.normal);

                o.viewDir =
                    normalize(
                        _WorldSpaceCameraPos -
                        world.xyz);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n =
                    normalize(
                        i.worldNormal);

                float3 v =
                    normalize(
                        i.viewDir);

                float ndv =
                    saturate(
                        abs(
                            dot(n, v)));

                float fresnel =
                    pow(
                        1.0 - ndv,
                        _FresnelPower) *
                    _FresnelStrength;

                float staticPattern =
                    sin(
                        (i.worldPos.x +
                         i.worldPos.z) *
                        0.55) *
                    0.5 +
                    0.5;

                float blendValue =
                    saturate(
                        0.35 +
                        staticPattern *
                        _ColorVariation +
                        fresnel);

                fixed3 waterColor =
                    lerp(
                        _DeepColor.rgb,
                        _ShallowColor.rgb,
                        blendValue);

                float highlightPattern =
                    pow(
                        saturate(
                            sin(
                                (i.worldPos.x * 0.8 -
                                 i.worldPos.z * 0.65) *
                                _HighlightScale) *
                            0.5 +
                            0.5),
                        7.0);

                waterColor +=
                    highlightPattern *
                    _HighlightStrength;

                return fixed4(
                    saturate(waterColor),
                    _Opacity);
            }

            ENDCG
        }
    }

    FallBack Off
}
