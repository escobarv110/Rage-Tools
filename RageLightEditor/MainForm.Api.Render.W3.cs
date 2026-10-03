using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        internal static bool ServeMode_W3 => Environment.GetEnvironmentVariable("RLE_SERVE") == "1";

        private sealed class ApiRenderRequest_W3
        {
            public ApiJob Job;
            public int Width, Height;
            public string Path;
            public int Settle;
            public bool RestoreCamera;
            public Vector3 CamTarget;
            public float CamDistance, CamYaw, CamPitch;
            public bool Orbit;
            public Vector3 OrbitCentre;
            public float OrbitDistance;
        }

        private readonly Queue<ApiRenderRequest_W3> apiRenders_W3 = new Queue<ApiRenderRequest_W3>();

        partial void RegisterApiVerbs_W3(ApiVerbs api)
        {
            api.Add("api.camera.get",
                "Where the camera is: orbit target, distance, yaw/pitch in degrees, field of view, and the resulting eye point.",
                ApiVerbs.Schema(),
                "{target, distance, yaw, pitch, fov, eye}",
                false, m => CameraJson_W3());

            api.Add("api.camera.set",
                "Put the camera somewhere. Orbit form, the same one --cam takes: a target to look at, how far off, and the angles.",
                ApiVerbs.Schema(("target", "array", "[x,y,z] to orbit around", false),
                                ("distance", "number", "metres from the target", false),
                                ("yaw", "number", "degrees around +Z", false),
                                ("pitch", "number", "degrees above the horizon", false),
                                ("fov", "number", "field of view in degrees", false)),
                "{target, distance, yaw, pitch, fov, eye}",
                true, m =>
                {
                    if (m.Has("target", 0)) camera.Target = m.Vec("target", 0, camera.Target);
                    if (m.Has("distance", 3)) camera.Distance = camera.TargetDistance = Math.Max(0.05f, m.Num("distance", 3, camera.Distance));
                    if (m.Has("yaw", 4)) camera.Yaw = camera.TargetYaw = MathUtil.DegreesToRadians(m.Num("yaw", 4, 0));
                    if (m.Has("pitch", 5)) camera.Pitch = camera.TargetPitch = MathUtil.DegreesToRadians(m.Num("pitch", 5, 0));
                    if (m.Has("fov", 6))
                        camera.FieldOfView = MathUtil.DegreesToRadians(Math.Clamp(m.Num("fov", 6, 50), 1.0f, 170.0f));
                    camera.SnapSmoothing();
                    camera.Update();
                    return CameraJson_W3();
                });

            api.Add("api.camera.frame",
                "Frame what is open - the whole scene, or one light - so a render is looking at something.",
                ApiVerbs.Schema(("light", "integer", "a light index to frame instead of the scene", false)),
                "{target, distance, yaw, pitch, fov, eye}",
                true, m =>
                {
                    bool wasLocked = debugCamLocked;
                    debugCamLocked = false;
                    try
                    {
                        int li = m.Int("light", 0, -1);
                        if (li >= 0)
                        {
                            if (li >= scene.Lights.Count) throw new ApiRefused("no light " + li + " (there are " + scene.Lights.Count + ")");
                            FrameLight(scene.Lights[li]);
                        }
                        else
                        {
                            if (!scene.HasModel) throw new ApiRefused("nothing is open to frame - open_file first");
                            FrameModel();
                        }
                    }
                    finally { debugCamLocked = wasLocked; }
                    camera.SnapSmoothing();
                    camera.Update();
                    return CameraJson_W3();
                });

            api.Add("api.time.set",
                "The hour of day the preview lights with, 0..24 - the same clock the Lights workspace shows.",
                ApiVerbs.Schema(("hour", "number", "0..24, fractions allowed", true)),
                "{hour}",
                true, m =>
                {
                    float h = m.Num("hour", 0, -1);
                    if (h < 0 || h > 24) throw new ApiRefused("hour must be 0..24");
                    panel.PreviewHour = h;
                    return "{\"hour\":" + DccBridgeProtocol.N(panel.PreviewHour) + "}";
                });

            api.Add("api.open_file",
                "Open a file in the editor: .ydr/.yft models, .ytd textures, .ytyp/.ymap imports, projects. Additive keeps what is already open.",
                ApiVerbs.Schema(("path", "string", "a path on disk", true),
                                ("additive", "boolean", "add to the scene instead of replacing it", false)),
                "{opened, files, lights}",
                true, m =>
                {
                    var path = m.Text("path", 0, "");
                    if (string.IsNullOrWhiteSpace(path)) throw new ApiRefused("no path");
                    if (!File.Exists(path)) throw new ApiRefused("no file at '" + path + "'");
                    bool additive = m.Flag("additive", 1, false);
                    if (!additive) scene.CloseAllFiles();
                    LoadFile(path);
                    return "{\"opened\":" + DccBridgeProtocol.S(Path.GetFileName(path)) +
                           ",\"files\":" + scene.Files.Count + ",\"lights\":" + scene.Lights.Count + "}";
                });

            api.Add("api.render.view",
                "Render what the camera is looking at, at any size, through the full pipeline. A job - the done event carries the PNG path.",
                ApiVerbs.Schema(("width", "integer", "pixels (default: the window's)", false),
                                ("height", "integer", "pixels (default: the window's)", false),
                                ("out", "string", "where to write the .png (default: a temp file)", false)),
                "{job} then job-done {path, width, height}",
                false, m => StartRender_W3(api, m, null));

            api.Add("api.render.prop",
                "Open a prop and photograph it: a .ydr/.yft on disk, or an archetype name to pull out of the archives. Frames it, renders, and puts the camera back.",
                ApiVerbs.Schema(("name", "string", "archetype name in the archives, or a path on disk", true),
                                ("width", "integer", "pixels", false),
                                ("height", "integer", "pixels", false),
                                ("out", "string", "where to write the .png", false),
                                ("yaw", "number", "degrees around the prop (default 35)", false),
                                ("pitch", "number", "degrees above it (default 20)", false),
                                ("hour", "number", "clock to light it with, 0..24", false),
                                ("keep", "boolean", "leave it open afterwards instead of restoring the scene", false)),
                "{job} then job-done {path, width, height, prop}",
                true, m =>
                {
                    var name = m.Text("name", 0, "");
                    if (string.IsNullOrWhiteSpace(name)) throw new ApiRefused("no name");
                    string opened = OpenPropForRender_W3(name);

                    if (m.Has("hour", 7)) panel.PreviewHour = Math.Clamp(m.Num("hour", 7, 12), 0.0f, 24.0f);

                    bool wasLocked = debugCamLocked;
                    debugCamLocked = false;
                    try { FrameModel(); } finally { debugCamLocked = wasLocked; }
                    camera.Yaw = camera.TargetYaw = MathUtil.DegreesToRadians(m.Num("yaw", 5, 35));
                    camera.Pitch = camera.TargetPitch = MathUtil.DegreesToRadians(m.Num("pitch", 6, 20));
                    camera.SnapSmoothing();
                    camera.Update();

                    return StartRender_W3(api, m, opened);
                });

            api.Add("api.render.orbit",
                "Several views of what is open, evenly spaced around it - so an agent can see every side rather than guess from one angle.",
                ApiVerbs.Schema(("views", "integer", "how many, 2..16 (default 4)", false),
                                ("width", "integer", "pixels", false),
                                ("height", "integer", "pixels", false),
                                ("out", "string", "a FOLDER for the .png files", false),
                                ("pitch", "number", "degrees above the subject (default 20)", false)),
                "{job} then job-done {paths:[...]}",
                false, m =>
                {
                    if (!scene.HasModel) throw new ApiRefused("nothing is open to orbit - open_file or render.prop first");
                    int views = Math.Clamp(m.Int("views", 0, 4), 2, 16);
                    int w = Math.Clamp(m.Int("width", 1, 1024), 16, 8192);
                    int h = Math.Clamp(m.Int("height", 2, 768), 16, 8192);
                    float pitch = m.Num("pitch", 4, 20);
                    var dir = m.Text("out", 3, "");
                    if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(Path.GetTempPath(), "rage_tools_api");
                    Directory.CreateDirectory(dir);

                    var bounds = scene.GetSceneBounds();
                    var centre = Vector3.Zero; float radius = 1.0f;
                    if (bounds.HasValue && bounds.Value.Minimum.X < bounds.Value.Maximum.X)
                    {
                        centre = (bounds.Value.Minimum + bounds.Value.Maximum) * 0.5f;
                        radius = Math.Max((bounds.Value.Maximum - bounds.Value.Minimum).Length() * 0.5f, 0.5f);
                    }

                    var job = api.NewJob(m, "api.render.orbit");
                    var paths = new List<string>();
                    for (int i = 0; i < views; i++)
                    {
                        float yaw = 360.0f * i / views;
                        var path = Path.Combine(dir, "orbit_" + i.ToString("00") + ".png");
                        paths.Add(path);
                        apiRenders_W3.Enqueue(new ApiRenderRequest_W3
                        {
                            Job = i == views - 1 ? job : null,
                            Width = w,
                            Height = h,
                            Path = path,
                            RestoreCamera = false,
                            CamYaw = MathUtil.DegreesToRadians(yaw),
                            CamPitch = MathUtil.DegreesToRadians(pitch),
                            Orbit = true,
                            OrbitCentre = centre,
                            OrbitDistance = radius * 2.2f,
                        });
                    }
                    orbitPaths_W3 = paths;
                    return ApiVerbs.StartedJson(job);
                });
        }

        private List<string> orbitPaths_W3;

        private string StartRender_W3(ApiVerbs api, DccMessage m, string propName)
        {
            int w = Math.Clamp(m.Int("width", 1, 0), 0, 8192);
            int h = Math.Clamp(m.Int("height", 2, 0), 0, 8192);
            var outPath = m.Text("out", 3, "");
            if (string.IsNullOrWhiteSpace(outPath))
            {
                var dir = Path.Combine(Path.GetTempPath(), "rage_tools_api");
                Directory.CreateDirectory(dir);
                outPath = Path.Combine(dir, "render_" + (mloBridge?.Received ?? 0) + ".png");
            }
            else if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) outPath += ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            bool keep = m.Flag("keep", 8, false);
            var job = api.NewJob(m, propName != null ? "api.render.prop" : "api.render.view");
            apiRenders_W3.Enqueue(new ApiRenderRequest_W3
            {
                Job = job,
                Width = w,
                Height = h,
                Path = outPath,
                RestoreCamera = false,
                CamYaw = camera.Yaw,
                CamPitch = camera.Pitch,
                Settle = propName != null ? 6 : 0,
            });
            renderPropName_W3 = propName;
            renderPropKeep_W3 = keep;
            return ApiVerbs.StartedJson(job);
        }

        private string renderPropName_W3;
        private bool renderPropKeep_W3;
        private bool servicingApiRender_W3;

        private void ServiceApiRender_W3()
        {
            if (apiRenders_W3.Count == 0 || renderingStill || servicingApiRender_W3) return;
            var head = apiRenders_W3.Peek();
            if (head.Settle > 0)
            {
                head.Settle--;
                servicingApiRender_W3 = true;
                try { RenderFrame(); } catch { } finally { servicingApiRender_W3 = false; }
                return;
            }
            var r = apiRenders_W3.Dequeue();

            if (r.Orbit)
            {
                camera.Target = r.OrbitCentre;
                camera.Distance = camera.TargetDistance = r.OrbitDistance;
                camera.MaxDistance = Math.Max(camera.MaxDistance, r.OrbitDistance * 2.0f);
            }
            if (r.Orbit || r.CamYaw != 0.0f || r.CamPitch != 0.0f)
            {
                camera.Yaw = camera.TargetYaw = r.CamYaw;
                camera.Pitch = camera.TargetPitch = r.CamPitch;
                camera.SnapSmoothing();
                camera.Update();
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            string err;
            try { err = RenderStill(r.Width, r.Height, r.Path); }
            catch (Exception ex) { err = ex.Message; }
            if (r.Job == null) return;

            if (err != null) { r.Job.Fail(err); }
            else
            {
                int w = r.Width > 0 ? r.Width : deviceResources.Width;
                int h = r.Height > 0 ? r.Height : deviceResources.Height;
                var sb = new StringBuilder("{");
                if (orbitPaths_W3 != null)
                {
                    sb.Append("\"paths\":[");
                    for (int i = 0; i < orbitPaths_W3.Count; i++) { if (i > 0) sb.Append(','); sb.Append(DccBridgeProtocol.S(orbitPaths_W3[i])); }
                    sb.Append("],");
                    orbitPaths_W3 = null;
                }
                sb.Append("\"path\":").Append(DccBridgeProtocol.S(r.Path))
                  .Append(",\"width\":").Append(w).Append(",\"height\":").Append(h)
                  .Append(",\"seconds\":").Append(DccBridgeProtocol.N((float)sw.Elapsed.TotalSeconds));
                if (renderPropName_W3 != null) sb.Append(",\"prop\":").Append(DccBridgeProtocol.S(renderPropName_W3));
                r.Job.Finish(sb.Append('}').ToString());
            }

            if (renderPropName_W3 != null && !renderPropKeep_W3)
            {
                try { scene.CloseAllFiles(); } catch { }
            }
            renderPropName_W3 = null;
        }

        private string OpenPropForRender_W3(string name)
        {
            string open = File.Exists(name) ? name : null;

            if (open == null)
            {
                var archive = panel?.Archive;
                if (archive == null || !archive.Ready)
                    throw new ApiRefused("no such file on disk, and the archive index is not ready to look a name up");

                var bare = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
                foreach (var ext in new[] { ".ydr", ".yft" })
                {
                    var hits = new List<ArchiveBrowser.Entry>();
                    archive.Find(bare + ext, new[] { ext }, hits, 8);
                    foreach (var hit in hits)
                    {
                        if (!string.Equals(hit.File?.Name, bare + ext, StringComparison.OrdinalIgnoreCase)) continue;
                        var data = ExtractStandalone_W2(hit.File);
                        if (data == null) continue;
                        var dir = Path.Combine(Path.GetTempPath(), "rage_tools_api");
                        Directory.CreateDirectory(dir);
                        open = Path.Combine(dir, hit.File.Name);
                        File.WriteAllBytes(open, data);
                        break;
                    }
                    if (open != null) break;
                }
                if (open == null) throw new ApiRefused("no prop called '" + name + "' on disk or in the archives");
            }

            scene.CloseAllFiles();
            LoadFile(open);
            if (!scene.HasModel)
                throw new ApiRefused("'" + Path.GetFileName(open) + "' did not open as a model");
            return Path.GetFileName(open);
        }

        private string CameraJson_W3()
        {
            return "{\"target\":" + DccBridgeProtocol.V(camera.Target) +
                   ",\"distance\":" + DccBridgeProtocol.N(camera.Distance) +
                   ",\"yaw\":" + DccBridgeProtocol.N(MathUtil.RadiansToDegrees(camera.Yaw)) +
                   ",\"pitch\":" + DccBridgeProtocol.N(MathUtil.RadiansToDegrees(camera.Pitch)) +
                   ",\"fov\":" + DccBridgeProtocol.N(MathUtil.RadiansToDegrees(camera.FieldOfView)) +
                   ",\"eye\":" + DccBridgeProtocol.V(camera.Position) + "}";
        }
    }
}

