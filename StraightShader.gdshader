shader_type canvas_item;

// Tints the node's texture with a flat color and draws a hard border along the
// edge where its alpha runs out, so on a white-on-transparent silhouette the
// result is a colored shape with an outline. Reads the node's own texture;
// nothing to pass in.

// How many taps the edge search uses: DIRECTIONS around the circle, RINGS deep.
// RINGS sets the distance resolution - too few and a wide border shows as a
// scalloped edge rather than a clean band.
const int DIRECTIONS = 16;
const int RINGS = 6;

// Tints the texture. White leaves it untouched; on a white silhouette this
// becomes the fill color outright. Its alpha multiplies into the fill.
uniform vec4 fill_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform vec4 border_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
// Thickness of the border, in texture pixels.
uniform float border_width : hint_range(0.0, 64.0) = 4.0;
// Which side of the edge the border sits on: 0 draws it entirely inside the
// shape, 1 entirely outside, 0.5 straddles it. Anything outside the shape is
// clipped by the node's own rect, so on a texture whose art touches its border
// (StraightIcon.png does, top right) the outward half is cut off there - keep
// this at 0 unless the texture has transparent padding to grow into.
uniform float border_outward : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);

	float outward = border_width * border_outward;
	float inward = border_width * (1.0 - border_outward);

	// Two morphology passes over a disc of taps: the max grows the silhouette
	// outwards by `outward` (dilate), the min shrinks it inwards by `inward`
	// (erode). Taking them from the texture's own alpha rather than a hard
	// threshold keeps its antialiased edge intact in both.
	float grown = tex.a;
	float shrunk = tex.a;
	for (int r = 1; r <= RINGS; r++) {
		float radius = float(r) / float(RINGS);
		for (int s = 0; s < DIRECTIONS; s++) {
			float ang = TAU * float(s) / float(DIRECTIONS);
			vec2 dir = vec2(cos(ang), sin(ang)) * radius;

			// The dilate pass reads anything past the texture edge as
			// transparent, so the border does not smear along the edges.
			vec2 uv_out = UV + dir * outward * TEXTURE_PIXEL_SIZE;
			if (uv_out.x >= 0.0 && uv_out.x <= 1.0 && uv_out.y >= 0.0 && uv_out.y <= 1.0) {
				grown = max(grown, texture(TEXTURE, uv_out).a);
			}

			// The erode pass instead ignores taps past the edge, since reading
			// them as transparent would eat into art that runs to the border
			// and leave a stray border stripe along it.
			vec2 uv_in = UV + dir * inward * TEXTURE_PIXEL_SIZE;
			if (uv_in.x >= 0.0 && uv_in.x <= 1.0 && uv_in.y >= 0.0 && uv_in.y <= 1.0) {
				shrunk = min(shrunk, texture(TEXTURE, uv_in).a);
			}
		}
	}

	// The shrunk shape is the fill, the ring between shrunk and grown is the
	// border, and grown is the full extent of what gets drawn.
	float fill_a = shrunk * fill_color.a;
	float border_a = max(grown - shrunk, 0.0) * border_color.a;
	float out_a = fill_a + border_a;
	vec3 out_rgb = out_a > 0.0
		? (tex.rgb * fill_color.rgb * fill_a + border_color.rgb * border_a) / out_a
		: vec3(0.0);

	COLOR = vec4(out_rgb, min(out_a, 1.0)) * COLOR;
}
