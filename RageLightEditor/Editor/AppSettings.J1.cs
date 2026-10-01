namespace RageLightEditor.Editor
{
    public partial class AppSettings
    {
        private static void MigrateGizmoStyle_J1(AppSettings s)
        {
            if (s == null) return;
            if (s.GizmoStyleIndex < 0 || s.GizmoStyleIndex > 3)
                s.GizmoStyleIndex = s.GizmoModern ? 0 : 2;
            if (!s.GizmoCodeWalkerU24) { s.GizmoStyleIndex = 3; s.GizmoCodeWalkerU24 = true; }
            s.GizmoModern = s.GizmoStyleIndex != 2;
        }
    }
}

