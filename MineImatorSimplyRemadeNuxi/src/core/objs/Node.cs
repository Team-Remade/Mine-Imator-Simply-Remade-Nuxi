using Microsoft.Xna.Framework;

namespace MineImatorSimplyRemadeNuxi.core.objs;

public class Node
{
    private Vector3 _position;
    private float _pitch;
    private float _yaw;

    public bool Visible = true;

    public Vector3 Position
    {
        get => _position;
        set { _position = value; PositionUpdated(); }
    }
    public float Pitch
    {
        get => _pitch;
        set { _pitch = value; PositionUpdated(); }
    }
    public float Yaw
    {
        get => _yaw;
        set { _yaw = value; PositionUpdated(); }
    }

    public Vector3 Rotation
    {
        get => new Vector3(_pitch, _yaw, 0);
        set { _pitch = value.X; _yaw = value.Y; PositionUpdated(); }
    }
    
    public virtual void PositionUpdated()
    {
        
    }
}