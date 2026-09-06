shader_type canvas_item;

// A line inked just inside the edge of a nine patch's art, following the shape's
// own alpha silhouette instead of the node's rectangle. On the torn paper frames
// this project uses, that means every notch and nick in the tear gets outlined,
// which is what stops a pale panel dissolving into a pale background.
//
// Inset, not outset: the line is drawn over the art's own outer pixels, so the
// panel never grows and never spills past its rect. A fragment is on the line
// when the art around it, one line width away, is markedly more transparent than
// the art under it - an erosion of the silhouette, sampled on two rings so a
// sharp notch cannot slip between the taps and leave a gap in the line.
//
// Three things about nine patches that this shader is built around, and that are
// easy to get wrong:
//
// 1. UV here is normalized over the WHOLE texture, not over the node and not
//    over the region. Godot remaps it through the patch layout and then through
//    `region_rect`, in the fragment shader, before this code runs. So
//    `texture(TEXTURE, UV)` is exactly the pixel being drawn - and a neighbour
//    tap travels in atlas space, which on a sprite sheet means it can reach into
//    the next sprite. `region_rect_pixels` is what stops that.
// 2. TEXTURE_PIXEL_SIZE is NOT the size of a texel when a region is set - Godot
//    feeds the patch mapper 1 / region_size, so on a 128 pixel tile in a 532
//    pixel sheet it is off by a factor of four. Nothing here uses it; texel size
//    comes from textureSize, and step size comes from a derivative.
// 3. COLOR arrives already multiplied by the texture and by modulate. So the art
//    is read out of COLOR rather than sampled again, and there is no trailing
//    `* COLOR` - that would square the art. It also means `outline_color` is
//    literal: a modulate of 0.63 grey dims the panel but not the line.
//
// COLOR.a is deliberately never written. That is what keeps the art's own edge
// anti-aliasing, keeps modulate's alpha, and keeps `draw_center = false` working
// - Godot signals a skipped centre by zeroing the incoming alpha.

// Taps around the ring, and around a second, smaller ring inside it. The outer
// ring alone sets the width: with 16 taps the width wobbles by 2% as the edge
// turns, which does not read at these sizes. The inner ring is there purely for
// the tear's concave notches - a notch that ends inside the ring is invisible to
// the ring, whose taps straddle it and land back in opaque art, and the line
// breaks exactly where the notch bites in.
const int OUTER_TAPS = 16;
const int INNER_TAPS = 8;
// Radius of the inner ring as a fraction of the line width. Nearer the centre
// than this and it only repeats what the fragment already knows about itself.
const float INNER_RING = 0.6;
// Ceiling on the measured source-to-screen ratio, in source pixels per screen
// pixel. A Tile or Tile Fit stretch mode makes UV jump back a whole tile at each
// seam, and a derivative across that jump is enormous; without a ceiling the
// ring would swing across the sheet and ink a black band down every seam. 16 is
// well past any honest minification, so it only ever catches the jumps.
const float MAX_SOURCE_PIXELS_PER_SCREEN_PIXEL = 16.0;

// The line itself. Alpha scales how strongly it is mixed over the art, so a
// softer line is a lower alpha rather than a lighter colour.
uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
// Turns the line off entirely at 0.
uniform float outline_amount : hint_range(0.0, 1.0) = 1.0;
// Line width in SCREEN pixels, held constant across all nine patches even though
// the corners draw the source at 1:1 while the centre is stretched many times
// over. Much past 6 and the line starts closing over the tear's finer notches.
uniform float outline_thickness_pixels : hint_range(0.0, 16.0) = 3.0;

// The node's `region_rect`, copied here in texture pixels as (x, y, width,
// height). It has to be repeated because a shader cannot read it back off the
// node, and it has to be right: on a sprite sheet the gutter between tiles is a
// few pixels, so a tap that is allowed to wander past the region hits the
// neighbouring sprite and inks a line along nothing. Leave at 0 when the node
// draws the whole texture.
uniform vec4 region_rect_pixels = vec4(0.0, 0.0, 0.0, 0.0);

// The alpha a tap outside the region counts as. 0 - transparent - is what closes
// the line around the panel's outer rim, and is also the truth on a sheet with a
// transparent gutter. Raise it towards 1 for art that deliberately bleeds to the
// region's edge and should be left unringed there.
//
// Deliberately not a clamp to the region's edge: clamping would smear the border
// texel outwards, so every tap near the rim would come back opaque and the line
// would vanish along exactly the edge it is most wanted on.
uniform float outside_region_alpha : hint_range(0.0, 1.0) = 0.0;

// Where the silhouette's edge sits, as the ring's alpha RELATIVE to the
// fragment's own, and how softly it is crossed. Relative rather than absolute so
// that evenly semi-transparent art has no edge anywhere in it - an absolute test
// would read a flat 40% panel as one enormous edge and paint all of it black.
uniform float edge_threshold : hint_range(0.0, 1.0) = 0.5;
uniform float edge_softness : hint_range(0.01, 0.5) = 0.25;

// How many source pixels one screen pixel covers. 0 measures it per fragment
// from screen space derivatives, which is correct for the default Stretch axis
// mode and is what makes one line width hold across corner, edge and centre
// patches alike.
//
// Set it explicitly for a node whose axis stretch mode is Tile or Tile Fit.
// Tiling makes UV sawtooth, and the derivative at each seam is a spike rather
// than a rate; MAX_SOURCE_PIXELS_PER_SCREEN_PIXEL keeps that from becoming a
// black band, but it still costs a hairline gap in the line at every seam.
// Tiling also draws the source at a fixed scale everywhere, which is the whole
// point of it - so the ratio is a constant and simply saying it is both exact
// and free. Use 1.0 for a tiled node at 1:1, or 1.0 / TileScale for one driven
// by ParentSizedNinePatchRect.
uniform float source_pixels_per_screen_pixel : hint_range(0.0, 8.0) = 0.0;

// Answers a tap that landed outside the node's region with `outside_region_alpha`
// instead of the alpha that was actually sampled there.
//
// The sampling itself deliberately stays at the call site rather than moving in
// here. Godot only exposes the built ins inside fragment(), so naming TEXTURE in
// a function of our own is "Unknown identifier in expression" - and handing the
// sampler in as a parameter instead compiles, but trips a binding bug in the
// shader compiler ("!actions.custom_samplers.has(...tex_builtin)"), which leaves
// the tap reading from nothing in particular. Passing the sampled alpha in is
// the way out that keeps the region test in one place.
float masked_alpha(float sampled, vec2 uv, vec2 region_min, vec2 region_max) {
	vec2 within = step(region_min, uv) * step(uv, region_max);
	return mix(outside_region_alpha, sampled, within.x * within.y);
}

void fragment() {
	if (outline_amount > 0.0 && outline_thickness_pixels > 0.0) {
		vec2 texel = 1.0 / vec2(textureSize(TEXTURE, 0));

		vec2 region_min = vec2(0.0);
		vec2 region_max = vec2(1.0);
		if (region_rect_pixels.z > 0.0 && region_rect_pixels.w > 0.0) {
			region_min = region_rect_pixels.xy * texel;
			region_max = (region_rect_pixels.xy + region_rect_pixels.zw) * texel;
		}

		// How far one screen pixel reaches in UV. Godot remaps UV through the
		// patch layout inside the fragment shader, before this code runs, so a
		// derivative of it is the real per-patch rate - roughly a texel in the
		// corners, a fraction of one across the stretched centre - rather than
		// the rect's average. fwidth rather than a per-axis dFdx/dFdy pair
		// because the pair collapses to zero on a rotated node, which would
		// erase the line; fwidth only over-estimates.
		vec2 uv_per_pixel = texel * source_pixels_per_screen_pixel;
		if (source_pixels_per_screen_pixel <= 0.0) {
			uv_per_pixel = min(fwidth(UV), texel * MAX_SOURCE_PIXELS_PER_SCREEN_PIXEL);
		}
		vec2 radius = uv_per_pixel * outline_thickness_pixels;

		// Erode the silhouette: the least opaque thing a line width away.
		// textureLod pins every tap to the full size image - an implicit
		// derivative is not something to lean on inside a loop, and a mip would
		// in any case blend in whatever sprite is packed next door.
		float own = masked_alpha(textureLod(TEXTURE, UV, 0.0).a, UV, region_min, region_max);
		float ring = 1.0;
		for (int i = 0; i < OUTER_TAPS; i++) {
			float angle = TAU * float(i) / float(OUTER_TAPS);
			vec2 tap = UV + vec2(cos(angle), sin(angle)) * radius;
			ring = min(ring, masked_alpha(textureLod(TEXTURE, tap, 0.0).a, tap, region_min, region_max));
		}
		// Half a step out of phase with the outer ring, so the two do not line
		// up along the same spokes and leave the same blind angles.
		for (int i = 0; i < INNER_TAPS; i++) {
			float angle = TAU * (float(i) + 0.5) / float(INNER_TAPS);
			vec2 tap = UV + vec2(cos(angle), sin(angle)) * radius * INNER_RING;
			ring = min(ring, masked_alpha(textureLod(TEXTURE, tap, 0.0).a, tap, region_min, region_max));
		}

		// The ring measured against this fragment rather than against a fixed
		// level. Besides ignoring evenly translucent art, this is what
		// anti-aliases the line's outer side: a half covered pixel on the
		// silhouette has half the alpha but the same ratio as a fully covered
		// one, so it comes out fully black at half coverage, which is exactly
		// the right edge.
		float ratio = clamp(ring / max(own, 0.001), 0.0, 1.0);
		float outline = 1.0 - smoothstep(edge_threshold - edge_softness, edge_threshold + edge_softness, ratio);

		COLOR.rgb = mix(COLOR.rgb, outline_color.rgb, outline * outline_color.a * outline_amount);
	}
}
