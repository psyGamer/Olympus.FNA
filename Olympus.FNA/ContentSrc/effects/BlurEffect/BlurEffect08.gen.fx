// Autogenerated from BlurEffectTemplate.tt
// Radius: 8 / Kernel Size: 17
float4x4 Transform;
float4 Color;
float4 OffsetsWeights[17];
float4 MinMax;


texture2D Tex0;
sampler Tex0Sampler = sampler_state {
    Texture = Tex0;
};


void GetVertex(
    inout float4 position : SV_Position,
    inout float2 texCoord : TEXCOORD0,
    inout float4 color    : COLOR0
) {
    position = mul(position, Transform);
}


float4 GetPixel(
    float2 texCoord : TEXCOORD0,
    float4 color : COLOR0
) : SV_Target0 {
    float4 c = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float4 offsetWeight;
    /* unrolled for 0 <= i < kernelSize */ {
        offsetWeight = OffsetsWeights[0];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[1];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[2];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[3];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[4];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[5];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[6];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[7];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[8];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[9];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[10];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[11];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[12];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[13];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[14];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[15];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
        offsetWeight = OffsetsWeights[16];
        c += tex2D(Tex0Sampler, clamp(texCoord + offsetWeight.xy, MinMax.xy, MinMax.zw)) * offsetWeight.z;
    }
    return c * color * Color;
}


technique Main
{
    pass
    {
        Sampler[0] = Tex0Sampler;
        VertexShader = compile vs_3_0 GetVertex();
        PixelShader = compile ps_3_0 GetPixel();
    }
}

