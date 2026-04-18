using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MineImatorSimplyRemadeNuxi.core.mdl.materials;

namespace MineImatorSimplyRemadeNuxi.core.mdl;

public abstract class Mesh
{
    protected VertexPositionColorTexture[] Vertices { get; set; }
    protected VertexBuffer VertexBuffer { get; private set; }
    protected GraphicsDevice GraphicsDevice { get; private set; }
    protected Color Color { get; set; } = Color.White;
    
    public CullMode CullMode { get; set; } = CullMode.CullClockwiseFace;

    protected void InitializeGraphicsDevice(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
    }

    protected void GenerateVertexBuffer()
    {
        if (Vertices == null || GraphicsDevice == null) return;

        VertexBuffer?.Dispose();
        VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColorTexture), Vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(Vertices);
    }

    protected abstract void GenerateVertices();

    public void Render(GraphicsDevice graphicsDevice, BasicEffect effect)
    {
        if (VertexBuffer == null) return;

        graphicsDevice.SetVertexBuffer(VertexBuffer);
        graphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode };

        effect.VertexColorEnabled = false;
        effect.TextureEnabled = true;

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, Vertices.Length / 3);
        }
    }
    
    public int GetSurfaceCount()
    {
        return 1;
    }

    public Material SurfaceGetMaterial(int surfaceIndex)
    {
        return new Material();
    }
}