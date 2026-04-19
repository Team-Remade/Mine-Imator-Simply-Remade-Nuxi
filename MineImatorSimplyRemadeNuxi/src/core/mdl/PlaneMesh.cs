using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi.core.mdl;

public enum PlaneOrientation
{
    XY,
    XZ
}

public class PlaneMesh : Mesh
{
    public float Width { get; set; }
    public float Height { get; set; }
    public PlaneOrientation Orientation { get; set; }

    public PlaneMesh(float width, float height, PlaneOrientation orientation, GraphicsDevice graphicsDevice)
    {
        Width = width;
        Height = height;
        Orientation = orientation;
        Color = Color.White;
        InitializeGraphicsDevice(graphicsDevice);
        GenerateVertices();
        GenerateVertexBuffer();
        CullMode = CullMode.None;
    }

    protected override void GenerateVertices()
    {
        float halfWidth = Width / 2f;
        float halfHeight = Height / 2f;

        Vector3[] positions;
        if (Orientation == PlaneOrientation.XY)
        {
            positions = new Vector3[]
            {
                new Vector3(halfWidth, halfHeight, 0),
                new Vector3(halfWidth, -halfHeight, 0),
                new Vector3(-halfWidth, -halfHeight, 0),
                new Vector3(-halfWidth, halfHeight, 0)
            };
        }
        else
        {
            positions = new Vector3[]
            {
                new Vector3(halfWidth, 0, halfHeight),
                new Vector3(halfWidth, 0, -halfHeight),
                new Vector3(-halfWidth, 0, -halfHeight),
                new Vector3(-halfWidth, 0, halfHeight)
            };
        }

        Vector2 uv0, uv1, uv2, uv3;
        if (Orientation == PlaneOrientation.XY)
        {
            uv0 = new Vector2(Width, Height);
            uv1 = new Vector2(Width, 0);
            uv2 = new Vector2(0, 0);
            uv3 = new Vector2(0, Height);
        }
        else
        {
            uv0 = new Vector2(Width, Height);
            uv1 = new Vector2(Width, 0);
            uv2 = new Vector2(0, 0);
            uv3 = new Vector2(0, Height);
        }

        Vertices = new VertexPositionColorTexture[]
        {
            new VertexPositionColorTexture(positions[0], Color, uv0),
            new VertexPositionColorTexture(positions[1], Color, uv1),
            new VertexPositionColorTexture(positions[2], Color, uv2),
            new VertexPositionColorTexture(positions[0], Color, uv0),
            new VertexPositionColorTexture(positions[2], Color, uv2),
            new VertexPositionColorTexture(positions[3], Color, uv3)
        };
    }

    public void UpdateColor(Color color)
    {
        Color = color;
        GenerateVertices();
        GenerateVertexBuffer();
    }
}