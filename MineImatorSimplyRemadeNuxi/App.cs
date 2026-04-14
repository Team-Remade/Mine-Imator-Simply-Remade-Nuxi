using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;

namespace MineImatorSimplyRemadeNuxi;

public class App : Game
{
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_MaximizeWindow(IntPtr window);
    
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    public static ImGuiRenderer GuiRenderer;
    
    Camera camera;
    
    BasicEffect basicEffect;
    VertexPositionTexture[] triangleVertices;
    VertexPositionColor[] coloredTriangleVertices;
    VertexBuffer vertexBuffer;
    VertexBuffer coloredVertexBuffer;
    RenderTarget2D renderTarget;
    Texture2D whiteTexture;
    IntPtr textureHandle;

    private MouseState _lastMouseState;
    private bool _isActive;

    public App()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.Title = "Mine Imator Simply Remade: Nuxi";
        
        GuiRenderer = new ImGuiRenderer(this);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        
        camera = new Camera();
        camera.Initialize(GraphicsDevice);
        
        basicEffect = new BasicEffect(GraphicsDevice);
        basicEffect.TextureEnabled = true;

        triangleVertices =
        [
            new VertexPositionTexture(new Vector3(0, 20, 0), new Vector2(0.5f, 0)),
            new VertexPositionTexture(new Vector3(-20, -20, 0), new Vector2(0, 1)),
            new VertexPositionTexture(new Vector3(20, -20, 0), new Vector2(1, 1))
        ];
        
        vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionTexture), triangleVertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(triangleVertices);
        
        renderTarget = new RenderTarget2D(GraphicsDevice, 512, 512);
        whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        whiteTexture.SetData([Color.White]);
        basicEffect.Texture = whiteTexture;
        
        coloredTriangleVertices =
        [
            new VertexPositionColor(new Vector3(0, 20, 0), Color.Red),
            new VertexPositionColor(new Vector3(-20, -20, 0), Color.Green),
            new VertexPositionColor(new Vector3(20, -20, 0), Color.Blue)
        ];
        
        coloredVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), coloredTriangleVertices.Length, BufferUsage.WriteOnly);
        coloredVertexBuffer.SetData(coloredTriangleVertices);
        
        Window.AllowUserResizing = true;
        
        _graphics.PreferredBackBufferWidth = 1920;
        _graphics.PreferredBackBufferHeight = 1080;
        _graphics.ApplyChanges();
        
        SDL_MaximizeWindow(Window.Handle);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        
        GuiRenderer.RebuildFontAtlas();
        
        textureHandle = GuiRenderer.BindTexture(renderTarget);
    }

    protected override void Update(GameTime gameTime)
    {
        MouseState currentMouse = Mouse.GetState();

        if (currentMouse.RightButton == ButtonState.Pressed)
        {
            if (!_isActive)
            {
                _isActive = true;
                IsMouseVisible = false;
                Mouse.SetPosition(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
                _lastMouseState = Mouse.GetState();
            }

            MouseState currentCentered = Mouse.GetState();
            int deltaX = currentCentered.X - _lastMouseState.X;
            int deltaY = currentCentered.Y - _lastMouseState.Y;

            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, deltaX, deltaY, true);

            Mouse.SetPosition(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
            _lastMouseState = Mouse.GetState();
        }
        else
        {
            if (_isActive)
            {
                _isActive = false;
                IsMouseVisible = true;
            }
            camera.Update((float)gameTime.ElapsedGameTime.TotalSeconds, 0, 0, false);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        RasterizerState rasterizerState = new RasterizerState();
        
        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.Clear(Color.Black);
        GraphicsDevice.SetVertexBuffer(coloredVertexBuffer);
        GraphicsDevice.RasterizerState = rasterizerState;
        
        basicEffect.VertexColorEnabled = true;
        basicEffect.TextureEnabled = false;
        camera.ApplyToEffect(basicEffect);
        foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 3);
        }
        
        GraphicsDevice.SetRenderTarget(null);
        
        textureHandle = GuiRenderer.BindTexture(renderTarget);
        
        GraphicsDevice.Clear(Color.Black);

        base.Draw(gameTime);
        
        GuiRenderer.BeginLayout(gameTime);

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());
        
        ImGui.Begin("Viewport");

        var size = ImGui.GetContentRegionAvail();
        
        camera.UpdateProjectionMatrix(size.X / size.Y);
        
        ImGui.Image(textureHandle, size);
        
        ImGui.End();
        
        GuiRenderer.EndLayout();
    }
}
