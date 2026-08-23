shader_type canvas_item;

// The radar look of a selection target marker: the middle of the icon sits at a low resting alpha and
// pie slices sweep around it at full opacity, like radar beams passing over a blip. The outer ring is
// left alone, so the marker keeps a solid rim and reads as a target even at the moment no beam is
// pointing anywhere useful.
//
// Lives in a shader rather than in a Tween because a tween can only drive whole-node properties — it
// could fade the marker, but it could not light up one wedge of it — and because there is then nothing
// to create, kill and re-create on every clickable/subdued/unclickable switch.
//
// Multiplies COLOR.a rather than assigning it, so whatever modulate the node carries still applies:
// the hover tint and the subdued dimming stay the scene script's business, and the transparent pixels
// around the icon stay transparent, so the wedge is only ever drawn ON the icon.

/// Off for a subdued target: a secondary option is the one that holds still. See ClickableTextureRect.
uniform bool sweeping = true;

/// What the swept area is drawn at while the beam is elsewhere.
uniform float base_alpha : hint_range(0.0, 1.0) = 0.0;

/// How far out the slices reach, as a fraction of the distance from the middle of the texture to the
/// middle of an edge. Everything beyond this is left at the icon's own alpha and never dims, which is
/// what keeps the rim of the target solid while the inside turns. Above 1.0 it starts eating into the
/// corners, which for a round marker is past the artwork anyway.
uniform float sweep_radius : hint_range(0.0, 1.5) = 0.66;

/// Width of the fade at that boundary, in the same units. Enough to keep the circle from stair-stepping
/// without blurring the rim into the sweep.
uniform float radius_edge : hint_range(0.0, 0.5) = 0.03;

/// What the icon is drawn at under the beam.
uniform float sweep_alpha : hint_range(0.0, 1.0) = 1.0;

/// How many slices turn at once, spaced evenly around the circle. Each one gets 360 / slice_count
/// degrees of the turn to itself, so this is also the ceiling on how wide sweep_degrees can be before
/// the slices meet and the marker stops looking like it is moving.
uniform int slice_count : hint_range(1, 12) = 4;

/// Width of ONE slice. The beam is its leading edge; the rest of the slice trails behind. Half of a
/// slice's 120-degree share at the default count, so lit and unlit are equal and the pinwheel reads.
uniform float sweep_degrees : hint_range(1.0, 360.0) = 60.0;

/// Angular width of the ramp on either side of the slice. Purely to antialias two radial edges — at 0
/// they crawl with jagged steps as the slice turns.
uniform float edge_degrees : hint_range(0.0, 45.0) = 3.0;

/// Seconds for one full revolution. Written from GameSettings so it follows the game speed setting.
uniform float period_seconds = 5.0;

/// TIME at which the sweep was (re)started, so a marker always appears with the beam at a known angle
/// instead of joining a shared clock wherever it happened to be. TIME rolls over every hour, which puts
/// this in the future and makes the difference below negative; fract() still returns a phase in [0, 1),
/// so the beam carries on from a different angle instead of breaking.
uniform float start_time = 0.0;

void fragment() {
	// 1.0 while the sweep is off, so a subdued marker is left at exactly the alpha its modulate asks for.
	float alpha_scale = 1.0;

	if (sweeping && period_seconds > 0.0) {
		// Measured from the middle of the texture, so the slice is a pie centred on the icon. Undefined
		// at the exact centre texel, which is one pixel of a marker that has a hole there anyway.
		vec2 offset = UV - vec2(0.5);
		float angle = atan(offset.y, offset.x);

		// UV y grows downward, so a leading edge that advances counter-clockwise in maths turns
		// clockwise on screen — the direction a radar is expected to sweep.
		float lead = TAU * fract((TIME - start_time) / period_seconds);

		// How far behind the nearest beam this pixel sits: 0 at a beam itself, growing backwards, and
		// wrapping once per slice rather than once per turn. That wrap is the whole trick — every slice
		// is the same one drawn again a sector further round, so the count costs nothing to raise.
		float sector = TAU / float(max(slice_count, 1));
		float behind = mod(lead - angle, sector);

		// Capped at the sector: slices asked to be wider than their share would run into the one ahead,
		// and a solid ring reads as a marker that has stopped rather than as a wider beam.
		float width = min(radians(sweep_degrees), sector);
		// Clamped against the width so a slice narrower than two ramps still has something solid left.
		float edge = clamp(radians(edge_degrees), 0.0001, width * 0.5);
		float wedge = smoothstep(0.0, edge, behind) * (1.0 - smoothstep(width - edge, width, behind));

		// 1 in the swept disc, 0 out on the rim. Scaled so the boundary is measured against the half
		// width of the texture rather than against UV, which would put it at half the intended radius.
		float radius = length(offset) * 2.0;
		float swept = 1.0 - smoothstep(sweep_radius - radius_edge, sweep_radius, radius);

		// The rim is not merely "always lit" — it is untouched, so whatever alpha the artwork and the
		// node's modulate give it is what it keeps.
		alpha_scale = mix(1.0, mix(base_alpha, sweep_alpha, wedge), swept);
	}

	COLOR.a *= alpha_scale;
}
