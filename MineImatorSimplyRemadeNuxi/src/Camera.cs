using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MineImatorSimplyRemadeNuxi;

public class Camera
{
    public Vector3 Target { get; set; }
    public Vector3 Position { get; set; }
    public Matrix Projection { get; private set; }
    public Matrix View { get; private set; }
    public Matrix World { get; private set; }

    public Camera()
    {
        Target = new Vector3(0, 0, 0);
        Position = new Vector3(0, 0, -100);
    }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45),
            graphicsDevice.Viewport.AspectRatio,
            0.1f,
            1000.0f);
        View = Matrix.CreateLookAt(Position, Target, Vector3.Up);
        World = Matrix.CreateWorld(Target, Vector3.Forward, Vector3.Up);
    }

    public void ApplyToEffect(BasicEffect effect)
    {
        effect.Projection = Projection;
        effect.View = View;
        effect.World = World;
    }
}
