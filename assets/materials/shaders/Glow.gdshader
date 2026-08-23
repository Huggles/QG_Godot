shader_type canvas_item;

// Draws a glow along the edge between the opaque and the transparent pixels of
// the node's texture, so on a white-on-transparent silhouette the glow traces
// the outline. Reads the node's own texture; nothing to pass in.

// How many taps the edge search uses: DIRECTIONS around the circle, RINGS deep.
// RINGS also sets the distance resolution - too few and the glow shows as
// concentric bands rather than a smooth falloff.
const int DIRECTIONS = 16;
const int RINGS = 8;

uniform vec4 glow_color : source_color = vec4(0.972549, 0.0, 0.952941, 1.0);

// Reach of the glow, in texture pixels.
uniform float glow_width : hint_range(0.0, 64.0) = 8.0;
// Shapes the falloff: 1 is linear, higher values pull the glow tight to the edge.
uniform float glow_falloff : hint_range(0.25, 8.0) = 2.0;
// Opacity of the glow at the edge itself.
uniform float glow_alpha : hint_range(0.0, 1.0) = 1.0;
// How far the glow bleeds inwards, as a fraction of glow_width. 0 = outside only.
uniform float inner_glow : hint_range(0.0, 1.0) = 0.5;
// Opacity of the texture itself. 0 leaves only the glow visible.
uniform float fill_alpha : hint_range(0.0, 1.0) = 1.0;
// Tints the texture. White leaves it untouched; on a white silhouette this
// becomes the fill color outright. Its alpha multiplies into fill_alpha.
uniform vec4 fill_color : source_color = vec4(1.0, 1.0, 1.0, 1.0); 

// Pulsing. Set pulse_speed to 0 for a steady glow.
uniform float pulse_speed = 3.0;
uniform float pulse_min : hint_range(0.0, 1.0) = 0.35;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec4 fill = tex * fill_color;

	// Walk outwards in rings and keep the strongest weighted hit, which gives
	// the distance to the nearest opaque pixel (near_solid) and to the nearest
	// transparent one (near_empty), both as a 0..1 closeness value. The first
	// ring carries weight 1.0 so a pixel sitting on the edge reads as a full hit.
	float near_solid = 0.0;
	float near_empty = 0.0;
	for (int r = 1; r <= RINGS; r++) {
		float radius = float(r) / float(RINGS);
		float weight = 1.0 - float(r - 1) / float(RINGS);
		for (int s = 0; s < DIRECTIONS; s++) {
			float ang = TAU * float(s) / float(DIRECTIONS);
			vec2 uv = UV + vec2(cos(ang), sin(ang)) * radius * glow_width * TEXTURE_PIXEL_SIZE;
			// Outside the texture counts as transparent rather than clamping to
			// the border pixel, which would smear the glow along the edges.
			bool outside = uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0;
			float a = outside ? 0.0 : texture(TEXTURE, uv).a;
			near_solid = max(near_solid, a * weight);
			near_empty = max(near_empty, (1.0 - a) * weight);
		}
	}

	// Outside the shape the glow comes from nearby solid pixels, inside it from
	// nearby empty ones, so the band straddles the outline.
	float outer = near_solid * (1.0 - tex.a);
	float inner = near_empty * tex.a * inner_glow;
	float edge = pow(clamp(max(outer, inner), 0.0, 1.0), glow_falloff);

	float pulse = pulse_speed == 0.0
		? 1.0
		: mix(pulse_min, 1.0, 0.5 + 0.5 * sin(TIME * pulse_speed));

	// Composite the glow over the texture, so the inner half of the band stays
	// visible on top of an opaque shape.
	float glow_a = edge * glow_alpha * pulse * glow_color.a;
	float fill_a = fill.a * fill_alpha;
	float out_a = glow_a + fill_a * (1.0 - glow_a);
	vec3 out_rgb = out_a > 0.0
		? (glow_color.rgb * glow_a + fill.rgb * fill_a * (1.0 - glow_a)) / out_a
		: vec3(0.0);

	COLOR = vec4(out_rgb, out_a) * COLOR;
}
