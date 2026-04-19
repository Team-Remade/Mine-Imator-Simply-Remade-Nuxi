using Microsoft.Xna.Framework;

namespace MineImatorSimplyRemadeNuxi.gizmo;

/// <summary>
/// Port of GizmoPlugin/GizmoHelper.cs. Godot APIs replaced with XNA/MonoGame equivalents.
/// Only GetEdge is needed for the MonoGame port; SetOnTopOfAlpha and ScaledOrthogonal are omitted.
/// </summary>
public static class GizmoHelper
{
    /// <summary>
    /// Port of https://github.com/godotengine/godot/blob/master/core/math/aabb.cpp#L361
    /// Returns the two endpoints of AABB edge <paramref name="edge"/> (0..11).
    /// </summary>
    public static void GetEdge(Vector3 position, Vector3 size, int edge, out Vector3 from, out Vector3 to)
    {
        from = to = default;
        switch (edge)
        {
            case 0:
                from = new(position.X + size.X, position.Y, position.Z);
                to   = new(position.X, position.Y, position.Z);
                break;
            case 1:
                from = new(position.X + size.X, position.Y, position.Z + size.Z);
                to   = new(position.X + size.X, position.Y, position.Z);
                break;
            case 2:
                from = new(position.X, position.Y, position.Z + size.Z);
                to   = new(position.X + size.X, position.Y, position.Z + size.Z);
                break;
            case 3:
                from = new(position.X, position.Y, position.Z);
                to   = new(position.X, position.Y, position.Z + size.Z);
                break;
            case 4:
                from = new(position.X, position.Y + size.Y, position.Z);
                to   = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            case 5:
                from = new(position.X + size.X, position.Y + size.Y, position.Z);
                to   = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
            case 6:
                from = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                to   = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            case 7:
                from = new(position.X, position.Y + size.Y, position.Z + size.Z);
                to   = new(position.X, position.Y + size.Y, position.Z);
                break;
            case 8:
                from = new(position.X, position.Y, position.Z + size.Z);
                to   = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            case 9:
                from = new(position.X, position.Y, position.Z);
                to   = new(position.X, position.Y + size.Y, position.Z);
                break;
            case 10:
                from = new(position.X + size.X, position.Y, position.Z);
                to   = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            case 11:
                from = new(position.X + size.X, position.Y, position.Z + size.Z);
                to   = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
        }
    }
}
