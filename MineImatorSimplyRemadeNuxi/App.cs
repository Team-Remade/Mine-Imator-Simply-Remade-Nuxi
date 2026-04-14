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
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

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
