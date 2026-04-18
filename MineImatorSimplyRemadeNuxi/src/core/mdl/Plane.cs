using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi.core.mdl;

public enum PlaneOrientation
{
    XY,
    XZ
}

public class Plane : Mesh
{
    public float Width { get; set; }
    public float Height { get; set; }
    public PlaneOrientation Orientation { get; set; }

    public Plane(float width, float height, PlaneOrientation orientation, GraphicsDevice graphicsDevice)
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

        Vertices = new VertexPositionColor[]
        {
            new VertexPositionColor(positions[0], Color),
            new VertexPositionColor(positions[1], Color),
            new VertexPositionColor(positions[2], Color),
            new VertexPositionColor(positions[0], Color),
            new VertexPositionColor(positions[2], Color),
            new VertexPositionColor(positions[3], Color)
        };
    }

    public void UpdateColor(Color color)
    {
        Color = color;
        GenerateVertices();
        GenerateVertexBuffer();
    }
}