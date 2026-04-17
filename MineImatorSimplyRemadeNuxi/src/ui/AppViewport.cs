using System;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MineImatorSimplyRemadeNuxi.ui;

public class AppViewport
{
    public Camera camera;
    GraphicsDevice graphicsDevice;
    BasicEffect basicEffect;
    RenderTarget2D renderTarget;
    IntPtr textureHandle;
    Texture2D whiteTexture;
    VertexPositionColor[] coloredTriangleVertices;
    VertexBuffer coloredVertexBuffer;
    
    private MouseState _lastMouseState;
    private bool _isActive;
    
    public AppViewport(Camera camera, GraphicsDevice graphicsDevice)
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

    public void Update(GameTime gameTime)
    {
        MouseState currentMouse = Mouse.GetState();

        if (currentMouse.RightButton == ButtonState.Pressed)
        {
            if (!_isActive)
            {
                _isActive = true;
                Program.App.IsMouseVisible = false;
                Mouse.SetPosition(Program.App.Window.ClientBounds.Width / 2, Program.App.Window.ClientBounds.Height / 2);
                _lastMouseState = Mouse.GetState();
            }

            MouseState currentCentered = Mouse.GetState();
            int deltaX = currentCentered.X - _lastMouseState.X;
            int deltaY = currentCentered.Y - _lastMouseState.Y;

            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, deltaX, deltaY, true);

            Mouse.SetPosition(Program.App.Window.ClientBounds.Width / 2, Program.App.Window.ClientBounds.Height / 2);
            _lastMouseState = Mouse.GetState();
        }
        else
        {
            if (_isActive)
            {
                _isActive = false;
                Program.App.IsMouseVisible = true;
            }
            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, 0, 0, false);
        }
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

        if ((renderTarget.Width != (int)MathF.Floor(size.X) || renderTarget.Height != (int)MathF.Floor(size.Y)) && size.X > 0 && size.Y > 0)
        {
            renderTarget.Dispose();
            renderTarget = new RenderTarget2D(graphicsDevice, (int)size.X, (int)size.Y);
        }
        
        camera.UpdateProjectionMatrix(size.X / size.Y);
        
        ImGui.Image(textureHandle, size);
        
        ImGui.End();
    }
}