// PickShader.fx
// Renders a solid pick colour for object-ID picking.
// The pick_color parameter is set per-object from SceneObject.PickColor.

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 pick_color;

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
};

VertexShaderOutput VS(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition  = mul(worldPosition, View);
    output.Position      = mul(viewPosition, Projection);
    return output;
}

float4 PS(VertexShaderOutput input) : SV_TARGET
{
    return float4(pick_color, 1.0);
}

technique PickTechnique
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
