// =============================================================================
// repulsor.scad: v0 palm-emitter repulsor prop for a Meta Quest 3 Touch Plus
// =============================================================================
// Open-hand ("Iron Man") prop, printed in PETG. The right Touch Plus controller
// rides on the BACK of the hand with its faceplate facing away from the hand.
// A round emitter disc sits in the PALM. A palm band (two 25 mm hook-and-loop
// straps) joins the back-of-hand cradle and the palm disc. A forearm pod holds
// the nicklink board (Phase 4), an optional nRF52840 board and a LiPo.
//
// One size fits adult hands from about the 5th-percentile female to the
// 95th-percentile male (ANSUR II). Nothing needs measuring: the straps take up
// hand size, the palm band slides along the cradle for palm length, and the
// thin cradle wings and palm-disc lugs flex to the hand.
//
// STATUS: v0, NOT TEST-FITTED. Touch Plus numbers come from a community 3D
// scan (see README.md). The saddle has a 1.5 mm gap for a felt or TPU liner.
//
// Units: mm and degrees. Checked with OpenSCAD 2026.10 nightly (manifold and
// CGAL backends).
//
// Export one part (print orientation, no supports needed):
//   openscad -D 'part="cradle"' -o stl/cradle.stl repulsor.scad
// Printable parts: cradle, palm_disc, palm_bezel, diffuser, wrist_pod,
//                  wrist_pod_lid, strap_adjuster, liner
// Preview only:    assembly (default), controller_dummy
// The console prints fit checks ("CHECK") and the nominal controller-to-
// emitter transform ("NOMINAL").
//
// Frames (all right-handed):
//   HAND frame (assembly): origin on the skin at the crease on the back of the
//     wrist, on the midline of the hand. +X toward the fingertips, +Z out of
//     the back of the hand, +Y toward the thumb (right hand). The palm skin is
//     at Z = -hand_thickness.
//   HANDLE frame (controller): origin at the bottom end of the handle, on its
//     axis. +X up the handle toward the head, +Z toward the faceplate side
//     (the back of the handle, where the palm sits in a normal grip),
//     +Y = Z x X (the side with the grip button).
//   Every printable part is modelled in its own print orientation and placed
//   into the HAND frame by the assembly.
// =============================================================================

/* [Part] */
part = "assembly"; // [assembly, cradle, palm_disc, palm_bezel, diffuser, wrist_pod, wrist_pod_lid, strap_adjuster, liner, controller_dummy]
// Left = mirror image of everything (for a left Touch Plus). Untested.
hand = "right"; // [right, left]

/* [Preview] */
// Hand size shown in the assembly (the printed parts are the same for all).
preview_hand = "mid"; // [small, mid, large]
// Wrist extension shown in the assembly (the Iron Man pose uses about 60 to 75).
preview_wrist_ext = 0; // [0:5:80]
show_hand = true;
show_straps = true;
// IR LED keep-out zones (translucent green cones). Nothing may enter them.
show_keepout = true;
// Optional path to the community scan "Q3_controller_full_100k_right.stl"
// (printables.com/model/651296, CC BY-NC; never commit it). Empty = simple dummy.
scan_file = "";

/* [Hand range: ANSUR II, not user measurements] */
// [5th %ile female, midpoint, 95th %ile male], computed from the public ANSUR II
// data (2012 US Army anthropometric survey). See README.md.
range_breadth = [72, 84, 96];       // hand breadth across the knuckles
range_circ = [173, 201, 230];       // hand circumference at the knuckles
range_palm_len = [100, 113, 127];   // wrist crease to middle-finger crease, palm side
range_wrist_circ = [142, 166, 191]; // wrist circumference
// Derived (see README.md): thickness from breadth and circumference (stadium
// section); knuckle line about 20 mm short of the finger crease; palm-disc
// centre at about 0.45 x palm length.
range_thick = [25, 29, 34];
range_dorsum = [80, 93, 107];
range_palm_c = [45, 51, 57];

/* [Touch Plus controller: scan-derived design values] */
// Handle width side to side at its widest (scan 31.5).
handle_w = 31.5;
// Handle depth, trigger side to back (scan 42.2).
handle_d = 42.2;
// Length over which the handle's bottom end rounds off to full size (scan about 28).
handle_cap_l = 28;
// Cross-section squareness (2 = ellipse). 2.5 envelopes the scanned front half best.
handle_n = 2.5;
// The handle's cross-section centre drifts slightly off the axis (y0, dy/dx, z0, dz/dx).
handle_center = [2.6, -0.084, -1.2, 0.027];
// Start of the grip button along the handle, from the bottom end (scan about 50).
grip_pad_x = 50;
// Length of the handle part of the dummy (preview).
handle_len = 66;
// Overall length, handle bottom to the nose of the head (scan 126).
ctrl_length = 126;
// Trigger tip position along the handle and in front of its axis (scan 92, 37).
trigger_x = 92;
trigger_drop = 37;
// Faceplate centre in the HANDLE frame (scan; preview and keep-out only).
face_center = [103, 8, 3.5];
// Angle between the handle axis and the faceplate normal (scan 58).
face_tilt = 58;
// Faceplate diameter including the rim (scan about 64).
face_d = 64;
// Head depth below the faceplate (preview only).
head_depth = 30;
// Mass with an AA battery, for the README weight budget (g).
ctrl_mass = 126;

/* [Controller placement on the back of the hand] */
// Handle bottom end, from the wrist crease toward the fingers.
ctrl_x = 18;
// Sideways shift of the controller, + toward the thumb.
ctrl_y = 0;
// Height of the handle axis (at its bottom end) above the plate top.
ctrl_lift = 24;
// Nose-up angle. Larger = faceplate faces more straight out of the hand, head sits higher.
ctrl_pitch = 12;
// Roll of the faceplate toward the thumb (toward the headset in the Iron Man pose).
ctrl_roll = 0;

/* [Cradle] */
// Plate starts this far past the wrist crease (room for wrist extension).
plate_x0 = 10;
// Plate end: 3 mm short of the knuckles of the smallest hand in range.
plate_x1 = 77;
// Plate width at the wrist end and at the knuckle end (wings flex down on narrow hands).
plate_w_prox = 64;
plate_w_dist = 72;
// Plate thickness under the saddle, and at the flexible side wings.
plate_t = 4;
wing_t = 2.6;
plate_corner = 8;
// Foam or TPU pad under the plate (preview spacing; the "liner" part is one option).
pad_t = 3;
// Part of the handle the saddle supports, from the handle bottom (keep below grip_pad_x).
saddle_x = [6, 48];
saddle_wall = 3;
// Saddle side walls stop this far below the handle axis.
saddle_top_z = -5;
// Radial gap between handle and saddle (room for 1 to 1.5 mm felt, foam or TPU).
fit_gap = 1.5;
// Controller strap stations along the handle (centre of each strap, from the handle bottom).
ctrl_strap_xh = [17, 37];
// Controller strap width (13 mm = common half-inch hook-and-loop ties).
ctrl_strap_w = 13;

/* [Straps and palm band] */
// Hand strap width. Slots are sized for 25 mm; 20 mm straps also work.
strap_w = 25;
strap_t = 2;
// Clearance added to the strap width in every slot (PETG).
slot_clear = 1.2;
// Palm band travel along the cradle: band centre from band_x[0] to band_x[1].
band_x = [44, 58];
// Bar between a band slot and the plate edge (the strap wraps it).
slot_edge = 6;
// Pitch of the index ridges on the band bars (count ridges to set both sides alike).
index_pitch = 4;
// Strap lugs on the palm disc and wrist pod.
lug_t = 3;
lug_wall = 3.5;

/* [Palm disc] */
disc_d = 54;
disc_floor = 1.6;
// Free height under the LED ring (speaker, wiring, side button).
disc_cavity_h = 5;
// LED ring: Adafruit NeoPixel Ring 12 is 36.8 OD / 23.3 ID (check the ring you buy).
ring_od = 36.8;
ring_id = 23.3;
// PCB plus LED height of the ring.
ring_t = 3.6;
// Air gap between the LEDs and the diffuser (more = softer light).
ring_gap = 1.5;
diffuser_t = 1.6;
bezel_t = 2;
// Micro speaker behind the centre grille (15 mm round, 4 mm tall assumed).
spk_d = 15;
spk_h = 4;
// Phase 4 button: hole through the side wall facing the fingers (0 deg), for a side-push tactile switch.
button_angle = 0;
button_hole_d = 3.5;
// Cable exit (0 deg = toward the fingers, 90 = toward the little finger for a right hand).
cable_angle = 160;
cable_d = 4.5;

/* [Electronics bay (wrist pod): nicklink v1.3] */
// Board outline: X along the USB edge, Y away from it, thickness (nicklink.kicad_pcb).
nl_board = [33.99, 25.5, 1.6];
// Mounting holes, top view, from the USB-edge left corner (X right, Y away from USB).
nl_holes = [[9.93, 2.05], [24.06, 2.05], [9.93, 23.45], [24.06, 23.45]];
// USB-C receptacle (GCT USB4085): centre along the top edge, overhang past the edge, height.
nl_usb_x = 17.0;
nl_usb_overhang = 2;
nl_usb_h = 3.3;
// RESET (SW1), BOOT (SW2), status LEDs and IMU (U4) centres, same coordinates.
nl_reset = [24.45, 13.76];
nl_boot = [9.58, 13.90];
nl_led = [11.4, 8.9];
nl_imu = [24.2, 17.71];
// Pin stubs under the board (through-hole headers and USB).
nl_below = 3;
// Room above the board for the 2x10 headers plus Dupont or JST wiring.
header_clear = 11;
// Board fixing: M2 heat-set inserts (PETG) or M2 self-tapping screws.
nl_mount = "insert"; // [insert, selftap]
insert_d = 3.2;
insert_h = 3.5;
// Optional BLE board slot (Seeed XIAO nRF52840: 21 x 17.8 mm, USB-C on a short edge).
bay_ble = true;
ble_board = [21, 17.8, 1.2];
// LiPo cell, e.g. a 502030 (about 250 mAh): length, width, thickness.
lipo = [30, 20, 5];
pod_wall = 2.2;
pod_floor = 2;
pod_lid_t = 2;
// Gap from the pod's hand end to the wrist crease (keeps it clear of the controller when the wrist bends back).
pod_gap = 70;
pod_switch = true;

/* [Fasteners] */
// M2 self-tapping screws into PETG.
pilot_d = 1.7;
screw_clear_d = 2.4;
screw_head_d = 4.0;
screw_head_h = 1.4;
screw_depth = 6;

/* [Unity transform estimate] */
// Distance from the handle bottom to the OpenXR grip-pose origin along the handle axis (estimate, +/-15).
oxr_grip_x = 45;

/* [Quality] */
fs_export = 0.5;

// -----------------------------------------------------------------------------
// Derived values
// -----------------------------------------------------------------------------
$fa = 3;
$fs = $preview ? 1.2 : fs_export;

function pick(v) = preview_hand == "small" ? v[0] : preview_hand == "large" ? v[2] : v[1];
hand_breadth = pick(range_breadth);
hand_thickness = pick(range_thick);
dorsum_length = pick(range_dorsum);
palm_center_x = pick(range_palm_c);
wrist_width = pick(range_wrist_circ) * 0.36;
// Where the palm band sits in the preview: at the palm centre, within its travel.
band_pos = max(band_x[0], min(band_x[1], palm_center_x));

plate_z0 = pad_t;                 // plate underside (HAND frame)
plate_top = pad_t + plate_t;      // plate top under the saddle (HAND frame)
slot_w = strap_t + 2;             // slot opening across the strap thickness
slot_l = strap_w + 2 * slot_clear;
saddle_half_w = handle_w / 2 + fit_gap + saddle_wall;
core_half_w = saddle_half_w + 1;  // full-thickness strip under the saddle

disc_r = disc_d / 2;
z_ring = disc_floor + disc_cavity_h;
body_h = z_ring + ring_t + ring_gap;
disc_total_h = body_h + diffuser_t + bezel_t;
bore_r = ring_od / 2 + 1.6;
screw_r = disc_r - 2.8;
diffuser_r = screw_r - 2.4;
bezel_open_r = ring_od / 2 + 0.5;
disc_screw_angles = [60, 180, 300];
rib_angles = [45, 120, 240, 315];
lug_y = disc_r + 0.5 + slot_w / 2;      // strap slot centre, palm disc

// Wrist pod layout (pod frame: x along the forearm toward the hand, y = HAND Y, z = 0 on the skin).
nl_standoff_h = nl_below + 1;
nl_x0 = pod_wall + nl_usb_overhang + 0.5;              // board's USB edge
ble_zone_l = max(lipo[0], ble_board[1]) + 3;
pod_in = [nl_x0 - pod_wall + nl_board[1] + 3 + ble_zone_l,
          nl_board[0] + 2,
          ceil(nl_standoff_h + nl_board[2] + header_clear)];
pod_out = [pod_in[0] + 2 * pod_wall, pod_in[1] + 2 * pod_wall, pod_floor + pod_in[2]];
ble_cx = nl_x0 + nl_board[1] + 3 + ble_zone_l / 2;     // centre of the LiPo / BLE zone
// nicklink board point (X along the USB edge, Y away from it) -> pod frame.
// Board +X (toward J1) points to +y (thumb side), board +Y (away from USB) to +x (toward the hand).
function nl_to_pod(p) = [nl_x0 + p[1], -nl_board[0] / 2 + p[0]];

face_n = [cos(face_tilt), 0, sin(face_tilt)];

c_red = [0.62, 0.07, 0.06];
c_gold = [0.86, 0.66, 0.22];
c_glow = [0.62, 0.92, 1.0];
c_ctrl = [0.25, 0.25, 0.27];
c_skin = [0.93, 0.76, 0.65];
c_pcb = [0.1, 0.35, 0.15];

// -----------------------------------------------------------------------------
// Helpers
// -----------------------------------------------------------------------------
function lerp(a, b, t) = a + (b - a) * t;
function clamp01(t) = max(0, min(1, t));
function rx(a) = [[1, 0, 0], [0, cos(a), -sin(a)], [0, sin(a), cos(a)]];
function ry(a) = [[cos(a), 0, sin(a)], [0, 1, 0], [-sin(a), 0, cos(a)]];
function tp(m) = [for (j = [0 : 2]) [for (i = [0 : 2]) m[i][j]]];
function m44(R, t) = [[R[0][0], R[0][1], R[0][2], t[0]],
                      [R[1][0], R[1][1], R[1][2], t[1]],
                      [R[2][0], R[2][1], R[2][2], t[2]],
                      [0, 0, 0, 1]];
function fmt(v, d = 1) = let(k = pow(10, d)) [for (x = v) round(x * k) / k];

// Rounded rectangle, centred.
module rrect(size, r) {
    rr = min(r, size[0] / 2 - 0.01, size[1] / 2 - 0.01);
    offset(r = rr) square([size[0] - 2 * rr, size[1] - 2 * rr], center = true);
}

// Strap slot (flat strap), centred, long side along X.
module slot2d() { rrect([slot_l, slot_w], 1); }
module lug_pad() { linear_extrude(lug_t) rrect([slot_l + 2 * lug_wall, slot_w + 2 * lug_wall], 2.5); }
module lug_cut() { translate([0, 0, -1]) linear_extrude(lug_t + 2) slot2d(); }

module handed() {
    if (hand == "left") mirror([0, 1, 0]) children();
    else children();
}

// -----------------------------------------------------------------------------
// Touch Plus model (HANDLE frame)
// -----------------------------------------------------------------------------
// Handle size factor along its axis: ellipsoidal bottom cap, then constant.
function hscale(x) = x >= handle_cap_l ? 1 : sqrt(max(0, 1 - pow(1 - x / handle_cap_l, 2)));
// Superellipse outline; a = half depth (handle Z), b = half width (handle Y).
function sellipse(a, b, n, N = 72) =
    [for (i = [0 : N - 1]) let(t = 360 * i / N, c = cos(t), s = sin(t))
        [a * sign(c) * pow(abs(c), 2 / n), b * sign(s) * pow(abs(s), 2 / n)]];
function hc_y(x) = handle_center[0] + handle_center[1] * x;
function hc_z(x) = handle_center[2] + handle_center[3] * x;

module handle_slice(x, g) {
    s = hscale(x);
    translate([x, hc_y(x), hc_z(x)]) rotate([0, 90, 0])
        linear_extrude(0.01) polygon(sellipse(handle_d / 2 * s + g, handle_w / 2 * s + g, handle_n));
}

// Handle envelope grown by g, from the bottom end to x_end.
module handle_envelope(g = 0, x_end = 66) {
    st = concat([0.4], [for (k = [1 : 12]) handle_cap_l * pow(k / 12, 1.6)], [x_end]);
    for (i = [0 : len(st) - 2]) hull() { handle_slice(st[i], g); handle_slice(st[i + 1], g); }
}

module orient_face() { translate(face_center) rotate([0, 90 - face_tilt, 0]) children(); }

// Simplified Touch Plus for previews. Not exact.
module touch_plus_dummy() {
    color(c_ctrl) {
        handle_envelope(0, handle_len);
        hull() {
            orient_face() translate([0, 0, -2]) cylinder(h = 2, d = face_d);
            orient_face() translate([0, 0, -head_depth]) cylinder(h = 1, d = face_d * 0.62);
            handle_slice(handle_len - 8, 0);
        }
        hull() for (p = [[trigger_x - 8, 4, -22], [trigger_x + 6, 4, -24], [trigger_x, 4, -trigger_drop + 3]])
            translate(p) scale([1, 1.8, 1]) sphere(3, $fn = 16);
    }
    color("black") orient_face() {
        translate([4, -12, 0]) cylinder(h = 6.5, d = 10);          // thumbstick
        for (p = [[-10, 8], [6, 14]]) translate(p) cylinder(h = 2, d = 8);  // A / B
    }
    // Grip button area (keep straps and saddle off it)
    color("orange", 0.6) translate([grip_pad_x, handle_w / 2 - 1, -12]) cube([20, 2, 24]);
}

module controller_model() {
    if (scan_file != "")
        color(c_ctrl) multmatrix(scan_to_handle) import(scan_file);
    else
        touch_plus_dummy();
}

// Maps "Q3_controller_full_100k_right.stl" (Printables 651296) into the HANDLE frame.
scan_to_handle = [[-0.703889, -0.112661, -0.701318, 238.036878],
                  [ 0.241005,  0.890893, -0.385003, 152.130042],
                  [ 0.668174, -0.440020, -0.599938, 349.303858],
                  [ 0, 0, 0, 1]];

// IR LED keep-out (assumed, not from Meta): a cone off the faceplate and one
// off the handle bottom, half-angles measured from the surface normal.
keepout_face = 60;
keepout_bottom = 45;
module keepout_zones(h = 14) {
    color([0.2, 0.9, 0.3], 0.18) {
        orient_face() cylinder(h = h, r1 = face_d / 2 + 1, r2 = face_d / 2 + 1 + h * tan(keepout_face));
        rotate([0, -90, 0]) cylinder(h = h, r1 = 7, r2 = 7 + h * tan(keepout_bottom));
    }
}

// -----------------------------------------------------------------------------
// Controller placement (HANDLE frame -> HAND frame)
// -----------------------------------------------------------------------------
ctrl_R = ry(-ctrl_pitch) * rx(-ctrl_roll);
ctrl_T = [ctrl_x, ctrl_y, plate_top + ctrl_lift];
module place_ctrl() multmatrix(m44(ctrl_R, ctrl_T)) children();
function to_hand(p) = ctrl_R * p + ctrl_T;

// -----------------------------------------------------------------------------
// Cradle (back-of-hand plate + controller saddle), HAND frame
// -----------------------------------------------------------------------------
module plate_outline2d() {
    hull() for (sy = [-1, 1]) {
        translate([plate_x0 + plate_corner, sy * (plate_w_prox / 2 - plate_corner)]) circle(r = plate_corner);
        translate([plate_x1 - plate_corner, sy * (plate_w_dist / 2 - plate_corner)]) circle(r = plate_corner);
    }
}
function plate_w_at(x) = lerp(plate_w_prox, plate_w_dist, clamp01((x - plate_x0) / (plate_x1 - plate_x0)));
function band_slot_y(x) = plate_w_at(x) / 2 - slot_edge - slot_w / 2;
band_slot_x = [band_x[0] - slot_l / 2, band_x[1] + slot_l / 2];

// Long palm-band slots, one per side, parallel to the plate edge.
module band_slots2d() {
    for (sy = [-1, 1]) hull() for (x = [band_slot_x[0] + slot_w / 2, band_slot_x[1] - slot_w / 2])
        translate([x, sy * band_slot_y(x)]) circle(d = slot_w, $fn = 24);
}

// Plate: full thickness under the saddle, thin flexible wings outboard.
module plate() {
    inner_inset = slot_edge + slot_w + 1.5;
    translate([0, 0, plate_z0]) {
        hull() {
            linear_extrude(plate_t) intersection() {
                plate_outline2d();
                translate([0, -core_half_w]) square([200, 2 * core_half_w]);
            }
            linear_extrude(wing_t) offset(delta = -inner_inset) plate_outline2d();
        }
        linear_extrude(wing_t) plate_outline2d();
        // Index ridges on top of the band bars (the folded strap catches on them).
        for (sy = [-1, 1]) for (x = [band_slot_x[0] + 2 : index_pitch : band_slot_x[1] - 2]) {
            yb = sy * (band_slot_y(x) + slot_w / 2 + slot_edge / 2);
            translate([x, yb, wing_t]) rotate([90, 0, 0])
                cylinder(h = slot_edge - 1.5, r = 0.6, center = true, $fn = 12);
        }
    }
}

module saddle_block() {
    intersection() {
        place_ctrl() translate([saddle_x[0], -saddle_half_w, -120])
            cube([saddle_x[1] - saddle_x[0], 2 * saddle_half_w, 120 + saddle_top_z]);
        translate([plate_x0, -80, plate_top - 0.5]) cube([plate_x1 - plate_x0, 160, 120]);
    }
}

// Tunnels under the handle for the two controller straps (axis along Y), placed
// where the strap's cross-section plane meets the plate top.
function tunnel_x(xh) = ctrl_T[0] + xh / cos(ctrl_pitch) + ctrl_lift * tan(ctrl_pitch);
module ctrl_strap_tunnels() {
    for (xh = ctrl_strap_xh)
        translate([tunnel_x(xh) - ctrl_strap_w / 2 - slot_clear, -core_half_w - 1, plate_top - 0.01])
            cube([ctrl_strap_w + 2 * slot_clear, 2 * core_half_w + 2, slot_w]);
}

module cradle_hand() {
    difference() {
        union() { plate(); saddle_block(); }
        place_ctrl() handle_envelope(fit_gap, saddle_x[1] + 30);
        ctrl_strap_tunnels();
        translate([0, 0, plate_z0 - 1]) linear_extrude(plate_t + 2) band_slots2d();
    }
}

module cradle() { translate([-(plate_x0 + plate_x1) / 2, 0, -plate_z0]) cradle_hand(); }

// Optional TPU 95A pad under the plate, with matching band slots.
module liner() {
    linear_extrude(2) difference() {
        translate([-(plate_x0 + plate_x1) / 2, 0]) offset(delta = -0.5) plate_outline2d();
        translate([-(plate_x0 + plate_x1) / 2, 0]) offset(delta = 0.6) band_slots2d();
    }
}

// -----------------------------------------------------------------------------
// Palm disc: body (cup), bezel and diffuser. Disc frame: z = 0 on the palm.
// -----------------------------------------------------------------------------
module palm_disc() {
    ch = 1.0;   // chamfer on the palm-side edge
    difference() {
        union() {
            hull() {
                translate([0, 0, ch]) cylinder(h = body_h - ch, r = disc_r);
                cylinder(h = body_h, r = disc_r - ch);
            }
            // One flexible strap lug per side for the palm band.
            for (sy = [-1, 1]) translate([0, sy * lug_y, 0]) lug_pad();
        }
        translate([0, 0, disc_floor]) cylinder(h = body_h, r = bore_r);
        for (sy = [-1, 1]) translate([0, sy * lug_y, 0]) lug_cut();
        for (a = disc_screw_angles) rotate(a) translate([screw_r, 0, body_h - screw_depth]) cylinder(h = screw_depth + 1, d = pilot_d, $fn = 16);
        rotate(cable_angle) translate([bore_r - 1, 0, disc_floor + cable_d / 2 + 0.4]) rotate([0, 90, 0]) cylinder(h = disc_r + 10, d = cable_d + 0.6, $fn = 24);
        rotate(button_angle) translate([bore_r - 1, 0, disc_floor + disc_cavity_h / 2]) rotate([0, 90, 0]) cylinder(h = disc_r + 10, d = button_hole_d, $fn = 24);
    }
    // Inside the bore: LED ring support ribs with locating lips, speaker locating ring.
    intersection() {
        cylinder(h = body_h, r = bore_r + 0.01);
        union() {
            for (a = rib_angles) rotate(a) {
                translate([ring_od / 2 - 1.5, -1.5, disc_floor - 0.01]) cube([bore_r - ring_od / 2 + 2, 3, z_ring - disc_floor + 0.01]);
                translate([ring_od / 2 + 0.3, -1.5, disc_floor - 0.01]) cube([bore_r - ring_od / 2 + 1, 3, z_ring + 1.5 - disc_floor]);
            }
            translate([0, 0, disc_floor - 0.01]) difference() {
                cylinder(h = 1.2, r = spk_d / 2 + 1.5);
                translate([0, 0, -1]) cylinder(h = 4, r = spk_d / 2 + 0.3);
            }
        }
    }
}

// Bezel in the disc frame (sits on the body, clamps the diffuser).
module palm_bezel_local() {
    h = diffuser_t + bezel_t;
    ch = 0.8;
    translate([0, 0, body_h]) difference() {
        hull() {
            cylinder(h = h - ch, r = disc_r);
            cylinder(h = h, r = disc_r - ch);
        }
        translate([0, 0, -1]) cylinder(h = h + 2, r = bezel_open_r);
        translate([0, 0, -1]) cylinder(h = diffuser_t + 1, r = diffuser_r + 0.3);
        for (a = disc_screw_angles) rotate(a) translate([screw_r, 0, 0]) {
            translate([0, 0, -1]) cylinder(h = h + 2, d = screw_clear_d, $fn = 16);
            translate([0, 0, h - screw_head_h]) cylinder(h = 5, d = screw_head_d, $fn = 24);
        }
    }
}
// Print face down.
module palm_bezel() translate([0, 0, body_h + diffuser_t + bezel_t]) rotate([180, 0, 0]) palm_bezel_local();

// Diffuser: flat disc with a small speaker grille in the middle.
module diffuser() {
    difference() {
        cylinder(h = diffuser_t, r = diffuser_r);
        for (p = concat([[0, 0]], [for (a = [0 : 60 : 300]) [3.2 * cos(a), 3.2 * sin(a)]]))
            translate([p[0], p[1], -1]) cylinder(h = diffuser_t + 2, d = 2.2, $fn = 12);
    }
}
module diffuser_local() translate([0, 0, body_h]) diffuser();

// Disc frame -> HAND frame: disc +z points out of the palm (-Z).
module disc_to_hand() translate([band_pos, 0, -hand_thickness]) rotate([180, 0, 0]) children();

// -----------------------------------------------------------------------------
// Wrist pod: nicklink bay, optional BLE board + LiPo zone, snap-on lid.
// -----------------------------------------------------------------------------
snap_w = 8;         // snap tab width
snap_len = 7;       // tab length below the lid (strain about 2 % in PETG)
snap_bump = 0.7;    // catch depth
snap_y = 12;        // snap tabs on the end walls at y = +/- snap_y
module pod_outline2d(inset = 0) {
    translate([pod_out[0] / 2, 0]) rrect([pod_out[0] - 2 * inset, pod_out[1] - 2 * inset], 4 - inset);
}
module pod_cavity2d() { translate([pod_wall + pod_in[0] / 2, 0]) rrect([pod_in[0], pod_in[1]], 1); }
// Snap windows in both end walls (catch height just below the lid).
function pod_snap_pts() = [for (ix = [0, 1], sy = [-1, 1]) [ix, sy * snap_y]];

// Board boss with a 4 mm top (nicklink allows standoffs up to 4 mm across).
// "insert": a wider base takes an M2 heat-set insert pressed in from UNDER the
// pod (through the floor), so the 4 mm top stays whole; use M2 x 6 screws.
// "selftap": 4 mm boss with a pilot for M2 x 6 self-tapping screws.
// Pod frame, boss base on the outer bottom (z = 0).
module nl_boss() {
    collar = 1.5;
    top = pod_floor + nl_standoff_h;
    if (nl_mount == "insert") difference() {
        union() {
            cylinder(h = top - collar, d = insert_d + 2.6);
            cylinder(h = top, d = 4);
        }
        translate([0, 0, -0.01]) cylinder(h = top - collar + 0.01, d = insert_d, $fn = 24);
        cylinder(h = top + 1, d = screw_clear_d, $fn = 16);
    }
    else difference() {
        cylinder(h = top, d = 4);
        translate([0, 0, pod_floor]) cylinder(h = top, d = pilot_d, $fn = 16);
    }
}

module wrist_pod() {
    L = pod_out[0]; W = pod_out[1]; H = pod_out[2];
    lugx = [L / 2 - (slot_l / 2 + lug_wall) + 1, L / 2 + (slot_l / 2 + lug_wall) - 1];  // overlap 2 mm
    lugy = W / 2 + 0.5 + slot_w / 2;
    usb_z = pod_floor + nl_standoff_h + nl_board[2] + nl_usb_h / 2;
    ble_z = pod_floor + lipo[2] + 1 + ble_board[2] + nl_usb_h / 2;
    difference() {
        union() {
            linear_extrude(H) pod_outline2d();
            for (x = lugx, sy = [-1, 1]) translate([x, sy * lugy, 0]) lug_pad();
        }
        translate([0, 0, pod_floor]) linear_extrude(H) pod_cavity2d();
        for (x = lugx, sy = [-1, 1]) translate([x, sy * lugy, 0]) lug_cut();
        // heat-set insert holes through the floor (inserts go in from underneath)
        if (nl_mount == "insert") for (h = nl_holes) let(p = nl_to_pod(h)) translate([p[0], p[1], -1]) cylinder(h = pod_floor + 1.02, d = insert_d, $fn = 24);
        // nicklink USB-C, elbow end wall (charge and flash in place)
        translate([0, nl_usb_x - nl_board[0] / 2, usb_z]) usb_opening();
        // BLE board USB-C, thumb-side wall (charges the LiPo)
        if (bay_ble) translate([ble_cx, W / 2, ble_z]) rotate(-90) usb_opening();
        // cable to the palm disc, hand end wall, little-finger side
        translate([L - pod_wall - 1, -pod_in[1] / 2 + 7, pod_floor + 8]) rotate([0, 90, 0]) cylinder(h = pod_wall + 2, d = cable_d + 1, $fn = 24);
        // slide power switch, little-finger-side wall
        if (pod_switch) translate([ble_cx - 4.75, -W / 2 - 1, pod_floor + 9.5]) cube([9.5, pod_wall + 2, 4.6]);
        // snap windows
        for (s = pod_snap_pts()) translate([s[0] == 0 ? -1 : L - pod_wall - 1, s[1] - snap_w / 2 - 0.5, H - snap_len - 0.2])
            cube([pod_wall + 2, snap_w + 1, 1.6 + 0.4]);
    }
    // nicklink bosses
    for (h = nl_holes) let(p = nl_to_pod(h)) translate([p[0], p[1], 0]) nl_boss();
    // LiPo end stops (the cell sits under the BLE board, on foam tape)
    for (k = [-1, 1]) translate([ble_cx + k * (lipo[0] / 2 + 0.8) - 0.6, -lipo[1] / 2, pod_floor - 0.01]) cube([1.2, lipo[1], 2]);
}

// USB-C cable opening: plug overmold clearance (13 x 7.5) with a 1 mm outer
// chamfer. x = 0 on the outer wall surface, +x into the pod.
module usb_opening() {
    translate([-1, -6.5, -3.75]) cube([pod_wall + 2, 13, 7.5]);
    hull() {
        translate([-1, -7.5, -4.75]) cube([1.01, 15, 9.5]);
        translate([0.99, -6.5, -3.75]) cube([0.02, 13, 7.5]);
    }
}

// Lid, print orientation (outer face on the bed, lip and snap tabs up).
module wrist_pod_lid() {
    L = pod_out[0];
    lip_h = 2;
    cl = 0.25;  // PETG sliding clearance per side
    difference() {
        union() {
            linear_extrude(pod_lid_t) pod_outline2d();
            // locating lip, open where the snap tabs are
            translate([0, 0, pod_lid_t - 0.01]) linear_extrude(lip_h) difference() {
                translate([pod_wall + pod_in[0] / 2, 0]) rrect([pod_in[0] - 2 * cl, pod_in[1] - 2 * cl], 1);
                translate([pod_wall + pod_in[0] / 2, 0]) rrect([pod_in[0] - 2 * cl - 2.4, pod_in[1] - 2 * cl - 2.4], 1);
                for (s = pod_snap_pts()) translate([s[0] == 0 ? pod_wall - 1 : L - pod_wall - 3, s[1] - snap_w / 2 - 1]) square([4, snap_w + 2]);
            }
            // snap tabs with catches
            for (s = pod_snap_pts()) {
                xin = s[0] == 0 ? pod_wall + cl : L - pod_wall - cl - 1.2;
                out = s[0] == 0 ? -1 : 1;   // catch points through the wall
                translate([xin, s[1] - snap_w / 2, pod_lid_t - 0.01]) {
                    cube([1.2, snap_w, snap_len + 0.01]);
                    // catch: flat face toward the lid, ramp toward the tip
                    translate([out < 0 ? 0 : 1.2, 0, snap_len - 1.6])
                        rotate([90, 0, 0]) mirror([0, 0, 1]) linear_extrude(snap_w)
                            polygon(out < 0 ? [[0, 0], [-snap_bump, 0], [-snap_bump, 0.4], [0, 1.6]]
                                            : [[0, 0], [snap_bump, 0], [snap_bump, 0.4], [0, 1.6]]);
                }
            }
        }
        // Pin access to RESET and BOOT (only needed for the serial bootloader), LED window.
        for (p = [nl_reset, nl_boot]) let(q = nl_to_pod(p)) translate([q[0], q[1], -1]) cylinder(h = pod_lid_t + 2, d = 2.2, $fn = 16);
        let(q = nl_to_pod(nl_led)) translate([q[0], q[1], -1]) cylinder(h = pod_lid_t + 2, d = 3, $fn = 20);
        // IMU position mark (shallow dot on the outer face)
        let(q = nl_to_pod(nl_imu)) translate([q[0], q[1], -0.01]) cylinder(h = 0.4, d = 2.5, $fn = 16);
    }
}
module pod_to_hand() translate([-pod_out[0] - pod_gap, 0, pad_t + 2]) children();

// nicklink and BLE board placeholders (preview only).
module pod_boards_preview() {
    color(c_pcb) translate([nl_x0, -nl_board[0] / 2, pod_floor + nl_standoff_h]) cube([nl_board[1], nl_board[0], nl_board[2]]);
    color("silver") translate([nl_x0 - nl_usb_overhang, nl_usb_x - nl_board[0] / 2 - 4.5, pod_floor + nl_standoff_h + nl_board[2]]) cube([7.5, 9, nl_usb_h]);
    color("black") for (xk = [2.67, 28.78]) let(q = nl_to_pod([xk, 1.32]))
        translate([q[0] - 1.27, q[1] - 1.27, pod_floor + nl_standoff_h + nl_board[2]]) cube([25.4, 5.08, 8.5]);
    if (bay_ble) {
        color([0.75, 0.75, 0.8]) translate([ble_cx - lipo[0] / 2, -lipo[1] / 2, pod_floor]) cube([lipo[0], lipo[1], lipo[2]]);
        color(c_pcb) translate([ble_cx - ble_board[1] / 2, pod_in[1] / 2 - ble_board[0] - 0.5, pod_floor + lipo[2] + 1]) cube([ble_board[1], ble_board[0], ble_board[2]]);
    }
}

// -----------------------------------------------------------------------------
// Strap adjuster: tri-glide for 25 mm webbing (also fits 20 mm).
// -----------------------------------------------------------------------------
module strap_adjuster() {
    bar = 3.2; t = 3;
    sw = strap_t + 1.4;
    ol = 3 * bar + 2 * sw;
    linear_extrude(t) difference() {
        rrect([slot_l + 2 * bar, ol], 2);
        for (k = [-1, 1]) translate([0, k * (sw / 2 + bar / 2)]) rrect([slot_l, sw], 0.6);
    }
}

// -----------------------------------------------------------------------------
// Preview helpers: hand, forearm and straps
// -----------------------------------------------------------------------------
module hand_dummy() {
    t = hand_thickness; b = hand_breadth;
    fl = pick([0.86, 1, 1.12]);
    color(c_skin, 0.45) {
        hull() {
            translate([2, 0, -t / 2]) scale([2, wrist_width / 2, t / 2]) sphere(1);
            translate([palm_center_x, 0, -t / 2]) scale([22, b / 2 * 0.97, t / 2]) sphere(1);
            translate([dorsum_length - 6, 0, -t / 2]) scale([6, b / 2, t / 2 * 0.8]) sphere(1);
        }
        for (f = [[28, 72, 18], [9, 80, 18.5], [-10, 75, 17.5], [-28, 60, 15.5]])
            translate([dorsum_length - 6, f[0] * b / 85, -t * 0.45]) rotate([0, 90, 0]) cylinder(h = f[1] * fl, d = f[2] * b / 85);
        hull() {
            translate([20, wrist_width * 0.38, -t * 0.62]) sphere(d = 24 * b / 85);
            translate([60 * fl, b * 0.6, -t * 0.8]) sphere(d = 19 * b / 85);
        }
        hull() {
            translate([60 * fl, b * 0.6, -t * 0.8]) sphere(d = 19 * b / 85);
            translate([90 * fl, b * 0.74, -t * 0.9]) sphere(d = 17 * b / 85);
        }
    }
}
module forearm_dummy() {
    t = hand_thickness;
    color(c_skin, 0.45) hull() {
        translate([0, 0, -t * 0.55]) scale([0.5, wrist_width / 2, t * 0.6]) sphere(1);
        translate([-150, 0, -t * 0.66]) scale([0.5, wrist_width / 2 + 8, t * 0.85]) sphere(1);
    }
}

module band(a, b, w) {
    hull() { translate(a) cube([w, 1.6, 1.6], center = true); translate(b) cube([w, 1.6, 1.6], center = true); }
}
module straps_preview() {
    color([0.08, 0.08, 0.08]) {
        for (sy = [-1, 1]) {
            a = [band_pos, sy * (band_slot_y(band_pos) + slot_w / 2 + slot_edge / 2), plate_z0 + wing_t + 0.8];
            e = [band_pos, sy * (hand_breadth / 2 + 3), -hand_thickness / 2];
            d = [band_pos, sy * lug_y, -hand_thickness - lug_t / 2];
            band(a, e, strap_w);
            band(e, d, strap_w);
        }
        place_ctrl() for (xh = ctrl_strap_xh) intersection() {
            translate([xh - ctrl_strap_w / 2, -60, saddle_top_z - 2]) cube([ctrl_strap_w, 120, 120]);
            difference() { handle_envelope(fit_gap + 1.8, 70); handle_envelope(fit_gap - 0.2, 70); }
        }
    }
}

// Everything that moves with the hand when the wrist bends back.
module wrist_bend() {
    translate([0, 0, -hand_thickness / 2]) rotate([0, -preview_wrist_ext, 0]) translate([0, 0, hand_thickness / 2]) children();
}

module assembly() {
    if (show_hand) forearm_dummy();
    pod_to_hand() {
        color(c_red) wrist_pod();
        pod_boards_preview();
        color(c_gold) translate([0, 0, pod_out[2] + pod_lid_t]) mirror([0, 0, 1]) wrist_pod_lid();
    }
    wrist_bend() {
        if (show_hand) hand_dummy();
        color(c_red) cradle_hand();
        place_ctrl() controller_model();
        disc_to_hand() {
            color(c_gold) palm_disc();
            color(c_red) palm_bezel_local();
            color(c_glow, 0.85) diffuser_local();
        }
        if (show_straps) straps_preview();
        if (show_keepout) place_ctrl() keepout_zones();
    }
}

// -----------------------------------------------------------------------------
// Report: fit checks and the nominal controller -> emitter transform
// -----------------------------------------------------------------------------
f_hand = ctrl_R * face_n;
trig_hand = to_hand([trigger_x, 4, -trigger_drop]);
face_c_hand = to_hand(face_center);
face_top_z = face_c_hand[2] + face_d / 2 * sqrt(max(0, 1 - f_hand[2] * f_hand[2]));
echo(str("CHECK faceplate normal is ", round(acos(f_hand[2])), " deg off the back-of-hand normal (",
         round(atan2(f_hand[0], f_hand[2])), " toward fingers, ", round(atan2(f_hand[1], f_hand[2])), " toward thumb)"));
echo(str("CHECK trigger tip at X=", round(trig_hand[0]), " (knuckles at ", range_dorsum[0], " to ", range_dorsum[2],
         "), ", round(trig_hand[2]), " mm above the back-of-hand skin"));
if (trig_hand[2] < 8) echo("WARNING trigger tip is less than 8 mm above the skin: raise ctrl_lift or ctrl_pitch");
echo(str("CHECK faceplate rim top ", round(face_top_z), " mm above the back-of-hand skin"));
echo(str("CHECK plate ends at X=", plate_x1, ", smallest knuckle line X=", range_dorsum[0],
         plate_x1 > range_dorsum[0] - 2 ? "  WARNING plate reaches the knuckles of small hands" : ""));
echo(str("CHECK palm band travel X=", band_x[0], "..", band_x[1], " covers palm centres ", range_palm_c[0], "..", range_palm_c[2],
         (band_x[0] <= range_palm_c[0] && band_x[1] >= range_palm_c[2]) ? "" : "  WARNING not covered"));
for (xh = ctrl_strap_xh) let(
        low = ctrl_T[2] + xh * sin(ctrl_pitch) + (hc_z(xh) - handle_d / 2 * hscale(xh)) * cos(ctrl_pitch) - fit_gap,
        roof = low - (plate_top + slot_w))
    echo(str("CHECK controller strap tunnel at handle x=", xh, ": roof ", round(roof * 10) / 10, " mm",
             roof < 1.2 ? "  WARNING thin, raise ctrl_lift" : ""));
if (ctrl_strap_xh[len(ctrl_strap_xh) - 1] + ctrl_strap_w / 2 > grip_pad_x)
    echo("WARNING a controller strap reaches the grip button");
if (saddle_x[1] > grip_pad_x) echo("WARNING the saddle reaches the grip button");
// Palm-band side strap: path from the cradle bar around the hand edge to the disc lug, plus two 40 mm fold-backs.
for (i = [0, 2]) let(path = range_circ[i] / 2 - band_slot_y(band_x[0]) - lug_y)
    echo(str("CHECK palm band side strap for a ", i == 0 ? "small" : "large", " hand: path ", round(path),
             " mm, cut length about ", round(path + 90), " mm"));
echo(str("CHECK wrist pod inside ", fmt(pod_in), " mm, outside ", fmt(pod_out), " mm, hand end ", pod_gap, " mm behind the wrist crease"));

// Emitter = centre of the diffuser front face; aim = palm normal (-Z).
function emitter_hand(i) = [max(band_x[0], min(band_x[1], range_palm_c[i])), 0, -range_thick[i] - body_h - diffuser_t];
n_hand = [0, 0, -1];
legacy_off = [-1, -20, 48];
// Unity Euler angles (x, y, 0) that turn Vector3.forward into n (pitch first, then yaw).
function euler_of(n) = abs(n[0]) < 1e-3 ? [atan2(-n[1], n[2]), 0, 0]
                                        : [atan2(-n[1], sqrt(n[0] * n[0] + n[2] * n[2])), atan2(n[0], n[2]), 0];
n_h = tp(ctrl_R) * n_hand;
// Unity, OpenXR grip pose (estimate): X = -handleY, Y = handleZ, Z = handleX - oxr_grip_x.
n_g = [-n_h[1], n_h[2], n_h[0]];
// Legacy OVR pose = grip pose offset by (-1, -20, 48) mm then rotated 60 deg about X
// (Quest 3 right-hand values from SenseGlove's Unity SDK, SG_HandTracking.cs).
n_l = rx(-60) * n_g;
for (i = [0 : 2]) let(
        E_h = tp(ctrl_R) * (emitter_hand(i) - ctrl_T),
        E_g = [-E_h[1], E_h[2], E_h[0] - oxr_grip_x],
        E_l = rx(-60) * (E_g - legacy_off))
    echo(str("NOMINAL ", ["small", "mid", "large"][i], " hand: emitter in HANDLE frame (mm) ", fmt(E_h),
             "; OVR legacy anchor localPosition (m) ", fmt(E_l / 1000, 3), "; OpenXR grip localPosition (m) ", fmt(E_g / 1000, 3)));
echo(str("NOMINAL aim dir: HANDLE ", fmt(n_h, 3), "; OVR legacy ", fmt(n_l, 3), " = localEulerAngles ", fmt(euler_of(n_l), 0),
         "; OpenXR grip ", fmt(n_g, 3), " = localEulerAngles ", fmt(euler_of(n_g), 0)));

// -----------------------------------------------------------------------------
// Part selector
// -----------------------------------------------------------------------------
handed() {
    if (part == "assembly") assembly();
    else if (part == "cradle") cradle();
    else if (part == "palm_disc") palm_disc();
    else if (part == "palm_bezel") palm_bezel();
    else if (part == "diffuser") diffuser();
    else if (part == "wrist_pod") wrist_pod();
    else if (part == "wrist_pod_lid") wrist_pod_lid();
    else if (part == "strap_adjuster") strap_adjuster();
    else if (part == "liner") liner();
    else if (part == "controller_dummy") { controller_model(); if (show_keepout) keepout_zones(); }
    else if (part == "none") { }
    else echo(str("ERROR unknown part: ", part));
}
