shader_type canvas_item;

// The sheen that runs across a foil card when you tilt it: a bright band sweeps
// across the node, then nothing happens until the next pass. Meant for a
// ColorRect laid over a card, sized to the card and with mouse_filter set to
// Ignore - the shader draws ONLY the gleam, so everywhere the band is not, the
// rect is fully transparent and the card underneath shows through untouched.
//
// The timing is two numbers, not one: period_seconds is how often a pass
// starts (3 seconds by default) and sweep_seconds is how long a single pass
// takes to cross (0.7s). The rest of the period is dead time. A single "speed"
// value cannot express that - it would smear one pass across the whole three
// seconds, which reads as a slow gradient rather than as a catch of light.
//
// Two bands travel together: a wide soft one and a narrow bright one trailing
// it, which is what foil actually does and what keeps the effect from looking
// like a single blurred stripe.

// The gleam's colour. Multiplied by the node's own colour/modulate, so a white
// ColorRect leaves this untouched, and tinting the ColorRect tints the sheen.
uniform vec4 gleam_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// Peak opacity of the wide band where it is brightest.
uniform float intensity : hint_range(0.0, 1.0) = 0.45;

// Seconds between the start of one pass and the start of the next.
uniform float period_seconds : hint_range(0.1, 30.0) = 3.0;
// Seconds one pass takes to cross the node. Anything left over in the period is
// the pause. Clamped against the period below, so setting it longer than the
// period gives a continuous sweep rather than breaking.
uniform float sweep_seconds : hint_range(0.05, 30.0) = 0.7;
// TIME at which the cycle is measured from, so a card can be dealt with the
// gleam at a known point instead of joining a shared clock wherever it happens
// to be. TIME rolls over every hour, which makes the difference negative;
// fract() still returns a phase in [0, 1), so the sweep carries on from a
// different point rather than breaking. Set it from script - see below.
uniform float start_time = 0.0;

// Direction the band travels, in degrees clockwise from "left to right". 0 is a
// vertical bar moving right; 90 a horizontal one moving down. The angle is a
// true on-screen angle, not a UV one - see the aspect correction in fragment().
uniform float angle_degrees : hint_range(-180.0, 180.0) = 25.0;

// Width of the wide band, as a fraction of the distance it travels.
uniform float band_width : hint_range(0.01, 1.0) = 0.22;
// Shapes its falloff: 1 is a linear ramp to a hard peak, higher values pull the
// light into a soft core with a long tail, which is what reads as a highlight.
uniform float band_softness : hint_range(0.5, 8.0) = 2.5;

// The narrow band trailing the wide one. Strength 0 turns it off.
uniform float streak_strength : hint_range(0.0, 2.0) = 0.8;
// Its width, as a fraction of band_width.
uniform float streak_width : hint_range(0.02, 1.0) = 0.22;
// How far behind the wide band it sits, in the same units as band_width.
uniform float streak_offset : hint_range(0.0, 1.0) = 0.16;

// Eases the pass in and out instead of running it at a constant speed. 0 is
// linear; 1 makes the band start and finish slowly with a quick middle, the way
// a card catches light as it turns through the reflection.
uniform float sweep_ease : hint_range(0.0, 1.0) = 0.6;

// Rounds off the gleam at the corners, as a fraction of the node's SHORT side,
// so a sheen over a rounded card does not spill past its corners. 0.5 is a
// capsule. 0 leaves the node's square corners.
uniform float corner_radius : hint_range(0.0, 0.5) = 0.06;

void fragment() {
	// Width of one screen pixel in UV, per axis, taken from the screen-space
	// derivatives rather than from a size uniform: a ColorRect has no texture,
	// so TEXTURE_PIXEL_SIZE is 1 and tells us nothing about how big the node
	// is. The length() form (rather than fwidth) keeps this correct when the
	// card itself is rotated, which cards in hand usually are.
	vec2 uv_per_pixel = vec2(
		length(vec2(dFdx(UV.x), dFdy(UV.x))),
		length(vec2(dFdx(UV.y), dFdy(UV.y))));
	uv_per_pixel = max(uv_per_pixel, vec2(1e-6));
	// Node width over node height, both in screen pixels.
	float aspect = uv_per_pixel.y / uv_per_pixel.x;

	// Work in "height units": x scaled by the aspect so both axes carry the same
	// unit. In plain UV a 25 degree band would shear with the card's proportions
	// and come out at some other angle entirely.
	vec2 pos = vec2(UV.x * aspect, UV.y);
	vec2 half_size = vec2(aspect, 1.0) * 0.5;

	// Where this pixel sits along the direction of travel, as 0..1 across the
	// node. The extent is the node's own width measured along that direction -
	// the projection of the two sides onto it - so the band takes the same time
	// to cross whatever angle it runs at.
	float angle = radians(angle_degrees);
	vec2 dir = vec2(cos(angle), sin(angle));
	float extent = abs(dir.x) * aspect + abs(dir.y);
	float t = dot(pos - half_size, dir) / extent + 0.5;

	// Phase within the cycle, then remapped so 0..1 is the pass itself and
	// anything past 1 is the pause before the next one.
	float sweep = clamp(sweep_seconds / period_seconds, 0.001, 1.0);
	float progress = fract((TIME - start_time) / period_seconds) / sweep;
	float gleam = 0.0;

	if (progress <= 1.0) {
		progress = mix(progress, smoothstep(0.0, 1.0, progress), sweep_ease);

		// The travel runs from fully off one side to fully off the other, with
		// room for the trailing streak at the start, so neither band is ever
		// half-drawn at the node's edge when a pass begins or ends.
		float lead_in = band_width + streak_offset;
		float head = mix(-lead_in, 1.0 + band_width, progress);

		float wide = clamp(1.0 - abs(t - head) / band_width, 0.0, 1.0);
		gleam = pow(wide, band_softness) * intensity;

		if (streak_strength > 0.0) {
			float narrow_width = max(band_width * streak_width, 1e-4);
			float narrow = clamp(1.0 - abs(t - (head - streak_offset)) / narrow_width, 0.0, 1.0);
			gleam += pow(narrow, band_softness) * intensity * streak_strength;
		}
	}

	// Rounded-rect mask. Signed distance to the card's outline, negative inside,
	// faded over one screen pixel (one pixel is uv_per_pixel.y in height units)
	// so the corners stay smooth at any card size or zoom.
	float radius = corner_radius * min(aspect, 1.0);
	vec2 corner = abs(pos - half_size) - half_size + vec2(radius);
	float dist = length(max(corner, vec2(0.0))) + min(max(corner.x, corner.y), 0.0) - radius;
	float mask = 1.0 - smoothstep(-uv_per_pixel.y, uv_per_pixel.y, dist);

	// Only the alpha is driven: the rect is invisible except where the band is,
	// so it can sit on top of the card art without dimming or tinting it. For a
	// hotter, more metallic sheen add `render_mode blend_add;` at the top of the
	// file - the same band then adds light instead of blending over.
	COLOR = vec4(gleam_color.rgb * COLOR.rgb, clamp(gleam, 0.0, 1.0) * mask * gleam_color.a * COLOR.a);
}
