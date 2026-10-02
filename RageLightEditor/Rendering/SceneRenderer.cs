using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SharpDX;
using RageLightEditor.Editor;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using D3DBuffer = SharpDX.Direct3D11.Buffer;

namespace RageLightEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuLight
    {
        public Vector3 Position; public float Intensity;
        public Vector3 Colour; public float Falloff;
        public Vector3 Direction; public float FalloffExponent;
        public Vector3 TangentX; public float ConeInnerAngle;
        public Vector3 TangentY; public float ConeOuterAngle;
        public Vector3 CapsuleExtent; public uint Type;
        public Vector3 CullingPlaneNormal; public float CullingPlaneOffset;
        public uint CullingPlaneEnable; public float ProjTexIndex; public float ShadowSlot; public float ShadowBlur;
        public uint Flags; public float LightPad0, LightPad1, LightPad2;

        public const int MaxLights = 8192;
        public static readonly int SizeInBytes = SharpDX.Utilities.SizeOf<GpuLight>();
        public const int MaxProjTextures = 4;
        public const int MaxShadowSpots = 8;
        public const int MaxShadowCubes = 4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SceneVars
    {
        public Matrix ViewProj;
        public Vector4 CameraPos;
        public Vector4 AmbientColour;
        public uint LightCount;
        public uint RenderMode;
        public float LightsMultiplier;
        public float Exposure;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = GpuLight.MaxShadowSpots)]
        public Matrix[] ShadowMatrices;
        public Vector3 GlobalLightDir; public float GlobalLightHdr;
        public Vector4 LightDirColour;
        public Vector4 LightDirAmbColour;
        public Vector4 LightNaturalAmbUp;
        public Vector4 LightNaturalAmbDown;
        public Vector4 LightArtificialAmbUp;
        public Vector4 LightArtificialAmbDown;
        public uint UseTimecycle;
        public uint UseSunShadow;
        public float SunShadowStrength;
        public float AmbientDownWrap;
        public float OoOnePlusAmbientDownWrap;
        public Vector3 FogColour;
        public float FogDensity;
        public float FogStart;
        public float SceneTime;
        public float BumpTiltLimit;
        public float ScenePad0;
        public float ScenePad1, ScenePad2, ScenePad3;
        public Matrix SunMatrix;
        public Vector4 SunPos;
        public Vector4 ShadowQuality;
        public Vector4 WaterParams;
        public Vector4 WaterFogParams;
        public Vector4 ProjParams;

        public GameFogVars Fog;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SunCascades.MaxCascades)]
        public Matrix[] SunCascadeMatrix;
        public Vector4 SunCascadeDepths;
        public Vector4 SunCascadeTexel;
        public Vector4 SunCascadeParams;
        public Vector4 ClipPlane;
        public Vector4 ReflectionParams;
        public Vector4 ReflectRect0, ReflectRect1;
        public Vector4 ReflectScale;
        public Vector4 EyeRoomParams;
        public Vector4 EyeRoomScales;
        public Vector4 EyeRoomAmbUp;
        public Vector4 EyeRoomAmbDown;
        public Vector4 CycleArtIntAmbUp;
        public Vector4 CycleArtIntAmbDown;
        public Vector4 InteriorAmbParams;
    }

    public class WaterResources
    {
        public ShaderResourceView Bump, Bump2, Fog;
        public Vector4 FogParams = new Vector4(-4000.0f, -4000.0f, 1.0f / 8500.0f, 1.0f / 12000.0f);
        public float FogLightIntensity = 1.0f;
        public Func<(int mode, ShaderResourceView srv)> BeginDepthRead;
        public Action EndDepthRead;
        public Func<ShaderResourceView> BeginColourRead;
    }

    public struct ShadowSetup
    {
        public ShaderResourceView SpotArray;
        public ShaderResourceView CubeArray;
        public Matrix[] SpotMatrices;
        public ShaderResourceView SunMap;
        public Matrix SunMatrix;
        public Vector3 SunPos;
        public bool SunEnabled;
        public float SunStrength;
        public SunCascades Cascades;
        public float SunTexelWorld;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectVars
    {
        public Matrix World;
        public Vector4 MatDiffuse;
        public float Bumpiness;
        public float EmissiveMult;
        public float SpecIntensity;
        public float FadeAlpha;
        public uint HasDiffuseTex;
        public uint HasBumpTex;
        public uint HasSpecTex;
        public uint AlphaMode;
        public Vector3 SpecMapIntMask;
        public uint IsSelectedMesh;
        public uint MeshLightCount;
        public float SpecFalloffMult;
        public float SpecFresnel;
        public float SpecFresnelMult;
        public Vector4 DetailSettings;
        public uint HasDetailTex;
        public uint IsTerrain;
        public uint TerrainBlendMode;
        public uint HasTintPalette;
        public float TintPaletteV;
        public uint DecalKind;
        public float IsMirror;
        public uint HasLayerBump;
        public Vector4 AnimUV0;
        public Vector4 AnimUV1;
        public Vector4 DecalMask;
        public Vector4 AmbientScales;
        public Vector4 ArtIntAmbUp;
        public Vector4 ArtIntAmbDown;
        public Vector4 L2Params;
        public Vector4 FurParams;
        public Vector4 FurParams2;
        public Vector4 FurParams3;
        public Vector4 FurParams4;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPerMeshLights)]
        public uint[] MeshLightIndices;

        public const int MaxPerMeshLights = 64;
    }

    public partial class SceneRenderer : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet modelShader;
        private readonly ConstantBuffer<SceneVars> sceneCB;
        private readonly ConstantBuffer<ObjectVars> objectCB;
        private D3DBuffer lightSB;
        private ShaderResourceView lightSRV;
        private int lightSBCapacity;
        private bool lastWasTerrain;
        private bool lastHadPalette;

        public Vector3 AmbientColour = new Vector3(0.075f, 0.08f, 0.09f);
        public uint RenderMode = 0;
        public bool Wireframe => RenderMode == 8;
        public static uint FrameRenderMode { get; private set; }
        public float LightsMultiplier = 1.0f;
        public float Exposure = 1.0f;
        public float AmbientDownWrap = 1.0f;
        public Vector3 FogColour = new Vector3(0.6f, 0.64f, 0.7f);
        public float FogDensity = 0.0f;
        public float FogStart = 20.0f;
        public bool DebugNoDepth = false;
        public int MsaaSamples = 1;
        public float ShadowSoftness_V68 = 0.35f;
        public bool AlphaToCoverage = true;
        public static bool DisableTint = Environment.GetEnvironmentVariable("RLE_NOTINT") == "1";
        public static bool DisableEnvReflection = Environment.GetEnvironmentVariable("RLE_NOENVREFL") == "1";
        public bool NoShadowJitter = false;
        public bool HighQualityShadows = false;

        public float BumpTiltLimit = 0.0f;

        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public TimecycleData.GlobalLightState? GlobalLight;
        public GameFogVars GameFog;
        public Vector4 EyeRoomParams, EyeRoomScales, EyeRoomAmbUp, EyeRoomAmbDown;

        public SceneRenderer(Device device)
        {
            this.device = device;
            modelShader = new ShaderSet(device, "model.hlsl", new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new InputElement("TANGENT", 0, Format.R32G32B32A32_Float, 24, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 40, 0),
                new InputElement("COLOR", 1, Format.R32G32B32A32_Float, 56, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 72, 0),
                new InputElement("TEXCOORD", 1, Format.R32G32_Float, 80, 0),
            });
            sceneCB = new ConstantBuffer<SceneVars>(device);
            objectCB = new ConstantBuffer<ObjectVars>(device);
            EnsureLightBuffer(64);
        }

        private void EnsureLightBuffer(int count)
        {
            if (lightSB != null && lightSBCapacity >= count) return;
            int cap = Math.Max(64, lightSBCapacity);
            while (cap < count) cap *= 2;

            lightSRV?.Dispose();
            lightSB?.Dispose();
            lightSB = new D3DBuffer(device, new BufferDescription
            {
                SizeInBytes = cap * GpuLight.SizeInBytes,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.BufferStructured,
                StructureByteStride = GpuLight.SizeInBytes,
            });
            lightSRV = new ShaderResourceView(device, lightSB);
            lightSBCapacity = cap;
        }

        public WaterResources Water = new WaterResources();
        public D3DBuffer SceneCBBuffer => sceneCB.Buffer;
        private int waterDepthMode;
        private SceneVars lastScene;

        private void UpdateSceneWaterDepthMode(DeviceContext context, int mode)
        {
            var sc = lastScene;
            sc.WaterParams.Z = mode;
            sceneCB.Update(context, ref sc);
        }

        private static Vector4 ProjParamsFor(Camera camera, DeviceContext context)
        {
            var pm = camera.ProjMatrix;
            float w = 1, h = 1;
            try
            {
                var vps = context.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();
                if (vps.Length > 0) { w = Math.Max(vps[0].Width, 1); h = Math.Max(vps[0].Height, 1); }
            }
            catch { }
            return new Vector4(pm.M33, pm.M43, 1.0f / w, 1.0f / h);
        }

        public void Render(DeviceContext context, Camera camera, IEnumerable<RenderModel> models,
            GpuLight[] lights, int lightCount, RenderMesh selectedMesh = null,
            ShaderResourceView[] projTextures = null, ShadowSetup shadow = default)
        {
            FrameRenderMode = RenderMode;
            context.PixelShader.SetShaderResource(22, Water?.Bump);
            context.PixelShader.SetShaderResource(23, Water?.Bump2);
            context.PixelShader.SetShaderResource(24, Water?.Fog);
            waterDepthMode = 0;
            for (int i = 0; i < GpuLight.MaxProjTextures; i++)
            {
                var srv = (projTextures != null && i < projTextures.Length) ? projTextures[i] : null;
                context.PixelShader.SetShaderResource(3 + i, srv);
            }

            context.PixelShader.SetShaderResource(7, shadow.SpotArray);
            context.PixelShader.SetShaderResource(8, shadow.CubeArray);
            context.PixelShader.SetShaderResource(10, shadow.SunMap);
            var viewProjNow = reflectionPass ? reflViewProj : camera.ViewProjMatrix;
            var camPosNow = reflectionPass ? reflCamPos : camera.Position;
            frameEye_V21 = camPosNow;
            var scene = new SceneVars
            {
                ViewProj = Matrix.Transpose(viewProjNow),
                CameraPos = new Vector4(camPosNow, 1),
                AmbientColour = new Vector4(AmbientColour, 1),
                LightCount = (uint)Math.Min(lightCount, GpuLight.MaxLights),
                RenderMode = RenderMode,
                LightsMultiplier = LightsMultiplier,
                Exposure = Exposure,
                ShadowMatrices = new Matrix[GpuLight.MaxShadowSpots],
                UseTimecycle = GlobalLight.HasValue ? 1u : 0u,
                UseSunShadow = (shadow.SunEnabled && shadow.SunMap != null) ? 1u : 0u,
                SunShadowStrength = shadow.SunStrength,
                AmbientDownWrap = AmbientDownWrap,
                OoOnePlusAmbientDownWrap = 1.0f / Math.Max(1.0f + AmbientDownWrap, 0.001f),
                FogColour = FogColour,
                FogDensity = FogDensity,
                FogStart = FogStart,
                SceneTime = (float)clock.Elapsed.TotalSeconds,
                ScenePad0 = NoShadowJitter ? 1.0f : 0.0f,
                BumpTiltLimit = BumpTiltLimit,
                SunMatrix = Matrix.Transpose(shadow.SunMatrix),
                SunPos = new Vector4(shadow.SunPos, 1),
                ShadowQuality = new Vector4(HighQualityShadows ? 1.0f : 0.0f, LegacySunShadow_R5 ? 1.0f : 0.0f, ShadowSoftness_V68, 0),
                WaterParams = new Vector4(Water?.Bump != null && Water?.Bump2 != null ? 1.0f : 0.0f,
                                          Water?.Fog != null ? 1.0f : 0.0f, 0.0f, Water?.FogLightIntensity ?? 0.9f),
                WaterFogParams = Water?.FogParams ?? Vector4.Zero,
                ProjParams = ProjParamsFor(camera, context),
                Fog = GameFog,
                SunCascadeMatrix = new Matrix[SunCascades.MaxCascades],
                ClipPlane = reflectionPass ? new Vector4(reflPlane.Normal, reflPlane.D) : Vector4.Zero,
                ReflectionParams = new Vector4(reflectionPass ? 1.0f : 0.0f, MirrorDebugView ? 1.0f : 0.0f, 0, 0),
                ReflectRect0 = reflRect[0], ReflectRect1 = reflRect[1],
                ReflectScale = new Vector4(reflUsed[0].X, reflUsed[0].Y, reflUsed[1].X, reflUsed[1].Y),
                EyeRoomParams = EyeRoomParams, EyeRoomScales = EyeRoomScales, EyeRoomAmbUp = EyeRoomAmbUp, EyeRoomAmbDown = EyeRoomAmbDown,
            };
            if (shadow.Cascades != null && shadow.Cascades.Count > 0)
            {
                var cs = shadow.Cascades;
                for (int i = 0; i < SunCascades.MaxCascades; i++)
                    scene.SunCascadeMatrix[i] = Matrix.Transpose(i < cs.Count ? cs.ViewProj[i] : cs.ViewProj[cs.Count - 1]);
                scene.SunCascadeDepths = new Vector4(cs.SplitFar[0], cs.SplitFar[1], cs.SplitFar[2], cs.SplitFar[3]);
                scene.SunCascadeTexel = new Vector4(cs.TexelWorld[0], cs.TexelWorld[1], cs.TexelWorld[2], cs.TexelWorld[3]);
                scene.SunCascadeParams = new Vector4(cs.Count, SunCascades.BlendBetweenCascades, ShadowRenderer.SunSize, 0);
            }
            else
            {
                for (int i = 0; i < SunCascades.MaxCascades; i++) scene.SunCascadeMatrix[i] = Matrix.Transpose(shadow.SunMatrix);
                scene.SunCascadeDepths = new Vector4(1e9f, 1e9f, 1e9f, 1e9f);
                float texel = shadow.SunTexelWorld > 0 ? shadow.SunTexelWorld : 0.05f;
                scene.SunCascadeTexel = new Vector4(texel, texel, texel, texel);
                scene.SunCascadeParams = new Vector4(1, 0, ShadowRenderer.SunSize, 0);
            }
            if (GlobalLight.HasValue)
            {
                var g = GlobalLight.Value;
                scene.GlobalLightDir = g.LightDir;
                scene.GlobalLightHdr = g.LightHdr;
                scene.LightDirColour = g.LightDirColour;
                scene.LightDirAmbColour = g.LightDirAmbColour;
                scene.LightNaturalAmbUp = g.NaturalAmbUp;
                scene.LightNaturalAmbDown = g.NaturalAmbDown;
                scene.LightArtificialAmbUp = g.ArtificialAmbUp;
                scene.LightArtificialAmbDown = g.ArtificialAmbDown;
                scene.CycleArtIntAmbUp = g.ArtificialIntUp;
                scene.CycleArtIntAmbDown = g.ArtificialIntDown;
            }
            scene.InteriorAmbParams = InteriorAmbParams_O2();
            for (int i = 0; i < GpuLight.MaxShadowSpots; i++)
            {
                var m = (shadow.SpotMatrices != null && i < shadow.SpotMatrices.Length)
                    ? shadow.SpotMatrices[i] : Matrix.Identity;
                scene.ShadowMatrices[i] = Matrix.Transpose(m);
            }
            sceneCB.Update(context, ref scene);
            lastScene = scene;

            frameLights = lights;
            frameLightCount = Math.Min(lightCount, GpuLight.MaxLights);
            BuildLightGrid();
            NoteLightStats_U10();
            MeshLightOverflow = false;
            OverflowMeshes = 0;
            MaxLightsPerMesh = 0;
            LightBatchesDrawn_U10 = 0;
            if (!reflectionPass) DrawnMeshes = 0;
            WaterMeshesDrawn = 0;
            TwoSidedMeshes = 0;
            ResetPassStats_J4(reflectionPass);

            EnsureLightBuffer(Math.Max(frameLightCount, 1));
            if (frameLightCount > 0 && lights != null)
            {
                var box = context.MapSubresource(lightSB, 0, MapMode.WriteDiscard,
                    SharpDX.Direct3D11.MapFlags.None);
                try
                {
                    SharpDX.Utilities.Write(box.DataPointer, lights, 0, frameLightCount);
                }
                finally { context.UnmapSubresource(lightSB, 0); }
            }
            context.PixelShader.SetShaderResource(9, lightSRV);

            modelShader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, sceneCB.Buffer);
            context.VertexShader.SetConstantBuffer(1, objectCB.Buffer);
            context.PixelShader.SetConstantBuffer(0, sceneCB.Buffer);
            context.PixelShader.SetConstantBuffer(1, objectCB.Buffer);
            context.PixelShader.SetSampler(0, CommonStates.LinearWrap);
            context.PixelShader.SetSampler(1, CommonStates.PointClamp);
            context.VertexShader.SetSampler(1, CommonStates.PointClamp);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.Rasterizer.State = CommonStates.RasterSolid;
            curRaster = CommonStates.RasterSolid;

            var frustum = new BoundingFrustum(viewProjNow);
            var allMeshes = new List<RenderMesh>();
            foreach (var m in models)
            {
                foreach (var mesh in m.Meshes)
                {
                    if (!mesh.Visible) continue;
                    if (CommonStates.CullingEnabled && frustum.Contains(ref mesh.WorldSphere) == ContainmentType.Disjoint) continue;
                    if (reflectionPass)
                    {
                        if (mesh.IsMirror) continue;
                        if (mesh.AlphaMode == GeomAlphaMode.Water) continue;
                        if (Vector3.Dot(reflPlane.Normal, mesh.WorldSphere.Center) + reflPlane.D < -mesh.WorldSphere.Radius) continue;
                        float reflDist = Vector3.Distance(mesh.WorldSphere.Center, reflCamPos);
                        if (mesh.WorldSphere.Radius < reflMinPixels * reflDist) { ReflectionMeshesSkipped++; continue; }
                        if (reflFloorPass && reflDist > MirrorFloorFarDist && mesh.WorldSphere.Radius < MirrorFloorFarMinRadius) { ReflectionMeshesSkipped++; continue; }
                    }
                    allMeshes.Add(mesh);
                }
            }

            context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
            context.OutputMerger.SetDepthStencilState(DebugNoDepth ? CommonStates.DepthDisabled : CommonStates.DepthDefault);

            if (DebugLog.Enabled)
            {
                var dss = context.OutputMerger.GetDepthStencilState(out int sref);
                DebugLog.Log($"depth state: enabled={dss?.Description.IsDepthEnabled}, func={dss?.Description.DepthComparison}, write={dss?.Description.DepthWriteMask}");
                dss?.Dispose();
                var rtvs = context.OutputMerger.GetRenderTargets(1, out var dsv);
                DebugLog.Log($"bound: rtv={(rtvs != null && rtvs.Length > 0 && rtvs[0] != null)}, dsv={(dsv != null)}");
                if (dsv != null)
                {
                    using (var res = dsv.Resource)
                    using (var tex = res.QueryInterface<SharpDX.Direct3D11.Texture2D>())
                    {
                        DebugLog.Log($"dsv size: {tex.Description.Width}x{tex.Description.Height} fmt={tex.Description.Format}");
                    }
                    dsv.Dispose();
                }
                if (rtvs != null) foreach (var r in rtvs) { if (r != null) { using (var res = r.Resource) using (var tex = res.QueryInterface<SharpDX.Direct3D11.Texture2D>()) { DebugLog.Log($"rtv size: {tex.Description.Width}x{tex.Description.Height} fmt={tex.Description.Format}"); } r.Dispose(); } }
                var vps = context.Rasterizer.GetViewports<SharpDX.Mathematics.Interop.RawViewportF>();
                foreach (var vp in vps) DebugLog.Log($"viewport: {vp.X},{vp.Y} {vp.Width}x{vp.Height} z {vp.MinDepth}..{vp.MaxDepth}");
                var rs = context.Rasterizer.State;
                DebugLog.Log($"raster: {(rs == null ? "null(default)" : $"cull={rs.Description.CullMode} scissor={rs.Description.IsScissorEnabled} depthClip={rs.Description.IsDepthClipEnabled}")}");
                rs?.Dispose();
                DebugLog.Log($"topology: {context.InputAssembler.PrimitiveTopology}");
                DebugLog.Log($"meshes: {allMeshes.Count}, lights: {scene.LightCount}");
            }

            bool wire = Wireframe;
            bool prepass = !DebugNoDepth;
            string profTag = reflectionPass ? "mirror." : "";
            if (prepass)
            {
                ProfBegin(profTag + "prepass");
                context.PixelShader.Set(null);
                context.OutputMerger.SetBlendState(CommonStates.BlendNoColor);
                context.OutputMerger.SetDepthStencilState(CommonStates.DepthDefault);
                foreach (var mesh in allMeshes)
                {
                    if (!Drawable(mesh) || mesh.AlphaMode != GeomAlphaMode.Opaque) continue;
                    if (mesh.FadeAlpha < 0.999f) continue;
                    DrawMeshGeom(context, mesh);
                }
                context.PixelShader.Set(modelShader.PS);
                ProfEnd(profTag + "prepass");
            }

            ProfBegin(profTag + "opaque");
            context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
            SetPassBlend_U10(CommonStates.BlendOpaque, CommonStates.BlendAdditive);
            context.OutputMerger.SetDepthStencilState(prepass ? CommonStates.DepthEqual :
                (DebugNoDepth ? CommonStates.DepthDisabled : CommonStates.DepthDefault));
            foreach (var mesh in allMeshes)
            {
                if (!Drawable(mesh) || mesh.AlphaMode != GeomAlphaMode.Opaque) continue;
                if (mesh.FadeAlpha < 0.999f) continue;
                DrawMesh(context, mesh, mesh == selectedMesh);
            }
            ProfEnd(profTag + "opaque");
            ProfBegin(profTag + "cutout");

            context.OutputMerger.SetDepthStencilState(DebugNoDepth ? CommonStates.DepthDisabled : CommonStates.DepthDefault);
            // A cutout's silhouette is an alpha test - a hard per-pixel yes/no - so MSAA alone
            // leaves every leaf and fence wire stair-stepped however many samples are taken.
            // Alpha to coverage turns that test into partial sample coverage, which is what
            // CodeWalker does for the same pass and the only thing that softens those edges.
            bool a2c = MsaaSamples > 1 && AlphaToCoverage;
            if (a2c) context.OutputMerger.SetBlendState(CommonStates.BlendOpaqueA2C);
            SetPassBlend_U10(a2c ? CommonStates.BlendOpaqueA2C : CommonStates.BlendOpaque, CommonStates.BlendAdditive);
            foreach (var mesh in allMeshes)
            {
                bool fadingOpaque = mesh.AlphaMode == GeomAlphaMode.Opaque && mesh.FadeAlpha < 0.999f;
                if (!Drawable(mesh) || (mesh.AlphaMode != GeomAlphaMode.Cutout && !fadingOpaque)) continue;
                DrawMesh(context, mesh, mesh == selectedMesh);
            }
            if (a2c) context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
            ProfEnd(profTag + "cutout");
            ProfBegin(profTag + "blend");

            var camPos = camPosNow;
            transparent.Clear();
            additive.Clear();
            waterMeshes.Clear();
            foreach (var mesh in allMeshes)
            {
                if (!Drawable(mesh)) continue;
                if (SkipBlend_J4(mesh)) continue;
                if (mesh.AlphaMode == GeomAlphaMode.Decal || mesh.AlphaMode == GeomAlphaMode.Glass)
                    transparent.Add(mesh);
                else if (mesh.AlphaMode == GeomAlphaMode.Water) waterMeshes.Add(mesh);
                else if (mesh.AlphaMode == GeomAlphaMode.Additive) additive.Add(mesh);
            }
            int BackToFront(RenderMesh a, RenderMesh b) =>
                (b.WorldSphere.Center - camPos).LengthSquared()
                    .CompareTo((a.WorldSphere.Center - camPos).LengthSquared());
            transparent.Sort(BackToFront);
            waterMeshes.Sort(BackToFront);

            if (wire)
            {
                context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
                context.OutputMerger.SetDepthStencilState(DebugNoDepth ? CommonStates.DepthDisabled : CommonStates.DepthDefault);
                foreach (var mesh in transparent) DrawMesh(context, mesh, mesh == selectedMesh);
                foreach (var mesh in waterMeshes) DrawMesh(context, mesh, mesh == selectedMesh);
                foreach (var mesh in additive) DrawMesh(context, mesh, mesh == selectedMesh);
                ProfEnd(profTag + "blend");
                return;
            }
            context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);
            context.OutputMerger.SetDepthStencilState(CommonStates.DepthReadOnly);
            SetPassBlend_U10(CommonStates.BlendAlpha, CommonStates.BlendAdditiveAlpha);
            bool multiplyBound = false;
            foreach (var mesh in transparent)
            {
                bool wantMul = mesh.AlphaMode == GeomAlphaMode.Decal && mesh.DecalKind == 5;
                if (wantMul != multiplyBound)
                {
                    context.OutputMerger.SetBlendState(wantMul ? CommonStates.BlendMultiply : CommonStates.BlendAlpha);
                    multiplyBound = wantMul;
                }
                DrawMesh(context, mesh, mesh == selectedMesh);
            }
            if (multiplyBound) context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);

            bool anyWater = waterMeshes.Count > 0;
            bool waterColourBound = false;
            if (anyWater && !reflectionPass && Water?.BeginDepthRead != null)
            {
                if (Water.BeginColourRead != null)
                {
                    var col = Water.BeginColourRead();
                    if (col != null)
                    {
                        context.PixelShader.SetShaderResource(28, col);
                        waterColourBound = true;
                        var sc2 = lastScene; sc2.ScenePad2 = 1.0f; lastScene = sc2;
                    }
                }
                var (mode, srv) = Water.BeginDepthRead();
                waterDepthMode = mode;
                WaterDepthModeUsed = mode;
                if (mode == 1) context.PixelShader.SetShaderResource(25, srv);
                else if (mode == 2) context.PixelShader.SetShaderResource(26, srv);
                if (mode != 0 || waterColourBound) UpdateSceneWaterDepthMode(context, mode);
            }
            if (waterListDbg_U9 && !waterListSaid_U9 && waterMeshes.Count > 0)
            {
                waterListSaid_U9 = true;
                foreach (var wm in waterMeshes)
                    Console.WriteLine($"WATERLIST {(wm.Geometry != null ? "drawable" : "quad")} shader={wm.ShaderName} tex={wm.DiffuseName ?? "-"} " +
                                      $"centre=({wm.WorldSphere.Center.X:0},{wm.WorldSphere.Center.Y:0},{wm.WorldSphere.Center.Z:0.##}) r={wm.WorldSphere.Radius:0} " +
                                      $"box=({wm.WorldBounds.Minimum.X:0},{wm.WorldBounds.Minimum.Y:0})..({wm.WorldBounds.Maximum.X:0},{wm.WorldBounds.Maximum.Y:0})");
            }
            foreach (var mesh in waterMeshes) DrawMesh(context, mesh, mesh == selectedMesh);
            if (waterColourBound)
            {
                context.PixelShader.SetShaderResource(28, null);
                var sc3 = lastScene; sc3.ScenePad2 = 0.0f; lastScene = sc3;
            }
            if (waterDepthMode != 0 || waterColourBound)
            {
                context.PixelShader.SetShaderResource(25, null);
                context.PixelShader.SetShaderResource(26, null);
                Water?.EndDepthRead?.Invoke();
                UpdateSceneWaterDepthMode(context, 0);
                waterDepthMode = 0;
            }

            if (additive.Count > 0)
            {
                context.OutputMerger.SetBlendState(CommonStates.BlendAdditiveAlpha);
                SetPassBlend_U10(CommonStates.BlendAdditiveAlpha, CommonStates.BlendAdditiveAlpha);
                foreach (var mesh in additive) DrawMesh(context, mesh, mesh == selectedMesh);
            }
            ProfEnd(profTag + "blend");
        }

        private static readonly float furBaseOverride_V64 =
            float.TryParse(Environment.GetEnvironmentVariable("RLE_FURBASE"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fb) ? fb : 0.0f;

        private readonly List<RenderMesh> transparent = new List<RenderMesh>();
        private readonly List<RenderMesh> additive = new List<RenderMesh>();
        private readonly List<RenderMesh> waterMeshes = new List<RenderMesh>();
        private static readonly bool waterListDbg_U9 = Environment.GetEnvironmentVariable("RLE_WATERLIST") == "1";
        private bool waterListSaid_U9;

        private static bool Drawable(RenderMesh m) => m.Visible && !m.NeverDraw;

        private void DrawMeshGeom(DeviceContext context, RenderMesh mesh)
        {
            SetRaster(context, mesh);

            var ov = new ObjectVars
            {
                World = Matrix.Transpose(mesh.Transform),
                MeshLightIndices = meshLightScratch,
            };
            if (mesh.IsPedFur_V38 && mesh.FurLayerParams.X > 0.0f &&
                FurFade_V21(mesh, frameEye_V21) > 0.0f)
            {
                ov.FurParams = new Vector4(-1.0f, -1.0f, 1.0f, mesh.FurLayerParams.X);
                ov.FurParams2 = new Vector4(mesh.FurUvScales.X, mesh.FurUvScales.Y, 1.0f, -1.0f);
            }
            objectCB.Update(context, ref ov);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VB, MeshVertex.Stride, 0));
            context.InputAssembler.SetIndexBuffer(mesh.IB, Format.R16_UInt, 0);
            context.DrawIndexed(mesh.IndexCount, 0, 0);
        }

        private int[] candList = new int[256];
        private void EnsureCandList(int n) { if (candList.Length < n) candList = new int[Math.Max(n, candList.Length * 2)]; }
        private readonly uint[] meshLightScratch = new uint[ObjectVars.MaxPerMeshLights];
        private GpuLight[] frameLights;
        private int frameLightCount;

        public Dictionary<RenderMesh, int> MeshLightFingerprints;

        public bool MeshLightOverflow;
        public int OverflowMeshes;
        public int MaxLightsPerMesh;
        public int DrawnMeshes;
        public int WaterMeshesDrawn, WaterDepthModeUsed;
        public int TwoSidedMeshes;

        private const float LightCellSize = 32.0f;
        private readonly Dictionary<long, List<int>> lightGrid = new Dictionary<long, List<int>>();
        private readonly List<List<int>> lightGridPool = new List<List<int>>();
        private int lightGridPoolUsed;
        private readonly int[] lightStamp = new int[GpuLight.MaxLights];
        private int meshStamp;
        private readonly List<int> lightGridWide = new List<int>();
        private const float LightGridWideReach = 400.0f;
        public int LightTestsThisFrame;

        private static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        private void BuildLightGrid()
        {
            lightGrid.Clear();
            lightGridPoolUsed = 0;
            lightGridWide.Clear();
            LightTestsThisFrame = 0;
            if (frameLights == null || frameLightCount == 0) return;
            for (int i = 0; i < frameLightCount; i++)
            {
                ref var l = ref frameLights[i];
                float reach = l.Falloff + (l.Type == 4 ? Math.Abs(l.CapsuleExtent.X) * 0.5f : 0f);
                if (reach > LightGridWideReach || float.IsNaN(reach)) { lightGridWide.Add(i); continue; }
                int x0 = (int)Math.Floor((l.Position.X - reach) / LightCellSize);
                int x1 = (int)Math.Floor((l.Position.X + reach) / LightCellSize);
                int y0 = (int)Math.Floor((l.Position.Y - reach) / LightCellSize);
                int y1 = (int)Math.Floor((l.Position.Y + reach) / LightCellSize);
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        long key = CellKey(cx, cy);
                        if (!lightGrid.TryGetValue(key, out var list))
                        {
                            if (lightGridPoolUsed < lightGridPool.Count) { list = lightGridPool[lightGridPoolUsed]; list.Clear(); }
                            else { list = new List<int>(8); lightGridPool.Add(list); }
                            lightGridPoolUsed++;
                            lightGrid[key] = list;
                        }
                        list.Add(i);
                    }
            }
        }

        private uint BuildMeshLightList(RenderMesh mesh)
        {
            if (frameLights == null) return 0;
            uint n = 0;
            var c = mesh.WorldSphere.Center;
            float mr = mesh.WorldSphere.Radius;
            int overflow = 0;
            meshLightAll_U10.Clear();
            meshLightRest_U10.Clear();

            meshStamp++;
            if (meshStamp == int.MaxValue) { Array.Clear(lightStamp, 0, lightStamp.Length); meshStamp = 1; }
            EnsureCandList(frameLightCount);
            int candidates = 0;
            int cx0 = (int)Math.Floor((c.X - mr) / LightCellSize), cx1 = (int)Math.Floor((c.X + mr) / LightCellSize);
            int cy0 = (int)Math.Floor((c.Y - mr) / LightCellSize), cy1 = (int)Math.Floor((c.Y + mr) / LightCellSize);
            long cells = (long)(cx1 - cx0 + 1) * (cy1 - cy0 + 1);
            if (cells > 4096 || cells <= 0 || float.IsNaN(mr) || lightGrid.Count == 0)
            {
                for (int i = 0; i < frameLightCount; i++) candList[i] = i;
                candidates = frameLightCount;
            }
            else
            {
                foreach (var wi in lightGridWide) { lightStamp[wi] = meshStamp; candList[candidates++] = wi; }
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cx = cx0; cx <= cx1; cx++)
                    {
                        if (!lightGrid.TryGetValue(CellKey(cx, cy), out var list)) continue;
                        for (int k = 0; k < list.Count; k++)
                        {
                            int i = list[k];
                            if (lightStamp[i] == meshStamp) continue;
                            lightStamp[i] = meshStamp;
                            candList[candidates++] = i;
                        }
                    }
            }
            LightTestsThisFrame += candidates;

            for (int ci = 0; ci < candidates; ci++)
            {
                int i = candList[ci];
                ref var l = ref frameLights[i];
                float reach = l.Falloff + (l.Type == 4 ? Math.Abs(l.CapsuleExtent.X) * 0.5f : 0f) + mr;
                var d = l.Position - c;
                float d2 = d.LengthSquared();
                if (d2 > reach * reach) continue;

                float surf = Math.Max((float)Math.Sqrt(d2) - mr, 0.5f);
                float score = (l.Intensity + 0.05f) * l.Falloff / surf;
                score += LightTieBreak(in l) * 1e-3f;

                meshLightAll_U10.Add(((uint)i, score));
            }
            n = SplitLightBatches_U10(meshLightAll_U10, meshLightScratch, meshLightRest_U10);
            overflow = meshLightRest_U10.Count;
            if (overflow > 0)
            {
                MeshLightOverflow = true;
                OverflowMeshes++;
                MaxLightsPerMesh = Math.Max(MaxLightsPerMesh, (int)n + overflow);
            }
            if (MeshLightFingerprints != null)
            {
                int fp = (int)n;
                for (uint k = 0; k < n; k++) fp ^= (int)(meshLightScratch[k] * 2654435761u);
                MeshLightFingerprints[mesh] = fp;
            }
            return n;
        }

        private static float LightTieBreak(in GpuLight l)
        {
            uint h = (uint)(l.Position.X * 37.0f) * 2654435761u ^ (uint)(l.Position.Y * 41.0f) * 2246822519u ^ (uint)(l.Position.Z * 43.0f) * 3266489917u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFF) / 65535.0f;
        }

        private RasterizerState curRaster;

        private void SetRaster(DeviceContext context, RenderMesh mesh)
        {
            bool decal = mesh.AlphaMode == GeomAlphaMode.Decal;
            bool keepsBothFaces = CommonStates.BackfaceMode switch
            {
                1 => mesh.SheetSided,
                2 => false,
                _ => mesh.DoubleSided,
            };
            bool twoSided = keepsBothFaces || !CommonStates.BackfaceCulling;
            if (twoSided) TwoSidedMeshes++;
            var want = Wireframe
                ? (twoSided ? CommonStates.RasterWireframe : CommonStates.RasterWireframeCullBack)
                : twoSided
                    ? (decal ? CommonStates.RasterDecal : CommonStates.RasterSolid)
                    : (decal ? CommonStates.RasterDecalCullBack : CommonStates.RasterSolidCullBack);
            if (!ReferenceEquals(want, curRaster))
            {
                context.Rasterizer.State = want;
                curRaster = want;
            }
        }

        private void DrawMesh(DeviceContext context, RenderMesh mesh, bool selected)
        {
            DrawnMeshes++;
            CountDraw_J4(mesh);
            if (mesh.AlphaMode == GeomAlphaMode.Water) WaterMeshesDrawn++;
            SetRaster(context, mesh);
            uint lcount;
            if (CommonStates.CullingEnabled)
            {
                lcount = BuildMeshLightList(mesh);
            }
            else
            {
                lcount = (uint)Math.Min(frameLightCount, ObjectVars.MaxPerMeshLights);
                for (uint i = 0; i < lcount; i++) meshLightScratch[i] = i;
            }

            float furBase = 0.0f;
            float furFade = 0.0f;
            bool grassFur = mesh.IsFur && !mesh.IsPedFur_V38;
            if (mesh.IsFur)
            {
                furFade = FurFade_V21(mesh, frameEye_V21);
                if (mesh.IsPedFur_V38)
                    furBase = ModelRenderer.PedFurShadow_V38(mesh.PedFurSelfShadowMin_V38, mesh.PedFurAOBlend_V38, 0, ModelRenderer.PedFurLayers_V38(mesh.PedFurMinLayers_V38, mesh.PedFurMaxLayers_V38, 0.0f));
                else
                {
                    ModelRenderer.FurLayer_V21(mesh, 0, out _, out var rung0);
                    float far = ModelRenderer.FurLadderAverage_V21(mesh);
                    furBase = far + (rung0 - far) * furFade;
                }
                if (furBase <= 0.0f || furBase > 1.0f) furBase = 1.0f;
                if (furBaseOverride_V64 > 0.0f) furBase = furBaseOverride_V64;
            }
            var ov = new ObjectVars
            {
                World = Matrix.Transpose(mesh.Transform),
                FurParams = new Vector4(0, 0, furBase, 0),
                FadeAlpha = mesh.FadeAlpha,
                MatDiffuse = mesh.MatDiffuse,
                Bumpiness = mesh.Bumpiness,
                EmissiveMult = mesh.EmissiveMult,
                SpecIntensity = mesh.SpecIntensity,
                HasDiffuseTex = mesh.DiffuseSRV != null ? (mesh.DiffuseSrgbView ? 2u : 1u) : 0u,
                HasBumpTex = mesh.BumpSRV != null ? 1u : 0u,
                HasSpecTex = mesh.SpecSRV != null ? 1u : 0u,
                AlphaMode = (uint)mesh.AlphaMode,
                SpecMapIntMask = mesh.SpecMapIntMask,
                IsSelectedMesh = mesh.Highlight != 0 ? mesh.Highlight : (selected ? 3u : 0u),
                MeshLightCount = lcount,
                SpecFalloffMult = mesh.SpecFalloffMult,
                SpecFresnel = mesh.SpecFresnel,
                SpecFresnelMult = 1.0f,
                DetailSettings = mesh.DetailSettings,
                HasDetailTex = mesh.DetailSRV != null ? 1u : 0u,
                IsTerrain = mesh.IsTerrain ? (mesh.LayersSrgbView ? 2u : 1u) : 0u,
                TerrainBlendMode = (mesh.TerrainBlendMode & 3u) | (mesh.TerrainLayersUseUv1 ? 4u : 0u),
                HasTintPalette = (mesh.TintPaletteSRV != null && !DisableTint) ? mesh.TintMode : 0u,
                TintPaletteV = mesh.TintPaletteV,
                IsMirror = mesh.IsMirror ? 1f + MirrorSlotFor(mesh) : 0f,
                HasLayerBump = (mesh.IsTerrain && mesh.LayerBumpSRV[0] != null) ? 1u : 0u,
                AnimUV0 = mesh.AnimUV0,
                AnimUV1 = mesh.AnimUV1,
                DecalKind = mesh.AlphaMode == GeomAlphaMode.Decal ? mesh.DecalKind : 0u,
                DecalMask = mesh.DecalMask,
                AmbientScales = new Vector4(mesh.NaturalAmbientScale, mesh.ArtificialAmbientScale, mesh.InInterior, mesh.ReflectIntAmb),
                ArtIntAmbUp = mesh.ArtIntAmbUp,
                ArtIntAmbDown = mesh.ArtIntAmbDown,
                L2Params = new Vector4(DisableEnvReflection ? 1f : 0f, mesh.SunScale, 0, 0),
                MeshLightIndices = meshLightScratch,
            };
            if (mesh.IsPedFur_V38 && furFade > 0.0f && mesh.FurLayerParams.X > 0.0f)
            {
                ov.FurParams = new Vector4(-1.0f, -1.0f, 1.0f, mesh.FurLayerParams.X);
                ov.FurParams2 = new Vector4(mesh.FurUvScales.X, mesh.FurUvScales.Y, 1.0f, -1.0f);
            }
            else if (grassFur) SetGrassFurVars_U20(mesh, ref ov, 0, false);
            objectCB.Update(context, ref ov);

            context.PixelShader.SetShaderResource(0, mesh.DiffuseSRV);
            context.PixelShader.SetShaderResource(1, mesh.BumpSRV);
            context.PixelShader.SetShaderResource(2, mesh.SpecSRV);
            context.PixelShader.SetShaderResource(11, mesh.DetailSRV);
            if (grassFur) BindGrassFurTextures_U20(context, mesh, true);

            if (mesh.IsTerrain)
            {
                for (int k = 0; k < 4; k++)
                {
                    context.PixelShader.SetShaderResource(12 + k, mesh.LayerSRV[k]);
                    context.PixelShader.SetShaderResource(16 + k, mesh.LayerBumpSRV[k]);
                }
                context.PixelShader.SetShaderResource(20, mesh.MaskSRV);
            }
            else if (lastWasTerrain)
            {
                for (int k = 12; k <= 20; k++) context.PixelShader.SetShaderResource(k, null);
            }
            lastWasTerrain = mesh.IsTerrain;
            if (mesh.TintPaletteSRV != null || lastHadPalette)
            {
                context.PixelShader.SetShaderResource(21, mesh.TintPaletteSRV);
                context.VertexShader.SetShaderResource(21, mesh.TintPaletteSRV);
            }
            lastHadPalette = mesh.TintPaletteSRV != null;

            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VB, MeshVertex.Stride, 0));
            context.InputAssembler.SetIndexBuffer(mesh.IB, Format.R16_UInt, 0);
            context.DrawIndexed(mesh.IndexCount, 0, 0);

            if (meshLightRest_U10.Count > 0 && CanBatchLights_U10(mesh)) DrawLightBatches_U10(context, mesh, ref ov);

            if (furFade > 0.0f && grassFur) DrawGrassFurShells_U20(context, mesh, ref ov);
            else if (furFade > 0.0f) DrawFurShells_V21(context, mesh, ref ov, furFade);
            else if (grassFur) BindGrassFurTextures_U20(context, mesh, false);
        }

        public void Dispose()
        {
            DisposeMirrors();
            lightSRV?.Dispose();
            lightSB?.Dispose();
            objectCB?.Dispose();
            sceneCB?.Dispose();
            modelShader?.Dispose();
        }
    }
}

