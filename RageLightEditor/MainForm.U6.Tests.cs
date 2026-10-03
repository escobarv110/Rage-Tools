using System;
using System.IO;
using System.Linq;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        partial void SeqTest_U6(Action<string, bool, string> check)
        {
            int ran = 0;
            void Check(string what, bool ok, string detail = "") { ran++; check(what, ok, detail); }

            try
            {
                Check("u6: Animation went on the END of the Space enum", (int)LightPanel.Space.Animation == 9,
                      ((int)LightPanel.Space.Animation).ToString());
                Check("u6: ...and nothing before it moved",
                      (int)LightPanel.Space.Light == 0 && (int)LightPanel.Space.Material == 1 &&
                      (int)LightPanel.Space.Cinematic == 2 && (int)LightPanel.Space.Archive == 3 &&
                      (int)LightPanel.Space.World == 4 && (int)LightPanel.Space.Mlo == 5 &&
                      (int)LightPanel.Space.Particles == 6 && (int)LightPanel.Space.NavMesh == 7 &&
                      (int)LightPanel.Space.Terrain == 8, "");

                var order = LightPanel.WorkspaceTabOrder_P1;
                int ai = Array.IndexOf(order, LightPanel.Space.Animation);
                int ci = Array.IndexOf(order, LightPanel.Space.Cinematic);
                Check("u6: the Animations tab is drawn before Cinematic", ai >= 0 && ci == order.Length - 1 && ai < ci,
                      $"anim at {ai}, cinematic at {ci} of {order.Length}");

                var mine = LightPanel.WorkspaceColour_Q3(LightPanel.Space.Animation);
                bool unique = true; string clash = "";
                foreach (LightPanel.Space s in Enum.GetValues(typeof(LightPanel.Space)))
                {
                    if (s == LightPanel.Space.Animation) continue;
                    var c = LightPanel.WorkspaceColour_Q3(s);
                    if (Math.Abs(c.X - mine.X) + Math.Abs(c.Y - mine.Y) + Math.Abs(c.Z - mine.Z) < 0.25f)
                    { unique = false; clash = s.ToString(); }
                }
                Check("u6: the Animations tab has a colour of its own", unique, clash);

                var sc = SceneFor_L3(LightPanel.Space.Animation);
                Check("u6: the Animations section has a scene of its own",
                      sc != null && !ReferenceEquals(sc, lightScene) &&
                      !ReferenceEquals(sc, mloScene) && !ReferenceEquals(sc, SceneFor_L3(LightPanel.Space.Terrain)),
                      sc == null ? "null" : "own");
                bool camOk;
                try { var _ = WorkspaceCameraOf(LightPanel.Space.Animation); camOk = true; } catch { camOk = false; }
                Check("u6: ...and a camera slot behind its tab", camOk, "");

                var t = new UvAnimTrack();
                t.Curve(UvAnimChannel.OffsetV).SetKey(0f, 0f);
                t.Curve(UvAnimChannel.OffsetV).SetKey(2f, 1f);
                t.Evaluate(1f, out var m0, out var m1);
                Check("u6: a scroll halfway through the clip is half a tile along V",
                      Math.Abs(m1.Z - 0.5f) < 1e-4f && Math.Abs(m0.Z) < 1e-6f,
                      $"{m0.Z:0.####} / {m1.Z:0.####}");
                var moved = UvAnimation.Apply(m0, m1, 0.25f, 0.25f);
                Check("u6: ...and that is what the shader's AnimateUVs would produce",
                      Math.Abs(moved.X - 0.25f) < 1e-4f && Math.Abs(moved.Y - 0.75f) < 1e-4f, moved.ToString());

                string dir = Path.Combine(Path.GetTempPath(), "rle_anim");
                Directory.CreateDirectory(dir);
                string ycd = Path.Combine(dir, "seqtest_uv.ycd");
                var clip = new UvAnimClip { Name = "seqtest", Duration = 2.0f, Fps = 30, Loop = true };
                var belt = clip.Add(3, "conveyor_mat");
                UvAnimPresets.All.First(p => p.Name == "Conveyor belt").Apply(belt, clip.Duration);
                var spin = clip.Add(5, "fan_mat");
                UvAnimPresets.All.First(p => p.Name == "Spinning").Apply(spin, clip.Duration);

                bool rt = UvAnimYcd.RoundTrip(clip, ycd, out var rtMsg, out float rtErr);
                Check("u6: an exported .ycd reads back with the same rows", rt,
                      rtMsg.Replace("\n", " | ") + $" (worst {rtErr:0.#######})");
                Check("u6: ...and it is a real file on disk", File.Exists(ycd) && new FileInfo(ycd).Length > 128,
                      File.Exists(ycd) ? new FileInfo(ycd).Length + " bytes" : "missing");
                Check("u6: ...with the .ycd.xml beside it", File.Exists(ycd + ".xml"), "");

                var found = UvAnimYcd.Read(ycd, out var readMsg);
                Check("u6: the written clip carries tracks 17 and 18 (globalAnimUV0 / 1)",
                      found != null && found.Count == 4 &&
                      found.Count(f => f.Track == UvAnimYcd.TrackUV0) == 2 &&
                      found.Count(f => f.Track == UvAnimYcd.TrackUV1) == 2,
                      readMsg);
                Check("u6: ...on bone 0, the way every clip in the game's own archives does",
                      found != null && found.All(f => f.BoneId == 0),
                      found == null ? "-" : string.Join(" ", found.Select(f => f.BoneId + "/" + f.Track)));
                uint key3 = UvAnimYcd.UvClipHash("seqtest", 3), key5 = UvAnimYcd.UvClipHash("seqtest", 5);
                Check("u6: ...and each clip is keyed hash(name) + material + 1",
                      found != null && found.Count(f => f.ClipHash == key3) == 2 &&
                                       found.Count(f => f.ClipHash == key5) == 2 && key3 != key5,
                      found == null ? "-" : string.Join(" ", found.Select(f => f.ClipHash.ToString("X8"))));
                Check("u6: ...and named the way the game names them",
                      UvAnimYcd.ClipNameFor("seqtest", spin) == "pack:/seqtest_uv_5.clip",
                      UvAnimYcd.ClipNameFor("seqtest", spin));
                Check("u6: ...with Unknown10 = 1, which is what all 204 of the game's carry",
                      found != null && found.All(f => f.Unk10 == 1) &&
                      found.All(f => f.Unk1C == UvAnimYcd.UvAnimUnknown1C),
                      found == null ? "-" : $"unk10 {found[0].Unk10} unk1C {found[0].Unk1C:X8}");
                var fanUv0 = found?.FirstOrDefault(f => f.ClipHash == key5 && f.Track == UvAnimYcd.TrackUV0);
                Check("u6: ...and a full spin comes back where it started",
                      fanUv0 != null && Math.Abs(fanUv0.First.X - fanUv0.Last.X) < 1e-3f &&
                      Math.Abs(fanUv0.First.Y - fanUv0.Last.Y) < 1e-3f,
                      fanUv0 == null ? "-" : $"{fanUv0.First.X:0.####}->{fanUv0.Last.X:0.####}");
                Check("u6: ...at the frame count the clip's rate asks for",
                      fanUv0 != null && fanUv0.Frames == clip.FrameCount,
                      $"{fanUv0?.Frames} vs {clip.FrameCount}");

                var empty = new UvAnimClip { Name = "empty", Duration = 1f };
                var w = UvAnimYcd.Write(empty, Path.Combine(dir, "seqtest_empty.ycd"));
                Check("u6: an export with no enabled track refuses, and says why",
                      !w.Ok && (w.Message ?? "").IndexOf("no material", StringComparison.OrdinalIgnoreCase) >= 0,
                      w.Message);

                string proj = Path.Combine(dir, "seqtest_uv.rleuv");
                Check("u6: the project saves", clip.Save(proj, out var saveMsg), saveMsg);
                var back = UvAnimClip.Load(proj, out var loadMsg);
                Check("u6: ...and reopens with its curves intact",
                      back != null && back.Tracks.Count == 2 &&
                      back.Find(3)?.Curve(UvAnimChannel.OffsetV).Keys.Count == 2 &&
                      back.Find(5)?.Curve(UvAnimChannel.Rotation).Keys.Count == 5, loadMsg);

                AnimPreviewTest_U6(Check);

                var probe = new AnimEditor();
                Check("u6: with nothing open the workspace says so", probe.Summary().StartsWith("No model"), probe.Summary());
                probe.HasModel = true;
                probe.ModelName = "probe.ydr";
                probe.Materials.Add(new AnimEditor.MatRow { Index = 0, Name = "cloth", AnimUv = false });
                Check("u6: a model whose presets cannot animate says THAT, in words",
                      probe.Summary().IndexOf("USE_ANIMATED_UVS", StringComparison.Ordinal) >= 0, probe.Summary());
                Check("u6: and ShaderPresets agrees with the preset table both ways",
                      ShaderPresets.SupportsAnimatedUvs("normal") && ShaderPresets.SupportsAnimatedUvs("default.sps") &&
                      !ShaderPresets.SupportsAnimatedUvs("trees") && !ShaderPresets.SupportsAnimatedUvs(""),
                      ShaderPresets.AnimatedUvPresets.Length + " presets declare it");
            }
            catch (Exception ex)
            {
                check("u6: the checks ran without throwing", false, ex.ToString());
            }

            Console.WriteLine($"U6SEQ {ran} checks ran");
        }

        private void AnimPreviewTest_U6(Action<string, bool, string> check)
        {
            var was = panel.Workspace;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "rle_anim");
                Directory.CreateDirectory(dir);
                string ydr = Path.Combine(dir, "anim_test_scene.ydr");
                if (!File.Exists(ydr)) TestSceneGenerator.Run(ydr);

                panel.SwitchWorkspace(LightPanel.Space.Animation);
                bool opened = AnimOpenPath_U6(ydr);
                check("u6: a model opens into the Animations workspace", opened && AnimEd.HasModel,
                      AnimEd.Status ?? "");
                check("u6: ...into ITS scene and nobody else's",
                      SceneFor_L3(LightPanel.Space.Animation).Files.Count > 0 &&
                      !lightScene.Files.Any(f => f.Path == ydr) &&
                      !(mloScene?.Files.Any(f => f.Path == ydr) ?? false),
                      $"anim {SceneFor_L3(LightPanel.Space.Animation).Files.Count}, light {lightScene.Files.Count}");
                if (!opened || AnimEd.Materials.Count == 0)
                {
                    check("u6: the opened model listed its materials", false, "0 materials");
                    return;
                }

                var row = AnimEd.Materials[0];
                AnimEd.SelectedMaterial = 0;
                AnimAddTrack_U6(0);
                var track = AnimEd.SelectedTrack;
                check("u6: adding a track lands on the selected material",
                      track != null && track.MaterialIndex == row.Index, row.Label);
                if (track == null) return;

                AnimEd.Clip.Duration = 2.0f;
                UvAnimPresets.Scroll(track, 2.0f, 0f, 1.0f);
                AnimEd.Playing = false;
                AnimEd.SeekTo(1.0f);
                AnimEd.PreviewEnabled = true;

                AnimSyncMaterials_U6();
                AnimApplyPreview_U6();

                track.Evaluate(1.0f, out var want0, out var want1);
                var meshes = animMats_U6.FirstOrDefault(m => m.Index == row.Index)?.Meshes;
                bool wrote = meshes != null && meshes.Count > 0 &&
                             meshes.All(m => Near_U6(m.AnimUV0, want0) && Near_U6(m.AnimUV1, want1));
                check("u6: the preview writes the animated rows onto the meshes",
                      wrote && AnimEd.LiveMeshes == (meshes?.Count ?? 0),
                      meshes == null || meshes.Count == 0 ? "no meshes"
                          : $"want {want1.Z:0.####}, mesh {meshes[0].AnimUV1.Z:0.####}, live {AnimEd.LiveMeshes}");
                check("u6: ...and it is halfway through the scroll at halfway through the clip",
                      Math.Abs(want1.Z - 1.0f) < 1e-3f, want1.Z.ToString("0.####"));

                AnimEd.SeekTo(0.5f);
                AnimApplyPreview_U6();
                float atHalf = meshes[0].AnimUV1.Z;
                check("u6: scrubbing moves what the shader is handed",
                      Math.Abs(atHalf - want1.Z) > 0.4f, $"{atHalf:0.####} vs {want1.Z:0.####}");

                AnimEd.PreviewEnabled = false;
                AnimApplyPreview_U6();
                var fileUv1 = MaterialEditing.GetValue(animMats_U6.First(m => m.Index == row.Index).Shader,
                                                       (uint)CodeWalker.GameFiles.ShaderParamNames.globalAnimUV1,
                                                       new Vector4(0, 1, 0, 0));
                check("u6: turning the preview off restores the material's own rows",
                      Near_U6(meshes[0].AnimUV1, fileUv1) && AnimEd.LiveMeshes == 0,
                      $"{meshes[0].AnimUV1} vs {fileUv1}");
                AnimEd.PreviewEnabled = true;

                AnimClose_U6();
                check("u6: closing empties the workspace", !AnimEd.HasModel && AnimEd.Materials.Count == 0, "");
            }
            catch (Exception ex)
            {
                check("u6: the preview check ran without throwing", false, ex.ToString());
            }
            finally
            {
                try { panel.SwitchWorkspace(was); } catch { }
            }
        }

        private static bool Near_U6(Vector4 a, Vector4 b) =>
            Math.Abs(a.X - b.X) < 1e-4f && Math.Abs(a.Y - b.Y) < 1e-4f && Math.Abs(a.Z - b.Z) < 1e-4f;
    }
}

