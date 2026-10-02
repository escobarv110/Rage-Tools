using System;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class LightDefs
    {
        public static float CoronaView_U25(LightAttributes l, Vector3 lightPos, Vector3 lightDir, Vector3 camPos)
        {
            if (l == null || l.Type != LightType.Spot) return 1.0f;
            var to = camPos - lightPos;
            if (to.LengthSquared() < 1e-8f || lightDir.LengthSquared() < 1e-8f) return 1.0f;
            to.Normalize();
            var d = Vector3.Normalize(lightDir);
            float c = Vector3.Dot(d, to);
            float outer = Math.Max(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
            float inner = Math.Min(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
            float cosO = (float)Math.Cos(outer), cosI = (float)Math.Cos(inner);
            if (cosI - cosO < 1e-4f) return c >= cosO ? 1.0f : 0.0f;
            float t = MathUtil.Clamp((c - cosO) / (cosI - cosO), 0.0f, 1.0f);
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
