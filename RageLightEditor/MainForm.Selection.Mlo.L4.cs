using System;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool DrawMloInstanceHelpers_L4(in WorldSelection s, YmapEntityDef mlo, MloArchetype mloa, bool full,
                                               ref Vector3 bbmin, ref Vector3 bbmax, ref bool drawBox)
        {
            if (L4OldHelpers || mlo == null || mloa == null) return false;
            var mlop = mlo.Position;
            var ori = mlo.Orientation;
            var focusRoom = full ? s.MloRoomDef : null;
            var focusPortal = full ? s.MloPortalDef : null;
            bool focused = focusRoom != null || focusPortal != null;
            if (focused) drawBox = false;
            var camLocal = Quaternion.Invert(ori).Multiply(camera.Position - mlop);
            var corners = new Vector3[8];
            Vector3 ToWorld(Vector3 v) => mlop + ori.Multiply(v);
            var winding = PortalRed;
            int drawn = 0;

            if (full && mloa.portals != null)
            {
                for (int ip = 0; ip < mloa.portals.Length; ip++)
                {
                    var portal = mloa.portals[ip];
                    if (portal?.Corners == null || portal.Corners.Length < 3) continue;
                    bool isFocus = ReferenceEquals(portal, focusPortal);
                    bool ofRoom = focusRoom != null && (portal._Data.roomFrom == (uint)focusRoom.Index || portal._Data.roomTo == (uint)focusRoom.Index);
                    bool faint = focused && !isFocus && !ofRoom;
                    uint pf = portal._Data.flags;
                    Vector4 pcol = PortalAqua;
                    if ((pf & 2048u) != 0) pcol = PortalWater;
                    if ((pf & (4u | 16u | 128u | 256u | 512u | 1024u)) != 0) pcol = PortalMirror;
                    if ((pf & 2u) != 0) pcol = PortalLink;
                    if (isFocus) pcol = SelColour;
                    if (faint) pcol = Faint(pcol, 0.35f);
                    int pcl = portal.Corners.Length;
                    var wc = new Vector3[pcl];
                    for (int ic = 0; ic < pcl; ic++) wc[ic] = ToWorld(portal.Corners[ic].XYZ());
                    L4Portal(wc, pcol, winding, markWinding: !faint, px: isFocus ? L4SelEdgePx : L4EdgePx,
                             ghost: faint ? 0f : (isFocus ? L4GhostSelAlpha : L4GhostAlpha * 1.5f),
                             fillAlpha: faint ? 0.03f : (isFocus ? L4PortalSelFillAlpha : L4PortalFillAlpha),
                             arrow: !faint, oneWay: (pf & 1u) != 0, arrowLen: 0.4f);
                    drawn++;
                    if (isFocus)
                    {
                        DrawPortalArrow(mlo, mloa, portal, SelColour);
                        DrawWorldLabel(ToWorld(portal.Center) + new Vector3(0, 0, 0.5f),
                            $"portal {portal.Index}: {portal._Data.roomFrom} -> {portal._Data.roomTo}", MloLabelSel);
                    }
                }
            }
            if (mloa.rooms != null)
            {
                for (int ir = 0; ir < mloa.rooms.Length; ir++)
                {
                    var room = mloa.rooms[ir];
                    if (room == null) continue;
                    bool isFocus = ReferenceEquals(room, focusRoom);
                    bool ofPortal = focusPortal != null && (focusPortal._Data.roomFrom == (uint)ir || focusPortal._Data.roomTo == (uint)ir);
                    bool faint = focused && !isFocus && !ofPortal;
                    if (ir == 0 || room.RoomName == "limbo")
                    {
                        bbmin = room._Data.bbMin; bbmax = room._Data.bbMax;
                        if (isFocus) DrawWorldLabel(mlop + new Vector3(0, 0, 2.2f), $"{room.Index}: {room.RoomName} (the outside)", MloLabelSel);
                        continue;
                    }
                    if (!full) continue;
                    var mn = room.BBMin_CW; var mx = room.BBMax_CW;
                    if (mx.X <= mn.X) continue;
                    var rcol = isFocus ? SelColour : faint ? Faint(SelWhite, 0.30f) : T(UiTheme.Accent, 0.9f);
                    L4BoxCorners(mn, mx, ToWorld, corners);
                    bool inside = camLocal.X >= mn.X && camLocal.X <= mx.X && camLocal.Y >= mn.Y && camLocal.Y <= mx.Y && camLocal.Z >= mn.Z && camLocal.Z <= mx.Z;
                    L4EdgeBox(corners, rcol, isFocus ? L4SelEdgePx : L4EdgePx,
                              ghost: faint ? 0f : (isFocus ? L4GhostSelAlpha : L4GhostAlpha),
                              ticks: !faint, fillAlpha: isFocus ? L4RoomFillAlpha : 0f, camInside: inside);
                    drawn++;
                    if (faint) continue;
                    var top = ToWorld(new Vector3((mn.X + mx.X) * 0.5f, (mn.Y + mx.Y) * 0.5f, mx.Z));
                    DrawWorldLabel(top, $"{room.Index}: {room.RoomName}", isFocus ? MloLabelSel : RoomLabel);
                }
            }
            if (drawn > 0) l4Deferred_U25 = true;
            return true;
        }
    }
}

