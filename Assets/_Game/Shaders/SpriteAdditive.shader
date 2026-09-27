// An additive sprite, for light rather than paint: beams, flares and anything else that should brighten
// what is behind it instead of covering it.
//
// WHY THIS EXISTS RATHER THAN A PARTICLE MATERIAL
// The obvious shortcut is to point a SpriteRenderer at URP's Particles/Unlit shader, which is already
// additive and already in this project. It draws, and the sprite's texture even binds, so it looks like it
// works. It is not: that shader declares no _RendererColor and reads no unity_SpriteColor, which is how a
// SpriteRenderer hands over its `color`. Every tint, every brightness curve and the whole build of the
// beam's warning were computed correctly and then thrown away one step before the pixel. The beam rendered
// flat white and the telegraph went from invisible to full instantly, which is a gameplay change, not a
// cosmetic one. Nothing in the test suite could see it, because the arithmetic was right.
//
// So this is URP's own 2D sprite shader with one line changed: the blend. Keeping the rest verbatim is the
// point, because it is what keeps sprite colour, flipping, skinning, instancing and the SRP batcher working
// the way every other sprite in the game already does.
Shader "YASS/Sprite Additive"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        // Legacy properties, kept so a material using this can fall back to the legacy sprite shader, and
        // because _RendererColor is the one this whole file exists for.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // The one line that differs from URP's Sprite-Unlit-Default. Alpha is how much light to add, so a
        // dim layer adds a little and a bright one adds a lot, and nothing is ever darkened.
        Blend SrcAlpha One

        Cull Off

        // Never. Additive layers are drawn over each other on purpose, and depth writes would have them
        // cut each other out.
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);

                // unity_SpriteColor is the SpriteRenderer's own colour. This is the line the particle
                // shader had no equivalent of.
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
