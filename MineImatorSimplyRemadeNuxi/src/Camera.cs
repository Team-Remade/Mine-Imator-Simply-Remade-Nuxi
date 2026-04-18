using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MineImatorSimplyRemadeNuxi;

public class Camera
{
    private Vector3 _position;
    private float _pitch;
    private float _yaw;
    private float _baseSpeed = 20;
    private float _speedMultiplier = 1;

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
        Reset();
    }

    private void Reset()
    {
        _position = new Vector3(4.005625f, 3.64125f, 4.005625f);

        // Orient toward the initial look-at target (0, 1, 0)
        var initialTarget = new Vector3(0f, 1f, 0f);
        var dir = Vector3.Normalize(initialTarget - _position);
        _pitch = (float)Math.Asin(dir.Y);
        _yaw   = (float)Math.Atan2(-dir.X, -dir.Z);
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

    public void UpdateProjectionMatrix(float fov, float aspectRatio)
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(fov), aspectRatio, 0.1f, 1000.0f);
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
    public void MoveUp(float distance) => Position += Vector3.Up * distance;
    public void MoveDown(float distance) => Position += Vector3.Down * distance;

    public void LookLeft(float radians) => Yaw -= radians;
    public void LookRight(float radians) => Yaw += radians;
    public void LookUp(float radians) => Pitch -= radians;
    public void LookDown(float radians) => Pitch += radians;

    public void Update(float deltaTime, int deltaX, int deltaY, int deltaWheel, bool isActive)
    {
        if (isActive)
        {
            const float sensitivity = 0.01f;
            Yaw -= deltaX * sensitivity;
            Pitch -= deltaY * sensitivity;
            Pitch = MathHelper.Clamp(Pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

            _speedMultiplier = MathHelper.Clamp(_speedMultiplier + deltaWheel * 0.001f, 0.01f, 5f);

            float speed = _baseSpeed * deltaTime * _speedMultiplier;
            var keyboard = Keyboard.GetState();
            if (keyboard.IsKeyDown(Keys.LeftShift)) speed *= 0.5f;
            if (keyboard.IsKeyDown(Keys.Space)) speed *= 2;
            if (keyboard.IsKeyDown(Keys.W)) MoveForward(speed);
            if (keyboard.IsKeyDown(Keys.S)) MoveBackward(speed);
            if (keyboard.IsKeyDown(Keys.A)) MoveLeft(speed);
            if (keyboard.IsKeyDown(Keys.D)) MoveRight(speed);
            if (keyboard.IsKeyDown(Keys.E)) MoveUp(speed);
            if (keyboard.IsKeyDown(Keys.Q)) MoveDown(speed);
            if (keyboard.IsKeyDown(Keys.R)) Reset();
        }
    }
}
