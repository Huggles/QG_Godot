shader_type canvas_item;

// A card status overlay. Its defaults are the "you can play this, but it would do
// nothing useful" look (Tag.NeedsAttention); CardUnavailableMaterial reuses it in red,
// steady and still, for a card that cannot be played. Meant for a ColorRect over a card with
// mouse_filter Ignore, like CardReflection. Three layers, back to front:
//   - a faint warm veil over the whole face, so the card reads a touch muted,
//   - a band of slowly drifting caution hatching just inside the border,
//   - an amber glow hugging the rounded edge that breathes in and out.
// The middle of the card stays almost clear, so its art and text remain readable.

// A flat, dark-amber veil over the whole face. Alpha 0 turns it off.
uniform vec4 wash_color : source_color = vec4(0.22, 0.13, 0.0, 1.0);
uniform float wash_alpha : hint_range(0.0, 1.0) = 0.06;

// The edge glow. Width is how far it reaches in from the edge, in units of the
// card's height; it fades with a squared falloff so the edge itself is brightest.
uniform vec4 glow_color : source_color = vec4(1.0, 0.66, 0.1, 1.0);
uniform float glow_width : hint_range(0.0, 0.5) = 0.08;
uniform float glow_intensity : hint_range(0.0, 1.0) = 0.5;

// The breathing: one full in-and-out every pulse_seconds, never dimmer than pulse_min.
uniform float pulse_seconds : hint_range(0.2, 10.0) = 2.6;
uniform float pulse_min : hint_range(0.0, 1.0) = 0.45;

// The hatched border. band is how deep it reaches in (card-height units),
// spacing the distance between stripes, speed how fast they drift along.
uniform vec4 stripe_color : source_color = vec4(1.0, 0.8, 0.25, 1.0);
uniform float stripe_band : hint_range(0.0, 0.5) = 0.055;
uniform float stripe_spacing : hint_range(0.005, 0.2) = 0.04;
uniform float stripe_intensity : hint_range(0.0, 1.0) = 0.2;
uniform float stripe_speed : hint_range(-1.0, 1.0) = 0.03;

// Rounds the effect off at the corners, as a fraction of the node's short side.
// Matches CardReflection so the two overlays share an outline.
uniform float corner_radius : hint_range(0.0, 0.5) = 0.06;

// Porter-Duff "over" for straight (non-premultiplied) colours.
vec4 over(vec4 top, vec4 bottom) {
	float a = top.a + bottom.a * (1.0 - top.a);
	vec3 rgb = (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / max(a, 1e-5);
	return vec4(rgb, a);
}

void fragment() {
	// Pixel size in UV from screen derivatives, rotation-safe; see CardReflection for why.
	vec2 uv_per_pixel = vec2(
		length(vec2(dFdx(UV.x), dFdy(UV.x))),
		length(vec2(dFdx(UV.y), dFdy(UV.y))));
	uv_per_pixel = max(uv_per_pixel, vec2(1e-6));
	float aspect = uv_per_pixel.y / uv_per_pixel.x;
	float px = uv_per_pixel.y;

	// Height units, so distances and stripe angles are true on screen.
	vec2 pos = vec2(UV.x * aspect, UV.y);
	vec2 half_size = vec2(aspect, 1.0) * 0.5;

	// Signed distance to the rounded outline (negative inside); depth is how far in a pixel sits.
	float radius = corner_radius * min(aspect, 1.0);
	vec2 corner = abs(pos - half_size) - half_size + vec2(radius);
	float dist = length(max(corner, vec2(0.0))) + min(max(corner.x, corner.y), 0.0) - radius;
	float mask = 1.0 - smoothstep(-px, px, dist);
	float depth = max(-dist, 0.0);

	float pulse = mix(pulse_min, 1.0, 0.5 + 0.5 * sin(TIME * TAU / pulse_seconds));
	float glow_falloff = 1.0 - smoothstep(0.0, max(glow_width, 1e-4), depth);
	float glow = glow_falloff * glow_falloff * glow_intensity * pulse;

	// 45-degree stripes, half on and half off, antialiased over one pixel, drifting with TIME.
	float s = (pos.x + pos.y - TIME * stripe_speed) / stripe_spacing;
	float aa = px / stripe_spacing;
	float on = smoothstep(0.25 - aa, 0.25 + aa, abs(fract(s) - 0.5));
	float band = 1.0 - smoothstep(stripe_band * 0.55, stripe_band, depth);
	float stripes = on * band * stripe_intensity;

	vec4 col = vec4(wash_color.rgb, wash_alpha);
	col = over(vec4(stripe_color.rgb, stripes), col);
	col = over(vec4(glow_color.rgb, glow), col);

	COLOR = vec4(col.rgb * COLOR.rgb, clamp(col.a, 0.0, 1.0) * mask * COLOR.a);
}
