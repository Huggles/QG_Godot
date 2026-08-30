shader_type canvas_item;

// Burns a canvas item away along a ragged, charred edge. `burn` drives the
// whole effect: 0 leaves the item untouched, 1 leaves nothing.
//
// Works on a single node out of the box. To burn a whole Control subtree - a
// card being panel, art, labels and button rather than one texture - give every
// node in it the SAME material and turn `group_space` on, so they all measure
// the burn in one shared space instead of each restarting it in its own. See
// BurnEffect.cs, which does that and keeps the uniforms below in sync.

// Octaves of value noise making up the burn front. More octaves means a more
// frayed edge; 4 is already fine enough for a card-sized sprite.
const int OCTAVES = 4;
// Summed octaves cluster around 0.5 rather than filling 0..1, which would make
// the burn crawl at the start and end and race through the middle. This
// stretches the distribution back out; the handful of pixels pushed past 0 or 1
// by it are the extremes the burn passes through almost instantly anyway.
const float NOISE_CONTRAST = 1.6;

// How far the burn has progressed. 0 = whole item, 1 = completely burnt away.
uniform float burn : hint_range(0.0, 1.0) = 0.0;

// Size of the blotches in the burn front, in pixels of whichever space the burn
// is measured in (see `group_space`). Measured in pixels rather than UV so it
// does not change with a texture's size or aspect. Small values fray the edge,
// large values make it roll in broad waves.
uniform float noise_scale_pixels : hint_range(4.0, 512.0) = 48.0;

// Slides the noise, in the same pixels as `noise_scale_pixels`. Several items
// burning side by side are usually the same size and so measure the burn over
// the same coordinates, which would burn all of them to one identical shape;
// giving each a different offset is what makes them individual.
uniform vec2 noise_offset = vec2(0.0, 0.0);

// Width of the charred/glowing band trailing the hole, as a fraction of the
// burn front's range. Larger values give a wider, softer scorch.
uniform float edge_width : hint_range(0.001, 0.5) = 0.12;
// Share of that band taken by the black char, the rest being the ember glow.
uniform float char_fraction : hint_range(0.0, 1.0) = 0.35;

uniform vec4 char_color : source_color = vec4(0.06, 0.05, 0.05, 1.0);
uniform vec4 ember_color : source_color = vec4(1.0, 0.35, 0.05, 1.0);
// Multiplies the ember color past 1.0 so it blooms if the scene has glow on.
uniform float ember_energy : hint_range(0.0, 8.0) = 2.0;
// Depth of the ember's shimmer, and its rate. 0 leaves the ember steady.
uniform float ember_flicker : hint_range(0.0, 1.0) = 0.3;
uniform float flicker_speed : hint_range(0.0, 20.0) = 4.0;

// Direction the burn travels, in degrees: 0 burns left to right, 90 top to
// bottom, 270 bottom to top.
uniform float direction_degrees : hint_range(0.0, 360.0) = 270.0;
// How much that direction steers the burn against the noise. 0 lets the hole
// eat outwards from wherever the noise happens to be lowest; 1 makes it a
// straight sweep. Some bias also evens out the pacing, because the directional
// gradient is spread evenly across the item where the noise is not.
uniform float direction_bias : hint_range(0.0, 1.0) = 0.35;

// Off, every node measures the burn in its own local pixels and its own UV,
// which is all a lone Sprite2D or TextureRect needs. On, they all measure it in
// one outer node's space instead - the card root - so a material shared across
// that whole subtree cuts one continuous hole through panel, art and text
// rather than a separate, identical-looking hole per node.
uniform bool group_space = false;
// The group root's global transform, inverted, as its two axes and its origin:
// what turns a node's global position into a position inside the root.
// Inverted on the CPU because it is the same for every node sharing this
// material, so doing it here would be the same work repeated per vertex.
// Godot's shader language has no Transform2D uniform, hence the three vec2s.
uniform vec2 group_inv_x = vec2(1.0, 0.0);
uniform vec2 group_inv_y = vec2(0.0, 1.0);
uniform vec2 group_inv_origin = vec2(0.0, 0.0);
// The group root's size in its own pixels, which is what the burn sweeps across.
uniform vec2 group_size = vec2(1.0, 1.0);

// Where this pixel sits in the space the burn is measured in: local pixels of
// this node, or of the group root. Deliberately not UV: UV is normalized per
// axis, so on a non-square item its two axes are in different units (which
// would stretch the blotches) and its scale follows the source image's
// dimensions (which would tie the blotch size to how big that image happened
// to be).
varying vec2 burn_position;
// The same position normalized to 0..1 across the whole burning item, which is
// what the sweep direction runs along. Not UV either, for the group case: UV
// restarts at every node, so a card's title would sweep through a whole burn of
// its own in the width of one label.
varying vec2 burn_uv;

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
	if (group_space) {
		// MODEL_MATRIX takes this node's local vertex to global space, and the
		// inverted root transform takes it from there into the root's own
		// pixels. Going through global space is what lets siblings at different
		// depths, offsets and scales agree on one burn front, and it survives
		// the card being moved, rotated or scaled while it burns.
		vec2 global_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
		burn_position = group_inv_x * global_position.x
			+ group_inv_y * global_position.y
			+ group_inv_origin;
		burn_uv = burn_position / max(group_size, vec2(1.0));
	} else {
		burn_position = VERTEX;
		burn_uv = UV;
	}
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);

	float noise = fbm((burn_position + noise_offset) / max(noise_scale_pixels, 1.0));
	noise = clamp((noise - 0.5) * NOISE_CONTRAST + 0.5, 0.0, 1.0);

	// Project onto the burn direction and rescale to 0..1 using the corner the
	// sweep starts from and the one it ends at, so the bias behaves the same at
	// any angle.
	float ang = radians(direction_degrees);
	vec2 dir = vec2(cos(ang), sin(ang));
	float lo = min(0.0, dir.x) + min(0.0, dir.y);
	float hi = max(0.0, dir.x) + max(0.0, dir.y);
	float gradient = (dot(burn_uv, dir) - lo) / max(hi - lo, 0.0001);

	// The burn front: every pixel below the threshold has already gone.
	float field = mix(noise, gradient, direction_bias);

	// The threshold sweeps from below the field's minimum to above its maximum,
	// with edge_width of slack at each end, so burn = 0 leaves the item whole
	// (and shows no scorch at all) and burn = 1 leaves nothing.
	float threshold = mix(-edge_width, 1.0 + edge_width, burn);

	// Anti-alias the hole's rim over one pixel of the field's own slope, which
	// keeps it equally crisp however far the item is scaled up.
	float aa = max(fwidth(field), 0.0001);
	float alpha = smoothstep(threshold - aa, threshold + aa, field);

	// Distance behind the front, 0 at the rim of the hole and 1 at untouched
	// pixels. Char sits nearest the rim, the ember glow just behind it.
	float behind = clamp((field - threshold) / edge_width, 0.0, 1.0);
	float char_end = clamp(char_fraction, 0.001, 0.999);
	float charness = 1.0 - smoothstep(0.0, char_end, behind);
	float emberness = 1.0 - smoothstep(char_end, 1.0, behind);

	float flicker = flicker_speed == 0.0 || ember_flicker == 0.0
		? 1.0
		: mix(1.0 - ember_flicker, 1.0, value_noise(burn_position * 0.05 + TIME * flicker_speed));

	vec3 rgb = tex.rgb;
	rgb = mix(rgb, ember_color.rgb * ember_energy * flicker, emberness * ember_color.a);
	rgb = mix(rgb, char_color.rgb, charness * char_color.a);

	// The scorch only marks what is already drawn, it never adds anything
	// outside it, so the item's own alpha still bounds the whole effect. That
	// is also what makes this safe on an untextured Control: a StyleBox draw
	// samples white here and comes out as its own color, burnt.
	COLOR = vec4(rgb, tex.a * alpha) * COLOR;
}
