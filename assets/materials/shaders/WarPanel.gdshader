shader_type canvas_item;

// Backdrop for a modal's content area: a weathered olive-drab field panel, the
// kind of scuffed metal a map gets pinned to in a command tent. Everything it
// draws is deliberately low contrast, because text sits directly on top of it -
// the effect has to read as texture, never as detail competing with the words.
//
// Four layers, back to front: the base colour, mud/oil staining, a faint map
// grid, and scratches, all finished with a vignette that grimes up the edges
// where the frame meets the panel. Each layer has its own amount uniform and
// turns off at 0, so the look can be dialled all the way back to a flat colour.
//
// Meant for a ColorRect (or anything else drawing one full quad): it needs UV
// spanning the whole node for the vignette, which a Panel's style box does not
// reliably provide.

// Octaves in the stain and scratch noise. Past 4 the extra detail lands below a
// pixel at these scales and just costs samples.
const int OCTAVES = 4;

// The panel's own colour, and the only source of the result's alpha - the
// layers below tint it but never make it more or less transparent.
uniform vec4 base_color : source_color = vec4(0.105, 0.115, 0.09, 0.9);

// Blotches of dried mud and oil worked into the base colour.
uniform vec4 stain_color : source_color = vec4(0.24, 0.19, 0.11, 1.0);
uniform float stain_amount : hint_range(0.0, 1.0) = 0.35;
// Size of a single blotch, in the node's own pixels. Measured in pixels rather
// than UV so the blotches keep their size when the modal is resized, instead of
// stretching with it.
uniform float stain_scale_pixels : hint_range(16.0, 1024.0) = 220.0;
// Slides the stain noise. Two panels of the same size otherwise wear exactly
// the same stains; a different offset per panel is what makes them individual.
uniform vec2 stain_offset = vec2(0.0, 0.0);

// Fine grain over everything, as a plain brightness jitter. This is the layer
// that stops large flat areas looking like a solid fill.
uniform float grain_amount : hint_range(0.0, 1.0) = 0.09;
// Size of one grain cell in node pixels. Around 2-3 reads as grain; larger
// values turn it into mottling.
uniform float grain_scale_pixels : hint_range(1.0, 64.0) = 2.5;

// The plotting grid ruled over the panel, as on a map table.
uniform vec4 grid_color : source_color = vec4(0.42, 0.47, 0.35, 1.0);
uniform float grid_amount : hint_range(0.0, 1.0) = 0.14;
uniform float grid_spacing_pixels : hint_range(8.0, 256.0) = 44.0;
// Line width in pixels. Kept in pixels, and anti-aliased against the actual
// on-screen derivative below, so the grid stays even at any modal size.
uniform float grid_line_pixels : hint_range(0.5, 4.0) = 1.0;

// Scuffs raked across the panel.
uniform vec4 scratch_color : source_color = vec4(0.62, 0.63, 0.55, 1.0);
uniform float scratch_amount : hint_range(0.0, 1.0) = 0.18;
// Direction the scratches run, in degrees: 0 is horizontal, 90 vertical.
uniform float scratch_angle_degrees : hint_range(0.0, 360.0) = 24.0;
// How long a scratch runs and how thin it is, both in node pixels. The gap
// between the two is what makes a streak rather than a blotch: the noise is
// sampled stretched along the scratch direction and compressed across it.
uniform float scratch_length_pixels : hint_range(32.0, 2048.0) = 700.0;
uniform float scratch_width_pixels : hint_range(1.0, 64.0) = 9.0;
// Only noise above this survives, so scratches stay sparse instead of covering
// the panel. Higher means fewer, finer scratches.
uniform float scratch_threshold : hint_range(0.0, 1.0) = 0.66;

// Grime and shadow pooling at the panel's edges, under the frame.
uniform float vignette_amount : hint_range(0.0, 1.0) = 0.5;
// How far in from the edge the vignette reaches, as a fraction of the half
// size. 0 keeps it hard against the border, 1 drags it to the centre.
uniform float vignette_width : hint_range(0.01, 1.0) = 0.45;

// Position within the node, in the node's own pixels. Deliberately not UV: UV
// is normalized per axis, so on a wide panel its two axes are in different
// units, which would shear the scratches and stretch the grid into rectangles.
varying vec2 local_position;

float hash(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 34.23);
	return fract(p.x * p.y);
}

float value_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	// Smoothstep the interpolation so the cell grid does not show as creases.
	vec2 w = f * f * (3.0 - 2.0 * f);
	float a = hash(cell);
	float b = hash(cell + vec2(1.0, 0.0));
	float c = hash(cell + vec2(0.0, 1.0));
	float d = hash(cell + vec2(1.0, 1.0));
	return mix(mix(a, b, w.x), mix(c, d, w.x), w.y);
}

float fbm(vec2 p) {
	float sum = 0.0;
	float amp = 0.5;
	float total_amp = 0.0;
	for (int i = 0; i < OCTAVES; i++) {
		sum += value_noise(p) * amp;
		total_amp += amp;
		p *= 2.0;
		amp *= 0.5;
	}
	return sum / total_amp;
}

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	vec3 col = base_color.rgb;

	// Mud and oil. Contrast is pushed around the midpoint so the noise reads as
	// separate blotches with clean panel between them, not an even haze.
	if (stain_amount > 0.0) {
		float stain = fbm((local_position + stain_offset) / stain_scale_pixels);
		stain = smoothstep(0.30, 0.70, stain);
		col = mix(col, stain_color.rgb, stain * stain_amount);
	}

	// Plotting grid. The distance to the nearest line is divided by that
	// coordinate's on-screen derivative, which turns it into a distance in
	// screen pixels and so anti-aliases the line at whatever scale the modal
	// ends up drawn at.
	if (grid_amount > 0.0) {
		vec2 cell = local_position / grid_spacing_pixels;
		vec2 distance_to_line = abs(fract(cell + 0.5) - 0.5) / max(fwidth(cell), vec2(0.0001));
		float half_width = grid_line_pixels * 0.5 / grid_spacing_pixels;
		float line = 1.0 - smoothstep(0.0, 1.0, min(distance_to_line.x, distance_to_line.y) - half_width);
		col = mix(col, grid_color.rgb, line * grid_amount);
	}

	// Scuffs. Sampling the same noise on axes with wildly different scales is
	// what stretches its blotches into streaks along `scratch_angle_degrees`.
	if (scratch_amount > 0.0) {
		float a = radians(scratch_angle_degrees);
		vec2 along = vec2(cos(a), sin(a));
		vec2 across = vec2(-along.y, along.x);
		vec2 scratch_position = vec2(
			dot(local_position, along) / scratch_length_pixels,
			dot(local_position, across) / scratch_width_pixels
		);
		float scratch = fbm(scratch_position);
		scratch = smoothstep(scratch_threshold, scratch_threshold + 0.14, scratch);
		col = mix(col, scratch_color.rgb, scratch * scratch_amount);
	}

	// Grain, as a brightness jitter rather than a tint, so it dirties the panel
	// without pulling its colour anywhere.
	if (grain_amount > 0.0) {
		float grain = value_noise(local_position / grain_scale_pixels) - 0.5;
		col += grain * grain_amount;
	}

	// Edge grime. Rectangular rather than radial: the panel sits inside a
	// rectangular frame, and a round vignette on a wide modal darkens the middle
	// of the long sides while leaving the corners clean, which reads as a
	// spotlight instead of dirt.
	if (vignette_amount > 0.0) {
		vec2 from_centre = abs(UV - 0.5) * 2.0;
		float edge = max(from_centre.x, from_centre.y);
		float vignette = smoothstep(1.0 - vignette_width, 1.0, edge);
		col *= 1.0 - vignette * vignette_amount;
	}

	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), base_color.a) * COLOR;
}
