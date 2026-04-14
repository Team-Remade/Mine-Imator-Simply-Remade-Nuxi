using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MineImatorSimplyRemadeNuxi;

public class App : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    
    Camera camera;
    
    BasicEffect basicEffect;
    VertexPositionColor[] triangleVertices;
    VertexBuffer vertexBuffer;

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
        camera = new Camera();
        camera.Initialize(GraphicsDevice);
        
        basicEffect = new BasicEffect(GraphicsDevice);
        basicEffect.VertexColorEnabled = true;
        basicEffect.LightingEnabled = false;

        triangleVertices =
        [
            new VertexPositionColor(new Vector3(0, 20, 0), Color.Red),
            new VertexPositionColor(new Vector3(-20, -20, 0), Color.Green),
            new VertexPositionColor(new Vector3(20, -20, 0), Color.Blue)
        ];
        
        vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), triangleVertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(triangleVertices);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
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
        camera.ApplyToEffect(basicEffect);
        
        GraphicsDevice.Clear(Color.CornflowerBlue);
        
        GraphicsDevice.SetVertexBuffer(vertexBuffer);

        RasterizerState rasterizerState = new RasterizerState();
        rasterizerState.CullMode = CullMode.None;
        GraphicsDevice.RasterizerState = rasterizerState;

        foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 3);
        }

        base.Draw(gameTime);
    }
}
