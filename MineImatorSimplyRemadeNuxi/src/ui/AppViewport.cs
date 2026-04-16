using System;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi.ui;

public class Viewport
{
    public Camera camera;
    GraphicsDevice graphicsDevice;
    BasicEffect basicEffect;
    RenderTarget2D renderTarget;
    IntPtr textureHandle;
    Texture2D whiteTexture;
    VertexPositionColor[] coloredTriangleVertices;
    VertexBuffer coloredVertexBuffer;
    
    public Viewport(Camera camera, GraphicsDevice graphicsDevice)
    {
        this.camera = camera;
        this.graphicsDevice = graphicsDevice;
        
        basicEffect = new BasicEffect(graphicsDevice);
        basicEffect.TextureEnabled = true;
        
        coloredTriangleVertices =
        [
            new VertexPositionColor(new Vector3(0, 20, 0), Color.Red),
            new VertexPositionColor(new Vector3(-20, -20, 0), Color.Green),
            new VertexPositionColor(new Vector3(20, -20, 0), Color.Blue)
        ];
        
        coloredVertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), coloredTriangleVertices.Length, BufferUsage.WriteOnly);
        coloredVertexBuffer.SetData(coloredTriangleVertices);
        
        renderTarget = new RenderTarget2D(graphicsDevice, 512, 512);
        
        whiteTexture = new Texture2D(graphicsDevice, 1, 1);
        whiteTexture.SetData([Color.White]);
        basicEffect.Texture = whiteTexture;
        
        textureHandle = App.GuiRenderer.BindTexture(renderTarget);
    }
    
    public void Render()
    {
        RasterizerState rasterizerState = new RasterizerState();
        
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(Color.Black);
        graphicsDevice.SetVertexBuffer(coloredVertexBuffer);
        graphicsDevice.RasterizerState = rasterizerState;
        
        basicEffect.VertexColorEnabled = true;
        basicEffect.TextureEnabled = false;
        camera.ApplyToEffect(basicEffect);
        foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 3);
        }
        
        graphicsDevice.SetRenderTarget(null);
        
        textureHandle = App.GuiRenderer.BindTexture(renderTarget);
        
        ImGui.Begin("Viewport");

        var size = ImGui.GetContentRegionAvail();
        
        camera.UpdateProjectionMatrix(size.X / size.Y);
        
        ImGui.Image(textureHandle, size);
        
        ImGui.End();
    }
}