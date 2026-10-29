using UnityEditor;
using UnityEngine;

namespace Wobblefolk.TalkMouth.Editor
{
    /// <summary>Scene view gizmos of the selected mouth drivers: hinge point, axis and the arc from closed to fully open.</summary>
    static class MouthDriverGizmos
    {
        static readonly Color AxisColor = new Color(1f, 0.35f, 0.3f);
        static readonly Color ArcColor = new Color(0.45f, 0.9f, 0.45f);
        static readonly Color ArcFill = new Color(0.45f, 0.9f, 0.45f, 0.08f);
        static readonly Color CurrentColor = new Color(1f, 0.85f, 0.3f);

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawJaw(JawBoneMouthDriver driver, GizmoType type)
        {
            Transform jaw = driver.Jaw;
            if (jaw == null) return;

            Vector3 pivot = jaw.position;
            Vector3 axis = jaw.TransformDirection(JawBoneMouthDriver.SafeAxis(driver.Axis)).normalized;
            float current = Application.isPlaying && driver.isActiveAndEnabled ? driver.CurrentAngle : 0f;
            Vector3 rest = Quaternion.AngleAxis(-current, axis) * Reach(jaw, pivot, axis, out float radius);

            DrawAxis(pivot, axis, radius);
            DrawArc(pivot, axis, rest, driver.ClosedAngle, driver.OpenAngle, radius);
            if (Application.isPlaying) DrawCurrent(pivot, axis, rest, current, radius);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawHinge(HingeSplitMouthDriver driver, GizmoType type)
        {
            Vector3 pivot = driver.HingePoint;
            Vector3 axis = driver.HingeAxis;
            float size = HandleUtility.GetHandleSize(pivot) * 0.08f;

            Handles.color = AxisColor;
            Handles.SphereHandleCap(0, pivot, Quaternion.identity, size, EventType.Repaint);

            float axisLength = 0f;
            foreach (HingePart p in driver.Parts)
            {
                if (p == null || p.part == null) continue;
                float current = Application.isPlaying && driver.isActiveAndEnabled ? p.CurrentAngle : 0f;
                Vector3 rest = Quaternion.AngleAxis(-current, axis) * Reach(p.part, pivot, axis, out float radius);
                float sign = p.reverse ? -1f : 1f;
                float closed = driver.ClosedAngle * sign;
                float open = (driver.ClosedAngle + (driver.MaxOpenAngle - driver.ClosedAngle) * p.multiplier) * sign;

                DrawArc(pivot, axis, rest, closed, open, radius);
                if (Application.isPlaying) DrawCurrent(pivot, axis, rest, current, radius);
                axisLength = Mathf.Max(axisLength, radius);
            }

            DrawAxis(pivot, axis, axisLength > 0f ? axisLength : HandleUtility.GetHandleSize(pivot) * 0.5f);
        }

        /// <summary>Direction and distance from the pivot to the middle of the moving mesh, flattened onto the swing plane.</summary>
        static Vector3 Reach(Transform moving, Vector3 pivot, Vector3 axis, out float radius)
        {
            Vector3 offset = Vector3.zero;
            Renderer[] renderers = moving.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                offset = Vector3.ProjectOnPlane(bounds.center - pivot, axis);
                if (offset.sqrMagnitude < 1e-6f) offset = Vector3.ProjectOnPlane(bounds.extents.magnitude * moving.forward, axis);
            }

            if (offset.sqrMagnitude < 1e-6f) offset = Vector3.ProjectOnPlane(moving.forward, axis) * HandleUtility.GetHandleSize(pivot) * 0.5f;
            if (offset.sqrMagnitude < 1e-6f) offset = Vector3.ProjectOnPlane(moving.up, axis) * HandleUtility.GetHandleSize(pivot) * 0.5f;
            radius = offset.magnitude;
            return radius > 1e-6f ? offset / radius : Vector3.forward;
        }

        static void DrawAxis(Vector3 pivot, Vector3 axis, float length)
        {
            Handles.color = AxisColor;
            Handles.DrawLine(pivot - axis * length * 0.6f, pivot + axis * length * 0.6f, 2f);
        }

        static void DrawArc(Vector3 pivot, Vector3 axis, Vector3 rest, float closedAngle, float openAngle, float radius)
        {
            Vector3 from = Quaternion.AngleAxis(closedAngle, axis) * rest;
            float sweep = openAngle - closedAngle;
            Handles.color = ArcFill;
            Handles.DrawSolidArc(pivot, axis, from, sweep, radius);
            Handles.color = ArcColor;
            Handles.DrawWireArc(pivot, axis, from, sweep, radius, 2f);
            Handles.DrawLine(pivot, pivot + from * radius);
            Handles.DrawLine(pivot, pivot + Quaternion.AngleAxis(openAngle, axis) * rest * radius);
        }

        static void DrawCurrent(Vector3 pivot, Vector3 axis, Vector3 rest, float angle, float radius)
        {
            Handles.color = CurrentColor;
            Handles.DrawLine(pivot, pivot + Quaternion.AngleAxis(angle, axis) * rest * radius * 1.05f, 3f);
        }
    }
}
