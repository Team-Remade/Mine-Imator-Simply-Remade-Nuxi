using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MineImatorSimplyRemadeNuxi;

public class Camera
{
    private Vector3 _position;
    private float _pitch;
    private float _yaw;

    public Vector3 Position
    {
        get => _position;
        set { _position = value; UpdateViewMatrix(); }
    }
    public float Pitch
    {
        get => _pitch;
        set { _pitch = value; UpdateViewMatrix(); }
    }
    public float Yaw
    {
        get => _yaw;
        set { _yaw = value; UpdateViewMatrix(); }
    }
    public Matrix Projection { get; private set; }
    public Matrix View { get; private set; }
    public Matrix World { get; private set; }

    public Vector3 Forward => Vector3.Transform(Vector3.Forward, RotationMatrix);
    public Vector3 Backward => Vector3.Transform(Vector3.Backward, RotationMatrix);
    public Vector3 Left => Vector3.Transform(Vector3.Left, RotationMatrix);
    public Vector3 Right => Vector3.Transform(Vector3.Right, RotationMatrix);
    public Vector3 Up => Vector3.Transform(Vector3.Up, RotationMatrix);
    public Vector3 Down => Vector3.Transform(Vector3.Down, RotationMatrix);

    public Matrix RotationMatrix => Matrix.CreateFromYawPitchRoll(_yaw, _pitch, 0);

    public Camera()
    {
        _position = new Vector3(0, 0, -50);
        _pitch = 0;
        _yaw = MathHelper.Pi;
    }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45),
            graphicsDevice.Viewport.AspectRatio,
            0.1f,
            1000.0f);
        UpdateViewMatrix();
    }

    public void ApplyToEffect(BasicEffect effect)
    {
        effect.Projection = Projection;
        effect.View = View;
        effect.World = World;
    }

    public void UpdateProjectionMatrix(float aspectRatio)
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45), aspectRatio, 0.1f, 1000.0f);
    }

    private void UpdateViewMatrix()
    {
        View = Matrix.CreateLookAt(_position, _position + Forward, Vector3.Up);
        World = Matrix.Identity;
    }

    public void MoveForward(float distance) => Position += Forward * distance;
    public void MoveBackward(float distance) => Position += Backward * distance;
    public void MoveLeft(float distance) => Position += Left * distance;
    public void MoveRight(float distance) => Position += Right * distance;
    public void MoveUp(float distance) => Position += Up * distance;
    public void MoveDown(float distance) => Position += Down * distance;

    public void LookLeft(float radians) => Yaw -= radians;
    public void LookRight(float radians) => Yaw += radians;
    public void LookUp(float radians) => Pitch -= radians;
    public void LookDown(float radians) => Pitch += radians;

    public void Update(float deltaTime, int deltaX, int deltaY, bool isActive)
    {
        if (isActive)
        {
            const float sensitivity = 0.01f;
            Yaw -= deltaX * sensitivity;
            Pitch -= deltaY * sensitivity;
            Pitch = MathHelper.Clamp(Pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
        }

        float speed = 100f * deltaTime;
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.W)) MoveForward(speed);
        if (keyboard.IsKeyDown(Keys.S)) MoveBackward(speed);
        if (keyboard.IsKeyDown(Keys.A)) MoveLeft(speed);
        if (keyboard.IsKeyDown(Keys.D)) MoveRight(speed);
        if (keyboard.IsKeyDown(Keys.Space)) MoveUp(speed);
        if (keyboard.IsKeyDown(Keys.LeftShift)) MoveDown(speed);
    }
}
