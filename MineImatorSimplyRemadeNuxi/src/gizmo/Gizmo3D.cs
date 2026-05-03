using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MineImatorSimplyRemadeNuxi.core.objs;
using MineImatorSimplyRemadeNuxi.core.objs.nodes;
using MineImatorSimplyRemadeNuxi.helpers;
using Numerics = System.Numerics;

namespace MineImatorSimplyRemadeNuxi.gizmo;

// ── Minimal Transform3D stand-in ─────────────────────────────────────────────
/// <summary>
/// Lightweight analogue of Godot's Transform3D.
/// Basis is stored as a Matrix (upper-left 3×3); Origin is the translation column.
/// </summary>
public struct Transform3D
{
    public Matrix Basis;   // stored in Godot column-major convention: columns = axes
    public Vector3 Origin; // translation

    public static readonly Transform3D Identity = new(Matrix.Identity, Vector3.Zero);

    public Transform3D(Matrix basis, Vector3 origin)
    {
        Basis   = basis;
        Origin  = origin;
    }

    public Vector3 BasisX => new(Basis.M11, Basis.M21, Basis.M31);
    public Vector3 BasisY => new(Basis.M12, Basis.M22, Basis.M32);
    public Vector3 BasisZ => new(Basis.M13, Basis.M23, Basis.M33);

    public Vector3 BasisColumn(int i) => i switch
    {
        0 => BasisX,
        1 => BasisY,
        2 => BasisZ,
        _ => throw new ArgumentOutOfRangeException()
    };

    public Transform3D AffineInverse()
    {
        Matrix inv = Matrix.Invert(Basis);
        Vector3 invOrigin = Vector3.Transform(-Origin, inv);
        return new Transform3D(inv, invOrigin);
    }

    public Transform3D Orthonormalized()
    {
        Vector3 x = Vector3.Normalize(BasisX);
        Vector3 y = BasisY;
        y = Vector3.Normalize(y - x * Vector3.Dot(x, y));
        Vector3 z = Vector3.Cross(x, y);
        Matrix m = Matrix.Identity;
        m.M11 = x.X; m.M21 = x.Y; m.M31 = x.Z;
        m.M12 = y.X; m.M22 = y.Y; m.M32 = y.Z;
        m.M13 = z.X; m.M23 = z.Y; m.M33 = z.Z;
        return new Transform3D(m, Origin);
    }

    public Transform3D ScaledBasis(Vector3 scale)
    {
        Matrix m = Basis;
        m.M11 *= scale.X; m.M21 *= scale.X; m.M31 *= scale.X;
        m.M12 *= scale.Y; m.M22 *= scale.Y; m.M32 *= scale.Y;
        m.M13 *= scale.Z; m.M23 *= scale.Z; m.M33 *= scale.Z;
        return new Transform3D(m, Origin);
    }

    public Transform3D TranslatedLocal(Vector3 localOffset)
    {
        return new Transform3D(Basis, Origin + GizmoMath.BasisTransform(Basis, localOffset));
    }

    public Transform3D Translated(Vector3 worldOffset)
    {
        return new Transform3D(Basis, Origin + worldOffset);
    }

    public Matrix ToMatrix4x4()
    {
        Matrix xna = Matrix.Transpose(Basis);
        xna.M41 = Origin.X; xna.M42 = Origin.Y; xna.M43 = Origin.Z; xna.M44 = 1f;
        return xna;
    }
}

// ── Math helpers ──────────────────────────────────────────────────────────────

internal static class GizmoMath
{
    /// <summary>Ray-plane intersection.  Returns null when ray is parallel to the plane.</summary>
    public static Vector3? PlaneIntersectsRay(Vector3 planeNormal, Vector3 planePoint,
                                              Vector3 rayOrigin, Vector3 rayDir)
    {
        float denom = Vector3.Dot(planeNormal, rayDir);
        if (MathF.Abs(denom) < 1e-6f) return null;
        float t = Vector3.Dot(planeNormal, planePoint - rayOrigin) / denom;
        if (t < 0) return null;
        return rayOrigin + rayDir * t;
    }

    /// <summary>
    /// Returns the first intersection point (and normal) of a segment against a sphere, or
    /// an empty array if there is no intersection.  Mirrors Geometry3D.SegmentIntersectsSphere.
    /// </summary>
    public static Vector3[] SegmentIntersectsSphere(Vector3 from, Vector3 to,
                                                    Vector3 center, float radius)
    {
        Vector3 dir = to - from;
        float   len = dir.Length();
        if (len < 1e-7f) return Array.Empty<Vector3>();
        Vector3 d = dir / len;

        Vector3 oc    = from - center;
        float   b     = Vector3.Dot(oc, d);
        float   c     = oc.LengthSquared() - radius * radius;
        float   discr = b * b - c;

        if (discr < 0) return Array.Empty<Vector3>();

        float t = -b - MathF.Sqrt(discr);
        if (t < 0 || t > len) return Array.Empty<Vector3>();

        Vector3 hit    = from + d * t;
        Vector3 normal = Vector3.Normalize(hit - center);
        return [hit, normal];
    }

    /// <summary>Index of the smallest absolute component (0=X, 1=Y, 2=Z).</summary>
    public static int MinAxisIndex(Vector3 v)
    {
        float ax = MathF.Abs(v.X), ay = MathF.Abs(v.Y), az = MathF.Abs(v.Z);
        if (ax <= ay && ax <= az) return 0;
        if (ay <= az)             return 1;
        return 2;
    }

    /// <summary>Signed angle from <paramref name="from"/> to <paramref name="to"/> around <paramref name="axis"/>.</summary>
    public static float SignedAngleTo(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 cross = Vector3.Cross(from, to);
        float   dot   = Vector3.Dot(from, to);
        float   angle = MathF.Atan2(cross.Length(), dot);
        if (Vector3.Dot(axis, cross) < 0) angle = -angle;
        return angle;
    }

    /// <summary>Snap a value to the nearest multiple of <paramref name="step"/>.</summary>
    public static float Snapped(float value, float step)
    {
        if (step <= 0) return value;
        return MathF.Floor(value / step + 0.5f) * step;
    }

    /// <summary>Component-wise snap.</summary>
    public static Vector3 Snapped(Vector3 value, float step)
        => new(Snapped(value.X, step), Snapped(value.Y, step), Snapped(value.Z, step));

    /// <summary>
    /// Builds a rotation matrix from an axis-angle (Rodrigues formula).
    /// Equivalent to <c>new Basis(axis, angle)</c> in Godot.
    /// </summary>
    public static Matrix AxisAngle(Vector3 axis, float angle)
    {
        return Matrix.CreateFromAxisAngle(axis, angle);
    }

    /// <summary>Rotate vector <paramref name="v"/> by matrix <paramref name="m"/> (3×3 part only).</summary>
    public static Vector3 BasisTransform(Matrix m, Vector3 v)
    {
        return new Vector3(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);
    }

    /// <summary>
    /// ScaledOrthogonal – port of basis.cpp#L262.
    /// Rescales the basis while preserving orientation.
    /// </summary>
    public static Matrix ScaledOrthogonal(Matrix basis, Vector3 scale)
    {
        Vector3 col0 = new(basis.M11, basis.M21, basis.M31);
        Vector3 col1 = new(basis.M12, basis.M22, basis.M32);
        Vector3 col2 = new(basis.M13, basis.M23, basis.M33);

        Vector3 s = new(-1, -1, -1);
        s.X += scale.X; s.Y += scale.Y; s.Z += scale.Z;
        bool sign = (s.X + s.Y + s.Z) < 0;

        Vector3 bx = Vector3.Normalize(col0);
        Vector3 by = col1;
        by = Vector3.Normalize(by - bx * Vector3.Dot(bx, by));
        Vector3 bz = Vector3.Normalize(Vector3.Cross(bx, by));

        float sx = s.X * bx.X + s.Y * by.X + s.Z * bz.X;
        float sy = s.X * bx.Y + s.Y * by.Y + s.Z * bz.Y;
        float sz = s.X * bx.Z + s.Y * by.Z + s.Z * bz.Z;
        s = new(sx, sy, sz);

        Vector3 dots = Vector3.Zero;
        Vector3[] bCols = [bx, by, bz];
        Vector3[] sCols = [col0, col1, col2];
        for (int i = 0; i < 3; i++)
        {
            float sv = i == 0 ? s.X : i == 1 ? s.Y : s.Z;
            for (int j = 0; j < 3; j++)
            {
                float dot = Vector3.Dot(
                    Vector3.Normalize(sCols[i]),
                    bCols[j]);
                if (j == 0) dots.X += sv * MathF.Abs(dot);
                else if (j == 1) dots.Y += sv * MathF.Abs(dot);
                else dots.Z += sv * MathF.Abs(dot);
            }
        }
        if (sign != ((dots.X + dots.Y + dots.Z) < 0))
            dots = -dots;

        Vector3 newScale = Vector3.One + dots;
        Matrix result = basis;
        result.M11 *= newScale.X; result.M21 *= newScale.X; result.M31 *= newScale.X;
        result.M12 *= newScale.Y; result.M22 *= newScale.Y; result.M32 *= newScale.Y;
        result.M13 *= newScale.Z; result.M23 *= newScale.Z; result.M33 *= newScale.Z;
        return result;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// MonoGame port of GizmoPlugin/Gizmo3D.cs.
/// All Godot rendering APIs are replaced with MonoGame VertexBuffer + BasicEffect.
/// Mouse input is driven externally from AppViewport via UpdateHover / TryBeginEdit /
/// ContinueEdit / EndEdit.
/// </summary>
public class Gizmo3D
{
    // ── Constants ─────────────────────────────────────────────────────────────

    const float DEFAULT_FLOAT_STEP = 0.001f;
    const float MAX_Z              = 1000000.0f;

    const float GIZMO_ARROW_WIDTH  = 0.12f;
    const float GIZMO_ARROW_SIZE   = 0.35f;
    const float GIZMO_RING_HALF_WIDTH = 0.1f;
    const float GIZMO_PLANE_SIZE   = 0.2f;
    const float GIZMO_PLANE_DST    = 0.3f;
    const float GIZMO_CIRCLE_SIZE  = 1.1f;
    const float GIZMO_SCALE_OFFSET = GIZMO_CIRCLE_SIZE - 0.3f;
    const float GIZMO_ARROW_OFFSET = GIZMO_CIRCLE_SIZE + 0.15f;
    const int   CIRCLE_SEGMENTS    = 128;
    const int   ARC_SEGMENTS       = 64;

    // ── Enums ─────────────────────────────────────────────────────────────────

    [Flags]
    public enum ToolMode { Move = 1, Rotate = 2, Scale = 4, All = 7 }
    public enum TransformMode  { None, Rotate, Translate, Scale }
    public enum TransformPlane { View, X, Y, Z, YZ, XZ, XY }

    // ── Public properties ─────────────────────────────────────────────────────

    public ToolMode Mode          { get; set; } = ToolMode.Move | ToolMode.Scale | ToolMode.Rotate;
    public bool     Snapping      { get; set; }
    public bool     ShiftSnap     { get; set; }
    public string   Message       { get; private set; } = "";
    public bool     Editing       { get; private set; }
    public bool     Hovering      { get; private set; }
    public bool     Visible       { get; private set; }
    public bool     UseLocalSpace { get; set; } = false;
    public float    Size          { get; set; } = 80.0f;
    public bool     ShowAxes      { get; set; } = true;
    public bool     ShowSelectionBox  { get; set; } = false;
    public bool     ShowRotationLine  { get; set; } = true;
    public bool     ShowRotationArc   { get; set; } = true;
    public float    Opacity           { get; set; } = 0.9f;
    public float    RotateSnap        { get; set; } = 15.0f;
    public float    TranslateSnap     { get; set; } = 1.0f;
    public float    ScaleSnap         { get; set; } = 0.25f;

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<TransformMode>               TransformBegin;
    public event Action<TransformMode, Vector3>      TransformChanged;
    public event Action<TransformMode, TransformPlane> TransformEnd;

    // ── Axis colors ───────────────────────────────────────────────────────────

    private Color[] _colors =
    [
        new Color(0.96f, 0.20f, 0.32f),   // X – red
        new Color(0.53f, 0.84f, 0.01f),   // Y – green
        new Color(0.16f, 0.55f, 0.96f),   // Z – blue
    ];
    private Color _selBoxColor = new Color(1.0f, 0.5f, 0f);

    // ── Internal geometry buffers ─────────────────────────────────────────────
    // Each entry is a list of VertexPositionColor vertices to draw as TriangleList or LineList.

    private struct GizmoPart
    {
        public VertexBuffer Buffer;
        public PrimitiveType PrimType;
        public int PrimCount;
        public Color NormalColor;
        public Color HighlightColor;
        public bool DepthTestDisabled; // for rotation rings
    }

    // Indices match the axis: 0=X, 1=Y, 2=Z
    private GizmoPart[] _moveGizmo        = new GizmoPart[3]; // shaft + arrow
    private GizmoPart[] _movePlaneGizmo   = new GizmoPart[3];
    private GizmoPart[] _rotateGizmo      = new GizmoPart[3];
    private GizmoPart   _rotateGizmoWhite;                     // white outline ring
    private GizmoPart[] _scaleGizmo       = new GizmoPart[3];
    private GizmoPart[] _scalePlaneGizmo  = new GizmoPart[3];
    private GizmoPart[] _axisGizmo        = new GizmoPart[3]; // infinite axis lines

    // Per highlighted axis (-1 = none)
    private int _highlightAxis = -1;

    // ── Transform state ───────────────────────────────────────────────────────

    private Transform3D _gizmoTransform = Transform3D.Identity;
    private float       _gizmoScale     = 1.0f;

    private struct SelectedItem
    {
        public SceneObject Object;
        public Transform3D TargetOriginal; // local transform at drag start
        public Transform3D TargetGlobal;   // world transform at drag start
    }

    private readonly List<SelectedItem> _selections = new();

    // Visual center of the gizmo, updated every frame during translate drags
    // so the handles follow the object in real time.
    private Vector3 _visualCenter;

    private struct EditData
    {
        public bool          ShowRotationLine;
        public Transform3D   Original;
        public TransformMode Mode;
        public TransformPlane Plane;
        public Vector3       ClickRay, ClickRayPos;
        public Vector3       Center;
        public Vector2       MousePos;

        // Rotation arc
        public Vector3 RotationAxis;
        public float   AccumulatedRotationAngle;
        public float   DisplayRotationAngle;
        public Vector3? InitialClickVector;
        public Vector3? PreviousRotationVector;
        public bool    GizmoInitiated;
    }

    private EditData _edit = new();

    // ── Camera reference (set by AppViewport each frame) ─────────────────────

    private WorkCamera _camera;
    private Vector2    _imageMin;
    private Vector2    _imageSize;

    // ── Graphics device ───────────────────────────────────────────────────────

    private readonly GraphicsDevice _gd;
    private BasicEffect _effect;

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public Gizmo3D(GraphicsDevice graphicsDevice)
    {
        _gd     = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = false,  // color driven by DiffuseColor, not vertex data
            LightingEnabled    = false,
        };
        InitIndicators();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Selection API
    // ─────────────────────────────────────────────────────────────────────────

    public void Select(SceneObject obj)
    {
        if (obj == null) return;
        // Avoid duplicates
        foreach (var s in _selections)
            if (s.Object == obj) return;

        _selections.Add(new SelectedItem
        {
            Object         = obj,
            TargetOriginal = GetLocalTransform(obj),
            TargetGlobal   = GetWorldTransform(obj),
        });
        UpdateTransformGizmo();
    }

    public void Deselect(SceneObject obj)
    {
        for (int i = _selections.Count - 1; i >= 0; i--)
            if (_selections[i].Object == obj) { _selections.RemoveAt(i); break; }
        UpdateTransformGizmo();
    }

    public void ClearSelection()
    {
        _selections.Clear();
        UpdateTransformGizmo();
    }

    public bool IsSelected(SceneObject obj)
    {
        foreach (var s in _selections)
            if (s.Object == obj) return true;
        return false;
    }

    public int GetSelectedCount() => _selections.Count;

    // ─────────────────────────────────────────────────────────────────────────
    // Input API (called by AppViewport)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call every frame when the mouse is over the viewport but no button is held.
    /// Updates the hover highlight and the Hovering property.
    /// </summary>
    public void UpdateHover(Vector2 screenPos, WorkCamera camera, Vector2 imageMin, Vector2 imageSize)
    {
        _camera    = camera;
        _imageMin  = imageMin;
        _imageSize = imageSize;
        Hovering   = TransformGizmoSelect(screenPos, highlightOnly: true);
    }

    /// <summary>
    /// Call on left-button press.  Returns true if the click landed on a handle
    /// and edit mode was started.
    /// </summary>
    public bool TryBeginEdit(Vector2 screenPos, WorkCamera camera, Vector2 imageMin, Vector2 imageSize)
    {
        _camera   = camera;
        _imageMin  = imageMin;
        _imageSize = imageSize;

        _edit.InitialClickVector       = null;
        _edit.PreviousRotationVector   = null;
        _edit.AccumulatedRotationAngle = 0;
        _edit.DisplayRotationAngle     = 0;
        _edit.GizmoInitiated           = false;
        _edit.MousePos                 = screenPos;

        bool hit = TransformGizmoSelect(screenPos, highlightOnly: false);
        if (hit)
        {
            Editing = true;
            TransformBegin?.Invoke(_edit.Mode);
        }
        return hit;
    }

    /// <summary>
    /// Call every frame while the left button is held and TryBeginEdit returned true.
    /// </summary>
    public void ContinueEdit(Vector2 screenPos)
    {
        if (!Editing) return;
        _edit.MousePos = screenPos;
        Vector3 value  = UpdateTransform(false);
        TransformChanged?.Invoke(_edit.Mode, value);
    }

    /// <summary>
    /// Call when left button is released.
    /// </summary>
    public void EndEdit()
    {
        if (!Editing) return;
        TransformEnd?.Invoke(_edit.Mode, _edit.Plane);
        Editing       = false;
        Message       = "";
        _edit.Mode    = TransformMode.None;
        UpdateTransformGizmo();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Snap helpers
    // ─────────────────────────────────────────────────────────────────────────

    public float GetTranslateSnap() => ShiftSnap ? TranslateSnap / 10f : TranslateSnap;
    public float GetRotationSnap()  => ShiftSnap ? RotateSnap   / 3f  : RotateSnap;
    public float GetScaleSnap()     => ShiftSnap ? ScaleSnap    / 2f  : ScaleSnap;

    // ─────────────────────────────────────────────────────────────────────────
    // Render – 3D pass
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw all gizmo 3D geometry into the current render target.
    /// Call this after normal scene objects are rendered.
    /// </summary>
    public void Render(GraphicsDevice gd, WorkCamera camera)
    {
        // Always update the camera reference so that UpdateTransformGizmoView()
        // has a valid camera even when the mouse has not yet entered the viewport
        // (e.g. after a position change driven by the PropertiesPanel).
        _camera = camera;

        // Recompute gizmo origin from live object positions every frame so that
        // external changes (e.g. PropertiesPanel DragFloat) move the gizmo
        // immediately without requiring a mouse-hover event.
        UpdateTransformGizmo();

        if (!Visible || _selections.Count == 0) return;

        UpdateTransformGizmoView();

        _effect.View       = camera.View;
        _effect.Projection = camera.Projection;

        var savedDepth = gd.DepthStencilState;
        var savedRast  = gd.RasterizerState;
        gd.RasterizerState = RasterizerState.CullNone;

        bool showGizmo = !IsRotationArcVisible();

        bool hasMove      = (Mode & ToolMode.Move)   != 0;
        bool hasRot       = (Mode & ToolMode.Rotate) != 0;
        bool hasScale     = (Mode & ToolMode.Scale)  != 0;
        bool scaleAndMove = hasMove && hasScale;

        // ── Everything rendered depth-test-disabled (always on top) ──────────
        gd.DepthStencilState = DepthStencilState.None;

        if (showGizmo)
        {
            for (int i = 0; i < 3; i++)
            {
                _effect.World = BuildAxisTransform(i);

                if (hasMove)  DrawPart(gd, ref _moveGizmo[i],      i);
                if (hasMove)  DrawPart(gd, ref _movePlaneGizmo[i], i + 6);
                if (hasScale) DrawPart(gd, ref _scaleGizmo[i],     i + 9);
                if (hasScale && !hasMove) DrawPart(gd, ref _scalePlaneGizmo[i], i + 12);
            }

            if (hasRot)
            {
                for (int i = 0; i < 3; i++)
                {
                    _effect.World = BuildAxisTransform(i);
                    DrawPart(gd, ref _rotateGizmo[i], i + 3);
                }
                // The white outline ring from the Godot source relies on a vertex shader
                // (front-face displacement) that has no MonoGame equivalent.
                // Skipped to avoid covering the Z rotation ring with an opaque dark tint.
            }
        }

        // Axis constraint lines (shown while dragging)
        for (int i = 0; i < 3; i++)
        {
            bool showAxis = ShowAxes && Editing &&
                (_edit.Plane == (TransformPlane)(i + 1) ||
                 (_edit.Plane == TransformPlane.XY && i < 2) ||
                 (_edit.Plane == TransformPlane.XZ && i != 1) ||
                 (_edit.Plane == TransformPlane.YZ && i > 0));
            if (showAxis)
            {
                _effect.World = _gizmoTransform.ToMatrix4x4();
                DrawPart(gd, ref _axisGizmo[i], i);
            }
        }

        // ── Selection boxes ───────────────────────────────────────────────────
        if (ShowSelectionBox)
        {
            DrawSelectionBoxes(gd);
        }

        gd.DepthStencilState = savedDepth;
        gd.RasterizerState   = savedRast;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Render – 2D overlay (ImGui draw list)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw the rotation arc and line on the ImGui overlay.
    /// Call this after ImGui.Image() in AppViewport.Render().
    /// </summary>
    public void RenderOverlay(WorkCamera camera, Vector2 imageMin, Vector2 imageSize)
    {
        _camera    = camera;
        _imageMin  = imageMin;
        _imageSize = imageSize;

        if (_edit.Mode != TransformMode.Rotate) return;

        var dl = ImGui.GetWindowDrawList();

        Numerics.Vector2 center2d = PointToScreen(_edit.Center);

        Color handleColor = _edit.Plane switch
        {
            TransformPlane.X => _colors[0],
            TransformPlane.Y => _colors[1],
            TransformPlane.Z => _colors[2],
            _                => Color.White,
        };

        if (IsRotationArcVisible() && _edit.InitialClickVector.HasValue)
        {
            Vector3 up    = _edit.RotationAxis;
            Vector3 right = _edit.InitialClickVector.Value;
            right -= up * Vector3.Dot(up, right);
            if (right.LengthSquared() > 1e-8f)
                right = Vector3.Normalize(right);
            Vector3 forward = Vector3.Cross(up, right);

            // Draw full circle
            var circlePts = new List<Numerics.Vector2>(ARC_SEGMENTS + 1);
            for (int i = 0; i <= ARC_SEGMENTS; i++)
            {
                float angle = (float)i / ARC_SEGMENTS * MathHelper.TwoPi;
                Vector3 pt3 = _edit.Center + _gizmoScale * GIZMO_CIRCLE_SIZE *
                              (right * MathF.Cos(angle) + forward * MathF.Sin(angle));
                circlePts.Add(PointToScreen(pt3));
            }
            uint circleCol = ToImGuiColor(ColorFromHsv(GetHue(handleColor), 0.6f, 1f, 0.8f));
            for (int i = 0; i < circlePts.Count - 1; i++)
                dl.AddLine(circlePts[i], circlePts[i + 1], circleCol, 2f);

            // Draw filled arc
            float dispAngle = _edit.DisplayRotationAngle;
            float absAngle  = MathF.Abs(dispAngle);
            if (absAngle > MathHelper.TwoPi)
            {
                float rem = absAngle % MathHelper.TwoPi;
                if (rem < 0.01f) rem = MathHelper.TwoPi;
                dispAngle = MathF.Sign(dispAngle) * rem;
                absAngle  = MathF.Abs(dispAngle);
            }

            int numSeg = Math.Max(8, (int)(absAngle / (MathHelper.TwoPi / ARC_SEGMENTS) * ARC_SEGMENTS));
            numSeg = Math.Min(numSeg, ARC_SEGMENTS);

            uint fillCol = ToImGuiColor(new Color(1f, 1f, 1f, 0.2f));
            float startA = dispAngle > 0 ? 0f : dispAngle;
            float endA   = dispAngle > 0 ? dispAngle : 0f;

            for (int i = 0; i < numSeg; i++)
            {
                float t1 = (float)i / numSeg;
                float t2 = (float)(i + 1) / numSeg;
                float a1 = MathHelper.Lerp(startA, endA, t1);
                float a2 = MathHelper.Lerp(startA, endA, t2);

                Vector3 p1_3d = _edit.Center + _gizmoScale * GIZMO_CIRCLE_SIZE *
                                (right * MathF.Cos(a1) + forward * MathF.Sin(a1));
                Vector3 p2_3d = _edit.Center + _gizmoScale * GIZMO_CIRCLE_SIZE *
                                (right * MathF.Cos(a2) + forward * MathF.Sin(a2));

                dl.AddTriangleFilled(
                    center2d,
                    PointToScreen(p1_3d),
                    PointToScreen(p2_3d),
                    fillCol);
            }

            // Edge lines from center
            uint edgeCol = ToImGuiColor(ColorFromHsv(GetHue(handleColor), 0.8f, 1f, 0.7f));
            Vector3 startPt = _edit.Center + _gizmoScale * GIZMO_CIRCLE_SIZE * right;
            Vector3 endPt   = _edit.Center + _gizmoScale * GIZMO_CIRCLE_SIZE *
                              (right * MathF.Cos(dispAngle) + forward * MathF.Sin(dispAngle));
            dl.AddLine(center2d, PointToScreen(startPt), edgeCol, 2f);
            dl.AddLine(center2d, PointToScreen(endPt),   edgeCol, 2f);
        }

        // Rotation line to cursor
        if (_edit.ShowRotationLine && ShowRotationLine)
        {
            Color lineColor = ColorFromHsv(GetHue(handleColor), 0.25f, 1f, 1f);
            dl.AddLine(
                VectorHelper.MonogameToSystemVec2(_edit.MousePos),
                center2d,
                ToImGuiColor(lineColor),
                2f);
        }
    }

    public bool IsRotationArcVisible()
        => _edit.Mode == TransformMode.Rotate && ShowRotationArc
           && _edit.AccumulatedRotationAngle != 0f && _edit.GizmoInitiated;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal: geometry init
    // ─────────────────────────────────────────────────────────────────────────

    private void InitIndicators()
    {
        // Godot's canonical vectors for i=0 (the mesh is built once in this orientation
        // and the per-axis world transform LookAt-rotates it at draw time).
        //   ivec  = (0, 0, -1)  – shaft direction (local –Z)
        //   nivec = (-1,-1, 0)  – perpendicular mask
        //   ivec2 = (-1, 0, 0)  – used for plane quad / ring sweep direction
        //   ivec3 = ( 0,-1, 0)  – used for plane quad
        Vector3 ivec  = new(0,  0, -1);
        Vector3 nivec = new(-1, -1,  0);
        Vector3 ivec2 = new(-1,  0,  0);
        Vector3 ivec3 = new(0,  -1,  0);

        // Build the geometry ONCE in canonical (i=0) orientation.
        // The per-axis LookAt matrix in BuildAxisTransform will orient it correctly.

        // ── Arrow shaft (move handle) ─────────────────────────────────────────
        // (We share the same vertex buffer for all three axes; DrawPart applies the
        //  axis transform that re-orients –Z toward the target axis.)
        var canonMoveVerts = BuildArrow(
        [
            nivec * 0.0f  + ivec * GIZMO_ARROW_OFFSET,
            nivec * 0.01f + ivec * GIZMO_ARROW_OFFSET,
            nivec * 0.01f + ivec * GIZMO_ARROW_OFFSET,
            nivec * GIZMO_ARROW_WIDTH + ivec * GIZMO_ARROW_OFFSET,
            nivec * 0.0f  + ivec * (GIZMO_ARROW_OFFSET + GIZMO_ARROW_SIZE)
        ], ivec, 5, 16, Color.White); // color overridden per-draw

        var canonScaleVerts = BuildArrow(
        [
            nivec * 0.0f  + ivec * 0.0f,
            nivec * 0.01f + ivec * 0.0f,
            nivec * 0.01f + ivec * 1.0f * GIZMO_SCALE_OFFSET,
            nivec * 0.07f + ivec * 1.0f * GIZMO_SCALE_OFFSET,
            nivec * 0.07f + ivec * 1.2f * GIZMO_SCALE_OFFSET,
            nivec * 0.0f  + ivec * 1.2f * GIZMO_SCALE_OFFSET
        ], ivec, 6, 4, Color.White);

        var canonPlaneVerts = BuildPlaneQuad(ivec, ivec2, ivec3, Color.White);

        // Rotation ring – also built once in canonical orientation
        var canonRingVerts = BuildRotationRing(ivec, ivec2, Color.White);

        // White outline ring (same shape, white)
        var whiteRingVerts = BuildRotationRing(ivec, ivec2,
            new Color(0.75f, 0.75f, 0.75f, (byte)(Opacity / 3f * 255)));

        for (int i = 0; i < 3; i++)
        {
            Color col      = WithAlpha(_colors[i], _colors[i].A * Opacity);
            Color colHl    = ColorFromHsv(GetHue(_colors[i]), 0.25f, 1f, 1f);
            Color planeCol = col;
            Color planeHl  = colHl;

            _moveGizmo[i]       = MakePart(Recolor(canonMoveVerts,  col), PrimitiveType.TriangleList, col, colHl, false);
            _movePlaneGizmo[i]  = MakePart(Recolor(canonPlaneVerts, planeCol), PrimitiveType.TriangleList, planeCol, planeHl, false);
            _rotateGizmo[i]     = MakePart(Recolor(canonRingVerts,  col), PrimitiveType.TriangleList, col, colHl, true);
            _scaleGizmo[i]      = MakePart(Recolor(canonScaleVerts, col), PrimitiveType.TriangleList, col, colHl, false);
            _scalePlaneGizmo[i] = MakePart(Recolor(canonPlaneVerts, planeCol), PrimitiveType.TriangleList, planeCol, planeHl, false);

            // Axis lines are always world-axis-aligned (X, Y, Z), not using ivec.
            Vector3 axisDir = i == 0 ? Vector3.UnitX : i == 1 ? Vector3.UnitY : Vector3.UnitZ;
            _axisGizmo[i] = MakePart(BuildAxisLineDir(axisDir, colHl), PrimitiveType.LineList, colHl, colHl, false);
        }

        // White outline ring (shared across all three; same shape as ring[2] in Godot)
        _rotateGizmoWhite = MakePart(
            Recolor(whiteRingVerts, new Color(0.75f, 0.75f, 0.75f, (byte)(Opacity / 3f * 255))),
            PrimitiveType.TriangleList,
            new Color(0.75f, 0.75f, 0.75f, (byte)(Opacity / 3f * 255)),
            new Color(0.75f, 0.75f, 0.75f, 255), true);
    }

    /// <summary>Return a copy of the vertex list with all colors replaced by <paramref name="col"/>.</summary>
    private static List<VertexPositionColor> Recolor(List<VertexPositionColor> src, Color col)
    {
        var dst = new List<VertexPositionColor>(src.Count);
        foreach (var v in src)
            dst.Add(new VertexPositionColor(v.Position, col));
        return dst;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometry builders
    // ─────────────────────────────────────────────────────────────────────────

    private List<VertexPositionColor> BuildArrow(Vector3[] arrowProfile, Vector3 ivec,
                                                  int arrowPoints, int arrowSides, Color col)
    {
        var verts = new List<VertexPositionColor>();
        float step = MathHelper.TwoPi / arrowSides;

        for (int k = 0; k < arrowSides; k++)
        {
            Matrix maa = Matrix.CreateFromAxisAngle(ivec, k * step);
            Matrix mbb = Matrix.CreateFromAxisAngle(ivec, (k + 1) * step);

            for (int j = 0; j < arrowPoints - 1; j++)
            {
                Vector3 a0 = GizmoMath.BasisTransform(maa, arrowProfile[j]);
                Vector3 a1 = GizmoMath.BasisTransform(mbb, arrowProfile[j]);
                Vector3 a2 = GizmoMath.BasisTransform(mbb, arrowProfile[j + 1]);
                Vector3 a3 = GizmoMath.BasisTransform(maa, arrowProfile[j + 1]);

                verts.Add(new VertexPositionColor(a0, col));
                verts.Add(new VertexPositionColor(a1, col));
                verts.Add(new VertexPositionColor(a2, col));

                verts.Add(new VertexPositionColor(a0, col));
                verts.Add(new VertexPositionColor(a2, col));
                verts.Add(new VertexPositionColor(a3, col));
            }
        }
        return verts;
    }

    private List<VertexPositionColor> BuildPlaneQuad(Vector3 ivec, Vector3 ivec2, Vector3 ivec3, Color col)
    {
        Vector3 vec = ivec2 - ivec3;
        Vector3[] plane =
        [
            vec * GIZMO_PLANE_DST,
            vec * GIZMO_PLANE_DST + ivec2 * GIZMO_PLANE_SIZE,
            vec * (GIZMO_PLANE_DST + GIZMO_PLANE_SIZE),
            vec * GIZMO_PLANE_DST - ivec3 * GIZMO_PLANE_SIZE
        ];

        Matrix ma = Matrix.CreateFromAxisAngle(ivec, MathHelper.PiOver2);
        Vector3[] pts =
        [
            GizmoMath.BasisTransform(ma, plane[0]),
            GizmoMath.BasisTransform(ma, plane[1]),
            GizmoMath.BasisTransform(ma, plane[2]),
            GizmoMath.BasisTransform(ma, plane[3])
        ];

        return
        [
            new(pts[0], col), new(pts[1], col), new(pts[2], col),
            new(pts[0], col), new(pts[2], col), new(pts[3], col)
        ];
    }

    private List<VertexPositionColor> BuildRotationRing(Vector3 ivec, Vector3 ivec2, Color col)
    {
        // The Godot source places all THICKNESS vertices of each segment at the same
        // center point and relies on a vertex shader to displace them outward along
        // their normals.  We have no shader, so we build a real torus instead:
        // each ring step spawns a small cross-section circle of THICKNESS points
        // offset by GIZMO_RING_HALF_WIDTH from the centre line.
        const int THICKNESS = 3;

        // Pre-compute all torus vertex positions
        var positions = new Vector3[CIRCLE_SEGMENTS * THICKNESS];

        float step = MathHelper.TwoPi / CIRCLE_SEGMENTS;
        for (int j = 0; j < CIRCLE_SEGMENTS; j++)
        {
            Matrix  basis  = Matrix.CreateFromAxisAngle(ivec, j * step);
            Vector3 centre = GizmoMath.BasisTransform(basis, ivec2 * GIZMO_CIRCLE_SIZE);

            // Tangent-space basis at this ring point:
            //   radial  = normalised centre direction (ivec2 rotated)
            //   axial   = ivec (the ring's rotation axis)
            Vector3 radial = Vector3.Normalize(centre);   // outward from ring axis
            Vector3 axial  = ivec;

            const float TUBE_RADIUS = 0.025f; // visual tube radius; Godot shader displaces ~0.02
            for (int k = 0; k < THICKNESS; k++)
            {
                float a   = MathHelper.TwoPi * k / THICKNESS;
                Vector3 offset = radial * MathF.Cos(a) * TUBE_RADIUS
                               + axial  * MathF.Sin(a) * TUBE_RADIUS;
                positions[j * THICKNESS + k] = centre + offset;
            }
        }

        // Stitch quads between consecutive ring segments
        var verts = new List<VertexPositionColor>(CIRCLE_SEGMENTS * THICKNESS * 6);
        for (int j = 0; j < CIRCLE_SEGMENTS; j++)
        {
            int cur  = j * THICKNESS;
            int next = ((j + 1) % CIRCLE_SEGMENTS) * THICKNESS;

            for (int k = 0; k < THICKNESS; k++)
            {
                int ks = k;
                int kn = (k + 1) % THICKNESS;

                verts.Add(new VertexPositionColor(positions[cur  + kn], col));
                verts.Add(new VertexPositionColor(positions[cur  + ks], col));
                verts.Add(new VertexPositionColor(positions[next + ks], col));

                verts.Add(new VertexPositionColor(positions[next + ks], col));
                verts.Add(new VertexPositionColor(positions[next + kn], col));
                verts.Add(new VertexPositionColor(positions[cur  + kn], col));
            }
        }
        return verts;
    }

    private List<VertexPositionColor> BuildAxisLineDir(Vector3 dir, Color col)
    {
        // Godot draws a LineStrip through the origin in both directions.
        // We use a LineList with two line segments: neg→origin, origin→pos.
        Vector3 d = dir.LengthSquared() > 1e-8f ? Vector3.Normalize(dir) : Vector3.UnitX;
        return
        [
            new VertexPositionColor(d * -1048576, col),
            new VertexPositionColor(d *  1048576, col)
        ];
    }

    private GizmoPart MakePart(List<VertexPositionColor> verts, PrimitiveType primType,
                                Color normalColor, Color highlightColor, bool depthDisabled)
    {
        if (verts.Count == 0)
            return new GizmoPart();

        int primCount = primType == PrimitiveType.TriangleList
            ? verts.Count / 3
            : verts.Count / 2;

        var buf = new VertexBuffer(_gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
        buf.SetData(verts.ToArray());

        return new GizmoPart
        {
            Buffer           = buf,
            PrimType         = primType,
            PrimCount        = primCount,
            NormalColor      = normalColor,
            HighlightColor   = highlightColor,
            DepthTestDisabled = depthDisabled,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Draw helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawPart(GraphicsDevice gd, ref GizmoPart part, int highlightAxisCode)
    {
        if (part.Buffer == null || part.PrimCount == 0) return;
        bool hl = _highlightAxis == highlightAxisCode;
        DrawPartColor(gd, ref part, hl ? part.HighlightColor : part.NormalColor);
    }

    private void DrawPartColor(GraphicsDevice gd, ref GizmoPart part, Color color)
    {
        if (part.Buffer == null || part.PrimCount == 0) return;

        // VertexColorEnabled=false: color comes entirely from DiffuseColor + Alpha.
        _effect.DiffuseColor = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        _effect.Alpha        = color.A / 255f;

        gd.SetVertexBuffer(part.Buffer);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawPrimitives(part.PrimType, 0, part.PrimCount);
        }
    }

    private void DrawSelectionBoxes(GraphicsDevice gd)
    {
        foreach (var item in _selections)
        {
            var (pos, size) = GetObjectAabb(item.Object);
            var lineVerts   = new List<VertexPositionColor>();
            Color col       = _selBoxColor;
            Color xrayCol   = WithAlpha(col, 0.15f * Opacity);

            for (int e = 0; e < 12; e++)
            {
                GizmoHelper.GetEdge(pos, size, e, out var a, out var b);
                lineVerts.Add(new VertexPositionColor(a, col));
                lineVerts.Add(new VertexPositionColor(b, col));
            }

            if (lineVerts.Count == 0) continue;

            var tmpBuf = new VertexBuffer(gd, typeof(VertexPositionColor), lineVerts.Count, BufferUsage.WriteOnly);
            tmpBuf.SetData(lineVerts.ToArray());

            Matrix worldMat = item.TargetGlobal.ToMatrix4x4();
            _effect.World = worldMat;

            // X-ray pass (depth disabled, low alpha)
            gd.DepthStencilState = DepthStencilState.None;
            _effect.DiffuseColor = new Vector3(xrayCol.R / 255f, xrayCol.G / 255f, xrayCol.B / 255f);
            _effect.Alpha        = xrayCol.A / 255f;
            gd.SetVertexBuffer(tmpBuf);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawPrimitives(PrimitiveType.LineList, 0, lineVerts.Count / 2);
            }

            // Solid pass (depth enabled)
            gd.DepthStencilState = DepthStencilState.Default;
            _effect.DiffuseColor = new Vector3(col.R / 255f, col.G / 255f, col.B / 255f);
            _effect.Alpha        = Opacity;
            gd.SetVertexBuffer(tmpBuf);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawPrimitives(PrimitiveType.LineList, 0, lineVerts.Count / 2);
            }

            tmpBuf.Dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transform update
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateTransformGizmo()
    {
        int     count = _selections.Count;
        Vector3 center = Vector3.Zero;
        Matrix  basis  = Matrix.Identity;

        if (count == 0)
        {
            Visible = false;
            return;
        }

        // While a drag is in progress:
        //   - Translate: gizmo follows the live object position (_visualCenter).
        //   - Rotate/Scale: pivot stays fixed at _edit.Center so the math stays consistent.
        if (Editing)
        {
            if (UseLocalSpace && count == 1)
                basis = _selections[0].TargetGlobal.Basis;
            Vector3 origin = _edit.Mode == TransformMode.Translate ? _visualCenter : _edit.Center;
            Visible         = true;
            _gizmoTransform = new Transform3D(basis, origin);
            return;
        }

        // Not editing – read live object positions.
        for (int i = 0; i < count; i++)
        {
            Transform3D xf = GetWorldTransform(_selections[i].Object);
            center += xf.Origin;
            if (i == 0 && UseLocalSpace) basis = xf.Basis;
        }
        center /= count;

        Visible         = true;
        _gizmoTransform = new Transform3D(count == 1 ? basis : Matrix.Identity, center);
    }

    private void UpdateTransformGizmoView()
    {
        if (!Visible || _camera == null) return;

        Vector3 gizmoOrigin  = _gizmoTransform.Origin;
        Vector3 cameraOrigin = _camera.Position;

        if (Vector3.DistanceSquared(gizmoOrigin, cameraOrigin) < 1e-8f)
        {
            Visible = false;
            return;
        }

        // Compute GizmoScale: pixels-per-world-unit at gizmo depth
        Vector3 camZ   = -Vector3.Normalize(new Vector3(_camera.View.M13, _camera.View.M23, _camera.View.M33));
        Vector3 camY   = -Vector3.Normalize(new Vector3(_camera.View.M12, _camera.View.M22, _camera.View.M32));
        float   gizmoD = MathF.Max(MathF.Abs(Vector3.Dot(camZ, gizmoOrigin - cameraOrigin)), float.Epsilon);

        Vector2 p0 = UnprojectPos(cameraOrigin + camZ * gizmoD);
        Vector2 p1 = UnprojectPos(cameraOrigin + camZ * gizmoD + camY);
        float   dd = MathF.Max(MathF.Abs(p0.Y - p1.Y), float.Epsilon);

        _gizmoScale = Size / MathF.Abs(dd);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hit testing
    // ─────────────────────────────────────────────────────────────────────────

    private bool TransformGizmoSelect(Vector2 screenpos, bool highlightOnly)
    {
        if (!Visible || _selections.Count == 0)
        {
            if (highlightOnly) _highlightAxis = -1;
            return false;
        }

        UpdateTransformGizmo();
        UpdateTransformGizmoView();

        Vector3        rayPos = GetRayPos(screenpos);
        Vector3        ray    = GetRay(screenpos);
        Transform3D    gt     = _gizmoTransform;

        // ── Move ────────────────────────────────────────────────────────────
        if ((Mode & ToolMode.Move) != 0)
        {
            int   colAxis = -1;
            float colD    = 1e20f;

            for (int i = 0; i < 3; i++)
            {
                Vector3 grabberPos    = gt.Origin + Vector3.Normalize(gt.BasisColumn(i)) * _gizmoScale * (GIZMO_ARROW_OFFSET + GIZMO_ARROW_SIZE * 0.5f);
                float   grabberRadius = _gizmoScale * GIZMO_ARROW_SIZE;

                var r = GizmoMath.SegmentIntersectsSphere(rayPos, rayPos + ray * MAX_Z, grabberPos, grabberRadius);
                if (r.Length != 0)
                {
                    float d = Vector3.Distance(r[0], rayPos);
                    if (d < colD) { colD = d; colAxis = i; }
                }
            }

            bool isPlane = false;
            if (colAxis == -1)
            {
                colD = 1e20f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 iv2        = Vector3.Normalize(gt.BasisColumn((i + 1) % 3));
                    Vector3 iv3        = Vector3.Normalize(gt.BasisColumn((i + 2) % 3));
                    Vector3 grabberPos = gt.Origin + (iv2 + iv3) * _gizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Vector3 planeNorm = Vector3.Normalize(gt.BasisColumn(i));
                    Vector3? r = GizmoMath.PlaneIntersectsRay(planeNorm, gt.Origin, rayPos, ray);
                    if (r != null)
                    {
                        float dist = Vector3.Distance(r.Value, grabberPos);
                        if (dist < _gizmoScale * GIZMO_PLANE_SIZE * 1.5f)
                        {
                            float d = Vector3.Distance(rayPos, r.Value);
                            if (d < colD) { colD = d; colAxis = i; isPlane = true; }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                    _highlightAxis = colAxis + (isPlane ? 6 : 0);
                else
                {
                    _edit.Mode  = TransformMode.Translate;
                    ComputeEdit(screenpos);
                    _edit.Plane = TransformPlane.X + colAxis + (isPlane ? 3 : 0);
                }
                return true;
            }
        }

        // ── Rotate ──────────────────────────────────────────────────────────
        if ((Mode & ToolMode.Rotate) != 0)
        {
            int colAxis = -1;

            float rayLen = Vector3.Distance(gt.Origin, rayPos) + GIZMO_CIRCLE_SIZE * _gizmoScale * 4f;
            var result   = GizmoMath.SegmentIntersectsSphere(rayPos, rayPos + ray * rayLen, gt.Origin, _gizmoScale * GIZMO_CIRCLE_SIZE);
            if (result.Length != 0)
            {
                Vector3 hitPos    = result[0];
                Vector3 hitNormal = result[1];
                if (Vector3.Dot(hitNormal, GetCameraNormal()) < 0.05f)
                {
                    // Map hit to gizmo local space, take absolute values
                    Matrix  invBasis = Matrix.Invert(_gizmoTransform.Basis);
                    Vector3 local    = GizmoMath.BasisTransform(invBasis, hitPos);
                    Vector3 abs      = new(MathF.Abs(local.X), MathF.Abs(local.Y), MathF.Abs(local.Z));
                    int minIdx       = GizmoMath.MinAxisIndex(abs);
                    float absAtMin   = minIdx == 0 ? abs.X : minIdx == 1 ? abs.Y : abs.Z;
                    if (absAtMin < _gizmoScale * GIZMO_RING_HALF_WIDTH)
                        colAxis = minIdx;
                }
            }

            if (colAxis == -1)
            {
                float colD = 1e20f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 planeNorm = Vector3.Normalize(gt.BasisColumn(i));
                    Vector3? r        = GizmoMath.PlaneIntersectsRay(planeNorm, gt.Origin, rayPos, ray);
                    if (r == null) continue;

                    float dist = Vector3.Distance(r.Value, gt.Origin);
                    Vector3 rDir = Vector3.Normalize(r.Value - gt.Origin);
                    if (Vector3.Dot(GetCameraNormal(), rDir) <= 0.005f)
                    {
                        if (dist > _gizmoScale * (GIZMO_CIRCLE_SIZE - GIZMO_RING_HALF_WIDTH) &&
                            dist < _gizmoScale * (GIZMO_CIRCLE_SIZE + GIZMO_RING_HALF_WIDTH))
                        {
                            float d = Vector3.Distance(rayPos, r.Value);
                            if (d < colD) { colD = d; colAxis = i; }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                    _highlightAxis = colAxis + 3;
                else
                {
                    _edit.Mode                    = TransformMode.Rotate;
                    ComputeEdit(screenpos);
                    _edit.Plane                   = TransformPlane.X + colAxis;
                    _edit.AccumulatedRotationAngle = 0f;
                    _edit.RotationAxis            = Vector3.Normalize(gt.BasisColumn(colAxis));
                    _edit.GizmoInitiated          = true;
                }
                return true;
            }
        }

        // ── Scale ────────────────────────────────────────────────────────────
        if ((Mode & ToolMode.Scale) != 0)
        {
            int   colAxis = -1;
            float colD    = 1e20f;

            for (int i = 0; i < 3; i++)
            {
                Vector3 grabberPos    = gt.Origin + Vector3.Normalize(gt.BasisColumn(i)) * _gizmoScale * GIZMO_SCALE_OFFSET;
                float   grabberRadius = _gizmoScale * GIZMO_ARROW_SIZE;

                var r = GizmoMath.SegmentIntersectsSphere(rayPos, rayPos + ray * MAX_Z, grabberPos, grabberRadius);
                if (r.Length != 0)
                {
                    float d = Vector3.Distance(r[0], rayPos);
                    if (d < colD) { colD = d; colAxis = i; }
                }
            }

            bool isPlane = false;
            if (colAxis == -1)
            {
                colD = 1e20f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 iv2        = Vector3.Normalize(gt.BasisColumn((i + 1) % 3));
                    Vector3 iv3        = Vector3.Normalize(gt.BasisColumn((i + 2) % 3));
                    Vector3 grabberPos = gt.Origin + (iv2 + iv3) * _gizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Vector3 planeNorm = Vector3.Normalize(gt.BasisColumn(i));
                    Vector3? r = GizmoMath.PlaneIntersectsRay(planeNorm, gt.Origin, rayPos, ray);
                    if (r != null)
                    {
                        float dist = Vector3.Distance(r.Value, grabberPos);
                        if (dist < _gizmoScale * GIZMO_PLANE_SIZE * 1.5f)
                        {
                            float d = Vector3.Distance(rayPos, r.Value);
                            if (d < colD) { colD = d; colAxis = i; isPlane = true; }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                    _highlightAxis = colAxis + (isPlane ? 12 : 9);
                else
                {
                    _edit.Mode  = TransformMode.Scale;
                    ComputeEdit(screenpos);
                    _edit.Plane = TransformPlane.X + colAxis + (isPlane ? 3 : 0);
                }
                return true;
            }
        }

        if (highlightOnly) _highlightAxis = -1;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transform computation
    // ─────────────────────────────────────────────────────────────────────────

    private void ComputeEdit(Vector2 point)
    {
        _edit.ClickRay    = GetRay(point);
        _edit.ClickRayPos = GetRayPos(point);
        _edit.Plane       = TransformPlane.View;
        UpdateTransformGizmo();
        _edit.Center   = _gizmoTransform.Origin;
        _edit.Original = _gizmoTransform;
        _visualCenter  = _edit.Center; // initialise so first translate frame is correct

        // Capture per-item originals
        for (int i = 0; i < _selections.Count; i++)
        {
            var item = _selections[i];
            item.TargetGlobal   = GetWorldTransform(item.Object);
            item.TargetOriginal = GetLocalTransform(item.Object);
            _selections[i] = item;
        }
    }

    private Vector3 UpdateTransform(bool shift)
    {
        Vector3 rayPos = GetRayPos(_edit.MousePos);
        Vector3 ray    = GetRay(_edit.MousePos);
        float   snap   = DEFAULT_FLOAT_STEP;

        Transform3D gt = _gizmoTransform;

        switch (_edit.Mode)
        {
            case TransformMode.Scale:
            {
                Vector3 smotionMask = Vector3.Zero;
                Vector3 planeNorm   = Vector3.Zero;
                Vector3 planePt     = _edit.Center;
                bool    splaneMv    = false;

                switch (_edit.Plane)
                {
                    case TransformPlane.View:
                        smotionMask = Vector3.Zero;
                        planeNorm   = GetCameraNormal();
                        break;
                    case TransformPlane.X:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(0));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(smotionMask, Vector3.Cross(smotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.Y:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(1));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(smotionMask, Vector3.Cross(smotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.Z:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(2));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(smotionMask, Vector3.Cross(smotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.YZ:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(2)) + Vector3.Normalize(gt.BasisColumn(1));
                        planeNorm   = Vector3.Normalize(gt.BasisColumn(0));
                        splaneMv    = true;
                        break;
                    case TransformPlane.XZ:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(2)) + Vector3.Normalize(gt.BasisColumn(0));
                        planeNorm   = Vector3.Normalize(gt.BasisColumn(1));
                        splaneMv    = true;
                        break;
                    case TransformPlane.XY:
                        smotionMask = Vector3.Normalize(gt.BasisColumn(0)) + Vector3.Normalize(gt.BasisColumn(1));
                        planeNorm   = Vector3.Normalize(gt.BasisColumn(2));
                        splaneMv    = true;
                        break;
                }

                Vector3? si = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, rayPos, ray);
                if (si == null) break;
                Vector3? sc = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, _edit.ClickRayPos, _edit.ClickRay);
                if (sc == null) break;

                Vector3 smotion = si.Value - sc.Value;
                if (_edit.Plane != TransformPlane.View)
                {
                    if (!splaneMv)
                        smotion = smotionMask * Vector3.Dot(smotionMask, smotion);
                    else if (shift)
                        smotion = smotionMask * Vector3.Dot(smotionMask, smotion);
                }
                else
                {
                    float clickDist  = Vector3.Distance(sc.Value, _edit.Center);
                    float intersDist = Vector3.Distance(si.Value, _edit.Center);
                    if (clickDist == 0) break;
                    float scale = intersDist - clickDist;
                    smotion = new(scale, scale, scale);
                }

                smotion /= Vector3.Distance(sc.Value, _edit.Center);

                bool slocalCoords = UseLocalSpace && _edit.Plane != TransformPlane.View;
                if (Snapping) snap = GetScaleSnap();
                if (slocalCoords)
                    smotion = GizmoMath.BasisTransform(Matrix.Invert(_edit.Original.Basis), smotion);

                smotion = EditScale(smotion);
                Vector3 smSnapped = GizmoMath.Snapped(smotion, snap);
                Message = $"Scaling: ({smSnapped.X:0.###}, {smSnapped.Y:0.###}, {smSnapped.Z:0.###})";
                ApplyTransform(smotion, snap);
                return smotion;
            }

            case TransformMode.Translate:
            {
                Vector3 tmotionMask = Vector3.Zero;
                Vector3 planeNorm   = Vector3.Zero;
                Vector3 planePt     = _edit.Center;
                bool    tplaneMv    = false;

                switch (_edit.Plane)
                {
                    case TransformPlane.View:
                        planeNorm = GetCameraNormal();
                        break;
                    case TransformPlane.X:
                        tmotionMask = Vector3.Normalize(gt.BasisColumn(0));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(tmotionMask, Vector3.Cross(tmotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.Y:
                        tmotionMask = Vector3.Normalize(gt.BasisColumn(1));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(tmotionMask, Vector3.Cross(tmotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.Z:
                        tmotionMask = Vector3.Normalize(gt.BasisColumn(2));
                        planeNorm   = Vector3.Normalize(Vector3.Cross(tmotionMask, Vector3.Cross(tmotionMask, GetCameraNormal())));
                        break;
                    case TransformPlane.YZ:
                        planeNorm = Vector3.Normalize(gt.BasisColumn(0));
                        tplaneMv  = true;
                        break;
                    case TransformPlane.XZ:
                        planeNorm = Vector3.Normalize(gt.BasisColumn(1));
                        tplaneMv  = true;
                        break;
                    case TransformPlane.XY:
                        planeNorm = Vector3.Normalize(gt.BasisColumn(2));
                        tplaneMv  = true;
                        break;
                }

                Vector3? ti = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, rayPos, ray);
                if (ti == null) break;
                Vector3? tc = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, _edit.ClickRayPos, _edit.ClickRay);
                if (tc == null) break;

                Vector3 tmotion = ti.Value - tc.Value;
                if (_edit.Plane != TransformPlane.View && !tplaneMv)
                    tmotion = tmotionMask * Vector3.Dot(tmotionMask, tmotion);

                bool tlocalCoords = UseLocalSpace && _edit.Plane != TransformPlane.View;
                if (Snapping) snap = GetTranslateSnap();
                if (tlocalCoords)
                    tmotion = GizmoMath.BasisTransform(Matrix.Invert(_gizmoTransform.Basis), tmotion);

                tmotion = EditTranslate(tmotion);
                Vector3 tmSnapped = GizmoMath.Snapped(tmotion, snap);
                Message = $"Translating: ({tmSnapped.X:0.###}, {tmSnapped.Y:0.###}, {tmSnapped.Z:0.###})";
                ApplyTransform(tmotion, snap);
                return tmotion;
            }

            case TransformMode.Rotate:
            {
                Vector3 camToObj = _edit.Center - _camera.Position;
                Vector3 planeNorm;
                if (!camToObj.Equals(Vector3.Zero))
                    planeNorm = Vector3.Normalize(camToObj);
                else
                    planeNorm = GetCameraNormal();

                Vector3 planePt    = _edit.Center;
                Vector3 localAxis  = Vector3.Zero;
                Vector3 globalAxis = Vector3.Zero;

                switch (_edit.Plane)
                {
                    case TransformPlane.View:
                        globalAxis = planeNorm;
                        break;
                    case TransformPlane.X:
                        localAxis = Vector3.UnitX;
                        globalAxis = Vector3.UnitX;
                        break;
                    case TransformPlane.Y:
                        localAxis = Vector3.UnitY;
                        globalAxis = Vector3.UnitY;
                        break;
                    case TransformPlane.Z:
                        localAxis = Vector3.UnitZ;
                        globalAxis = Vector3.UnitZ;
                        break;
                }

                if (UseLocalSpace && _edit.Plane != TransformPlane.View)
                    globalAxis = Vector3.Normalize(GizmoMath.BasisTransform(_gizmoTransform.Basis, localAxis));

                Vector3? ri = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, rayPos, ray);
                if (ri == null) break;
                Vector3? rc = GizmoMath.PlaneIntersectsRay(planeNorm, planePt, _edit.ClickRayPos, _edit.ClickRay);
                if (rc == null) break;

                Vector3 curVec = Vector3.Normalize(ri.Value - _edit.Center);
                if (!_edit.InitialClickVector.HasValue)
                {
                    _edit.InitialClickVector       = Vector3.Normalize(rc.Value - _edit.Center);
                    _edit.PreviousRotationVector   = curVec;
                    _edit.AccumulatedRotationAngle = 0f;
                    _edit.DisplayRotationAngle     = 0f;
                }

                float orthThreshold = MathF.Cos(MathHelper.ToRadians(85f));
                bool  axisOrthogonal = MathF.Abs(Vector3.Dot(planeNorm, globalAxis)) < orthThreshold;

                float angle;
                if (axisOrthogonal)
                {
                    _edit.ShowRotationLine = false;
                    Vector3 projAxis  = Vector3.Cross(planeNorm, globalAxis);
                    Vector3 delta     = ri.Value - rc.Value;
                    float   proj      = Vector3.Dot(delta, projAxis);
                    angle = (proj * (MathF.PI / 2f)) / (_gizmoScale * GIZMO_CIRCLE_SIZE);
                }
                else
                {
                    _edit.ShowRotationLine = true;
                    Vector3 clickAxis = Vector3.Normalize(rc.Value - _edit.Center);
                    angle = GizmoMath.SignedAngleTo(clickAxis, curVec, globalAxis);
                }

                if (_edit.PreviousRotationVector.HasValue)
                {
                    float da = GizmoMath.SignedAngleTo(_edit.PreviousRotationVector.Value, curVec, globalAxis);
                    _edit.AccumulatedRotationAngle += da;
                }
                _edit.PreviousRotationVector = curVec;

                if (Snapping)
                {
                    snap = GetRotationSnap();
                    _edit.DisplayRotationAngle = MathHelper.ToRadians(
                        GizmoMath.Snapped(MathHelper.ToDegrees(_edit.AccumulatedRotationAngle), snap));
                }
                else
                {
                    _edit.DisplayRotationAngle = _edit.AccumulatedRotationAngle;
                }

                bool    rlocalCoords = UseLocalSpace && _edit.Plane != TransformPlane.View;
                Vector3 computeAxis  = rlocalCoords ? localAxis : globalAxis;
                Vector3 result       = EditRotate(computeAxis * angle);
                if (result != computeAxis * angle)
                {
                    computeAxis = Vector3.Normalize(result);
                    angle       = result.Length();
                }

                angle   = MathHelper.ToDegrees(angle);
                if (Snapping) angle = GizmoMath.Snapped(angle, snap);
                Message = $"Rotating: {angle:0.###} degrees";
                angle   = MathHelper.ToRadians(angle);

                ApplyTransform(computeAxis, angle);
                return computeAxis * angle;
            }
        }

        return Vector3.Zero;
    }

    private void ApplyTransform(Vector3 motion, float snap)
    {
        bool localCoords = UseLocalSpace && _edit.Plane != TransformPlane.View;

        for (int i = 0; i < _selections.Count; i++)
        {
            var item = _selections[i];
            Transform3D newTransform = ComputeTransform(
                _edit.Mode,
                item.TargetGlobal,
                item.TargetOriginal,
                motion, snap, localCoords,
                _edit.Plane != TransformPlane.View);

            // Write back to SceneObject.
            // NOTE: TargetGlobal / TargetOriginal are intentionally NOT updated here;
            // they hold the pre-drag snapshot captured in ComputeEdit so that every
            // ContinueEdit frame recomputes from the same origin rather than accumulating.
            SceneObject obj = item.Object;

            if (_edit.Mode == TransformMode.Translate)
            {
                // newTransform.Origin is a world-space position because TargetGlobal.Origin
                // is world-space.  Convert back to local space by subtracting the parent's
                // world translation (captured at drag-start in TargetOriginal vs TargetGlobal).
                // localOffset = worldResult - (worldOrigin - localOrigin)
                //             = worldResult - worldOrigin + localOrigin
                Vector3 worldOrigin = item.TargetGlobal.Origin;
                Vector3 localOrigin = item.TargetOriginal.Origin;
                Vector3 newWorldPos = newTransform.Origin;
                Vector3 localPos    = localOrigin + (newWorldPos - worldOrigin);
                obj.SetLocalPosition(localPos);
            }
            else if (_edit.Mode == TransformMode.Rotate)
            {
                obj.SetLocalRotation(MatrixToEulerYXZ(Matrix.Transpose(newTransform.Basis)));
            }
            else if (_edit.Mode == TransformMode.Scale)
            {
                obj.SetLocalScale(ExtractScale(newTransform.Basis));
            }
        }

        // For translate, keep the visual center in sync with the objects so the
        // handles follow in real time.  For rotate/scale the pivot stays fixed.
        if (_edit.Mode == TransformMode.Translate && _selections.Count > 0)
        {
            Vector3 liveCenter = Vector3.Zero;
            foreach (var s in _selections)
                liveCenter += s.Object.GetWorldPosition();
            _visualCenter = liveCenter / _selections.Count;
        }

        UpdateTransformGizmo();
    }

    private Transform3D ComputeTransform(TransformMode mode, Transform3D original,
                                          Transform3D originalLocal, Vector3 motion,
                                          float extra, bool local, bool orthogonal)
    {
        switch (mode)
        {
            case TransformMode.Scale:
            {
                if (Snapping) motion = GizmoMath.Snapped(motion, extra);
                Transform3D s = Transform3D.Identity;
                if (local)
                {
                    Matrix newBasis = originalLocal.Basis;
                    Vector3 scaleVec = motion + Vector3.One;
                    newBasis.M11 *= scaleVec.X; newBasis.M21 *= scaleVec.X; newBasis.M31 *= scaleVec.X;
                    newBasis.M12 *= scaleVec.Y; newBasis.M22 *= scaleVec.Y; newBasis.M32 *= scaleVec.Y;
                    newBasis.M13 *= scaleVec.Z; newBasis.M23 *= scaleVec.Z; newBasis.M33 *= scaleVec.Z;
                    s = new Transform3D(newBasis, originalLocal.Origin);
                }
                else
                {
                    Vector3 sv = motion + Vector3.One;
                    Transform3D baseT = new(Matrix.Identity, _edit.Center);
                    Transform3D inv = baseT.AffineInverse();
                    Vector3 newOrigin = new(
                        sv.X * (original.Origin.X - _edit.Center.X) + _edit.Center.X,
                        sv.Y * (original.Origin.Y - _edit.Center.Y) + _edit.Center.Y,
                        sv.Z * (original.Origin.Z - _edit.Center.Z) + _edit.Center.Z);

                    Matrix newBasis = original.Basis;
                    if (orthogonal)
                        newBasis = GizmoMath.ScaledOrthogonal(newBasis, sv);
                    else
                    {
                        newBasis.M11 *= sv.X; newBasis.M21 *= sv.X; newBasis.M31 *= sv.X;
                        newBasis.M12 *= sv.Y; newBasis.M22 *= sv.Y; newBasis.M32 *= sv.Y;
                        newBasis.M13 *= sv.Z; newBasis.M23 *= sv.Z; newBasis.M33 *= sv.Z;
                    }
                    s = new Transform3D(newBasis, newOrigin);
                }
                return s;
            }

            case TransformMode.Translate:
            {
                if (Snapping) motion = GizmoMath.Snapped(motion, extra);
                if (local)
                    return originalLocal.TranslatedLocal(motion);
                return original.Translated(motion);
            }

            case TransformMode.Rotate:
            {
                Matrix rotMat = GizmoMath.AxisAngle(motion.LengthSquared() > 1e-8f ? Vector3.Normalize(motion) : Vector3.Up, extra);
                if (local)
                {
                    Matrix xnaBasis = Matrix.Transpose(originalLocal.Basis);
                    Matrix xnaRotated = rotMat * xnaBasis;
                    return new Transform3D(Matrix.Transpose(xnaRotated), originalLocal.Origin);
                }
                else
                {
                    Matrix rot2 = rotMat;
                    Vector3 newOrigin = GizmoMath.BasisTransform(rot2, original.Origin - _edit.Center) + _edit.Center;

                    Matrix xnaBasis = Matrix.Transpose(original.Basis);
                    Matrix xnaRotated = rot2 * xnaBasis;
                    Matrix newBasis = Matrix.Transpose(xnaRotated);

                    if (original.Basis != originalLocal.Basis)
                    {
                        Matrix xnaLocalBasis = Matrix.Transpose(originalLocal.Basis);
                        Matrix localToWorldXna = xnaBasis * Matrix.Invert(xnaLocalBasis);
                        Matrix worldToLocalXna = Matrix.Invert(localToWorldXna);
                        Matrix xnaResult = xnaRotated * worldToLocalXna * xnaLocalBasis;
                        newBasis = Matrix.Transpose(xnaResult);
                    }
                    return new Transform3D(newBasis, newOrigin);
                }
            }

            default:
                Console.Error.WriteLine("Gizmo3D#ComputeTransform: Invalid mode");
                return Transform3D.Identity;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Camera helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 GetRayPos(Vector2 screenPos)
    {
        if (_camera == null) return Vector3.Zero;
        return _camera.ProjectRayOrigin(screenPos, _imageMin, _imageSize);
    }

    private Vector3 GetRay(Vector2 screenPos)
    {
        if (_camera == null) return Vector3.Forward;
        return _camera.ProjectRayNormal(screenPos, _imageMin, _imageSize);
    }

    private Vector3 GetCameraNormal()
    {
        if (_camera == null) return -Vector3.UnitZ;
        // Camera's local -Z in world space
        return -Vector3.Normalize(new Vector3(_camera.View.M13, _camera.View.M23, _camera.View.M33));
    }

    private Numerics.Vector2 PointToScreen(Vector3 worldPos)
    {
        if (_camera == null) return default;
        Vector2 v = _camera.UnprojectPosition(worldPos, _imageMin, _imageSize);
        return VectorHelper.MonogameToSystemVec2(v);
    }

    private Vector2 UnprojectPos(Vector3 worldPos)
    {
        if (_camera == null) return Vector2.Zero;
        return _camera.UnprojectPosition(worldPos, _imageMin, _imageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SceneObject transform helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a Transform3D whose <see cref="Transform3D.Origin"/> is the object's
    /// true world-space position (the gizmo/rotation anchor, excluding pivot offset),
    /// and whose <see cref="Transform3D.Basis"/> is the object's local rotation/scale.
    /// Used for gizmo visual placement; drag write-back uses <see cref="GetLocalTransform"/>.
    /// </summary>
    private static Transform3D GetWorldTransform(SceneObject obj)
    {
        Vector3 worldOrigin = obj.GetWorldPosition();

        Matrix rot   = Matrix.CreateFromYawPitchRoll(obj.Rotation.Y, obj.Rotation.X, obj.Rotation.Z);
        Matrix basis = Matrix.Transpose(rot) * Matrix.CreateScale(obj.Scale);

        if (obj.Parent != null)
        {
            Matrix parentWorld = obj.Parent.GetWorldMatrix();
            Matrix parentBasis = new(
                parentWorld.M11, parentWorld.M12, parentWorld.M13, 0,
                parentWorld.M21, parentWorld.M22, parentWorld.M23, 0,
                parentWorld.M31, parentWorld.M32, parentWorld.M33, 0,
                0, 0, 0, 1);
            parentBasis = Matrix.Transpose(parentBasis);
            basis = basis * parentBasis;
        }

        return new Transform3D(basis, worldOrigin);
    }

    private static Transform3D GetLocalTransform(SceneObject obj)
    {
        Matrix rot   = Matrix.CreateFromYawPitchRoll(obj.Rotation.Y, obj.Rotation.X, obj.Rotation.Z);
        Matrix basis = Matrix.Transpose(rot) * Matrix.CreateScale(obj.Scale);
        return new Transform3D(basis, obj.Position);
    }

    private static (Vector3 pos, Vector3 size) GetObjectAabb(SceneObject obj)
    {
        // Unit AABB centred at pivot origin, scaled by obj.Scale
        Vector3 scale = obj.Scale == Vector3.Zero ? Vector3.One : obj.Scale;
        Vector3 size  = scale;
        Vector3 pos   = -size * 0.5f - obj.PivotOffset;
        return (pos, size);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Axis transform for gizmo handle rendering
    // ─────────────────────────────────────────────────────────────────────────

    private Matrix BuildAxisTransform(int axis)
    {
        // Mirrors Godot's UpdateTransformGizmoView:
        //   axisAngle = Identity.LookingAt(gt.Basis[axis], gt.Basis[(axis+1)%3])
        //   axisAngle.Basis *= Scale(gizmoScale)
        //   axisAngle.Origin = gt.Origin
        //
        // Godot's Transform3D.LookingAt(target, up) orients the node so that its
        // LOCAL –Z axis points TOWARD target and its local +Y is up.
        // The mesh shaft is built along local –Z (ivec=(0,0,-1)) so this makes the
        // arrow point along Basis[axis] in world space.
        //
        // For global space (Basis = Identity):
        //   axis=0 → target=(1,0,0),  up=(0,1,0)
        //   axis=1 → target=(0,1,0),  up=(0,0,1)
        //   axis=2 → target=(0,0,1),  up=(1,0,0)

        Vector3 basisAxis = Vector3.Normalize(_gizmoTransform.BasisColumn(axis));
        Vector3 basisUp   = Vector3.Normalize(_gizmoTransform.BasisColumn((axis + 1) % 3));

        // LookAt: –Z toward basisAxis.  Standard CreateLookAt points –Z toward target
        // when used as a view matrix; as a world matrix we need its inverse (= transpose
        // for orthonormal).  Build it directly as a camera-like world matrix:
        //   forward (–Z local) = basisAxis
        //   right              = normalize(basisAxis × basisUp)  (but basisUp may be parallel)
        Matrix orient;
        if (basisAxis.LengthSquared() < 1e-8f ||
            MathF.Abs(Vector3.Dot(basisAxis, basisUp)) > 0.9999f)
        {
            // Degenerate – fall back to identity orientation
            orient = Matrix.Identity;
        }
        else
        {
            // The mesh shaft lives at local –Z (ivec = (0,0,–1)).
            // We need local –Z to map to world +basisAxis, i.e. local +Z maps to –basisAxis.
            // XNA's Matrix Row2 is the world direction of local +Z, so set it to –basisAxis.
            Vector3 zRow  = -basisAxis;                             // local +Z → –basisAxis
            Vector3 right = Vector3.Normalize(Vector3.Cross(basisUp, basisAxis));
            Vector3 yRow  = Vector3.Cross(zRow, right);             // local +Y

            // XNA row-major world matrix: Row0=localX, Row1=localY, Row2=localZ
            orient = new Matrix(
                right.X, right.Y, right.Z, 0,
                yRow.X,  yRow.Y,  yRow.Z,  0,
                zRow.X,  zRow.Y,  zRow.Z,  0,
                0,       0,       0,       1);
        }

        Matrix scale = Matrix.CreateScale(_gizmoScale);
        Matrix trans = Matrix.CreateTranslation(_gizmoTransform.Origin);
        // Apply: orient first (rotate), then scale, then translate.
        return orient * scale * trans;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Virtual override points (match Godot's EditTranslate / EditScale / EditRotate)
    // ─────────────────────────────────────────────────────────────────────────

    protected virtual Vector3 EditTranslate(Vector3 translation) => translation;
    protected virtual Vector3 EditScale(Vector3 scale)           => scale;
    protected virtual Vector3 EditRotate(Vector3 rotation)       => rotation;

    // ─────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────

    private static Color WithAlpha(Color c, float alpha)
        => new(c.R, c.G, c.B, (byte)(MathHelper.Clamp(alpha, 0, 1) * 255));

    private static Color ColorFromHsv(float h, float s, float v, float a)
    {
        // Standard HSV → RGB
        h = h % 1f;
        if (h < 0) h += 1f;
        float hh = h * 6f;
        int   i  = (int)hh;
        float ff = hh - i;
        float p  = v * (1f - s);
        float q  = v * (1f - s * ff);
        float t  = v * (1f - s * (1f - ff));
        float r, g, b;
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255));
    }

    private static float GetHue(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
        if (delta < 1e-6f) return 0;
        float h;
        if (max == r)      h = ((g - b) / delta) % 6f;
        else if (max == g) h = (b - r) / delta + 2f;
        else               h = (r - g) / delta + 4f;
        h /= 6f;
        if (h < 0) h += 1f;
        return h;
    }

    private static uint ToImGuiColor(Color c)
    {
        return ((uint)c.A << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
    }

    /// <summary>
    /// Decompose a rotation-only XNA Matrix back to the (pitch, yaw, roll) = (X, Y, Z)
    /// Euler angles that <c>Matrix.CreateFromYawPitchRoll(Y, X, Z)</c> would produce.
    ///
    /// XNA CreateFromYawPitchRoll(yaw=Y, pitch=X, roll=Z) stores (row-major):
    ///   M32 = -sin(pitch)
    ///   M31 =  sin(yaw)*cos(pitch)    M33 = cos(yaw)*cos(pitch)
    ///   M12 =  sin(roll)*cos(pitch)   M22 = cos(roll)*cos(pitch)
    /// </summary>
    private static Vector3 MatrixToEulerYXZ(Matrix m)
    {
        float pitch = MathF.Asin(-MathHelper.Clamp(m.M32, -1f, 1f));
        float yaw, roll;

        if (MathF.Abs(m.M32) < 0.9999f)
        {
            yaw  = MathF.Atan2(m.M31, m.M33);
            roll = MathF.Atan2(m.M12, m.M22);
        }
        else
        {
            // Gimbal lock – yaw absorbs roll
            yaw  = MathF.Atan2(-m.M13, m.M11);
            roll = 0;
        }
        return new Vector3(pitch, yaw, roll);
    }

    /// <summary>Extract the scale vector from a TRS matrix.</summary>
    private static Vector3 ExtractScale(Matrix m)
    {
        return new Vector3(
            new Vector3(m.M11, m.M21, m.M31).Length(),
            new Vector3(m.M12, m.M22, m.M32).Length(),
            new Vector3(m.M13, m.M23, m.M33).Length());
    }
}
