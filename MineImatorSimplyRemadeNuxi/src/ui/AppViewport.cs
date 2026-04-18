using System;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MineImatorSimplyRemadeNuxi.core.mdl;
using MineImatorSimplyRemadeNuxi.core.objs;
using MineImatorSimplyRemadeNuxi.core.objs.nodes;

namespace MineImatorSimplyRemadeNuxi.ui;

public class AppViewport
{
    public WorkCamera camera;
    GraphicsDevice graphicsDevice;
    BasicEffect basicEffect;
    RenderTarget2D renderTarget;
    ImTextureRef textureHandle;
    Texture2D whiteTexture;
    Texture2D _benchTexture;
    ImTextureRef _benchTextureHandle;
    core.mdl.Plane xzPlane;
    VertexBuffer coloredVertexBuffer;
    
    private MouseState _lastMouseState;
    private int _lastScrollWheelValue;
    private bool _isActive;
    private Vector2 _imageMin;
    private Vector2 _imageMax;
    
    public AppViewport(WorkCamera camera, GraphicsDevice graphicsDevice)
    {
        this.camera = camera;
        this.graphicsDevice = graphicsDevice;
        
        basicEffect = new BasicEffect(graphicsDevice);
        basicEffect.TextureEnabled = true;
        
        xzPlane = new core.mdl.Plane(64f, 64f, PlaneOrientation.XZ, graphicsDevice);
        coloredVertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), 0, BufferUsage.WriteOnly);
        
        renderTarget = new RenderTarget2D(graphicsDevice, 512, 512);
        
        whiteTexture = new Texture2D(graphicsDevice, 1, 1);
        whiteTexture.SetData([Color.White]);
        basicEffect.Texture = whiteTexture;
        
        textureHandle = App.GuiRenderer.BindTexture(renderTarget);
        
        _benchTexture = Program.App.Content.Load<Texture2D>("assets/img/bench");
        _benchTextureHandle = App.GuiRenderer.BindTexture(_benchTexture);
    }

    public void LoadTerrainTexture()
    {
        if (TerrainAtlas.Textures.TryGetValue("8,2", out var terrainTexture))
        {
            basicEffect.Texture = terrainTexture;
        }
    }

    private bool IsMouseOverImage()
    {
        Vector2 mousePos = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
        return mousePos.X >= _imageMin.X && mousePos.X <= _imageMax.X &&
               mousePos.Y >= _imageMin.Y && mousePos.Y <= _imageMax.Y;
    }

    public void Update(GameTime gameTime)
    {
        MouseState currentMouse = Mouse.GetState();

        if (currentMouse.RightButton == ButtonState.Pressed && IsMouseOverImage())
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
            int deltaWheel = currentCentered.ScrollWheelValue - _lastScrollWheelValue;

            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, deltaX, deltaY, deltaWheel, true);

            Mouse.SetPosition(Program.App.Window.ClientBounds.Width / 2, Program.App.Window.ClientBounds.Height / 2);
            _lastMouseState = Mouse.GetState();
            _lastScrollWheelValue = _lastMouseState.ScrollWheelValue;
        }
        else
        {
            if (_isActive)
            {
                _isActive = false;
                Program.App.IsMouseVisible = true;
            }
            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, 0, 0, 0, false);
        }
        
        if (_isActive && !IsMouseOverImage() && currentMouse.RightButton == ButtonState.Released)
        {
            _isActive = false;
            Program.App.IsMouseVisible = true;
        }
    }

    public SceneObject[] GetChildren()
    {
        return [];
    }

    public Node GetNodeOrNull<t>(string id)
    {
        return null;
    }
    
    public void Render()
    {
        RasterizerState rasterizerState = new RasterizerState();
        
        Color bgColor = new Color(Program.App.Properties.BackgroundColor[0], Program.App.Properties.BackgroundColor[1], Program.App.Properties.BackgroundColor[2]);
        
        ImGui.Begin("Viewport");

        var size = ImGui.GetContentRegionAvail();

        if ((renderTarget.Width != (int)MathF.Floor(size.X) || renderTarget.Height != (int)MathF.Floor(size.Y)) && size.X > 0 && size.Y > 0)
        {
            renderTarget.Dispose();
            renderTarget = new RenderTarget2D(graphicsDevice, (int)size.X, (int)size.Y);
            textureHandle = App.GuiRenderer.BindTexture(renderTarget);
        }

        if (size.X > 0 && size.Y > 0)
            camera.UpdateProjectionMatrix(45, size.X / size.Y);

        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(bgColor);
        graphicsDevice.RasterizerState = rasterizerState;
        graphicsDevice.SamplerStates[0] = new SamplerState
        {
            Filter = TextureFilter.Point,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap
        };
        camera.ApplyToEffect(basicEffect);
        xzPlane.Render(graphicsDevice, basicEffect);
        
        graphicsDevice.SetRenderTarget(null);
        
        ImGui.Image(textureHandle, size);
        _imageMin = ImGui.GetItemRectMin();
        _imageMax = ImGui.GetItemRectMax();
        
        // Texture button overlaid at the top-left corner of the viewport image
        float padding = 8f;
        ImGui.SetCursorPos(new System.Numerics.Vector2(padding, ImGui.GetFrameHeight() + padding));
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0, 0, 0, 0));
        ImGui.ImageButton("##benchBtn", _benchTextureHandle, new System.Numerics.Vector2(64, 64));
        ImGui.PopStyleColor(3);
        
        ImGui.End();
    }
}