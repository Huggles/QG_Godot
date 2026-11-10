// A faction pennant flying from the flagpole: the flag at the hoist, a tail in the faction's colour
// flying out to a swallowtail, brass trim along the edges and across the seam. Pinned at the pole,
// the cloth ripples more toward the fly end, and the folds catch the light as they move.
shader_type canvas_item;

uniform sampler2D flag : source_color, filter_linear_mipmap;
uniform vec4 tail_color : source_color = vec4(0.5, 0.1, 0.1, 1.0);
uniform vec4 brass : source_color = vec4(0.851, 0.678, 0.361, 1.0);
uniform vec2 size = vec2(260.0, 56.0);    // the cloth's rect in px, set by the row
uniform float margin = 8.0;               // headroom around the cloth for the ripple, px
uniform float head = 80.0;                // px of flag at the hoist
uniform float notch = 18.0;               // depth of the swallowtail cut
uniform float sway = 1.5;                 // ripple amplitude at the fly end, px
uniform float speed = 1.6;
uniform float phase = 0.0;
uniform float hover = 0.0;                // 0..1, brightens the cloth under the mouse
uniform float active = 0.0;               // 0..1, the faction whose turn it is: trim glints

void fragment() {
	// The rect is the cloth grown by `margin` above, below and past the fly end, so a ripple that
	// carries the cloth outside its resting outline is drawn instead of clipped.
	vec2 p = UV * (size + vec2(margin, 2.0 * margin)) - vec2(0.0, margin);
	float depth = clamp(p.x / size.x, 0.0, 1.0);
	float t = TIME * speed + phase;

	float ripple = sin(p.x * 0.055 - t) * sway * depth * depth;
	p.y -= ripple;

	// Swallowtail: the fly end cuts back toward the centre line.
	float fly = size.x - notch * (1.0 - abs(p.y / size.y - 0.5) * 2.0);
	float alpha = clamp(fly - p.x, 0.0, 1.0) * clamp(min(p.y, size.y - p.y) + 0.5, 0.0, 1.0);

	vec3 c;
	if (p.x < head) {
		c = texture(flag, vec2(clamp(p.x / head, 0.0, 1.0), clamp(p.y / size.y, 0.0, 1.0))).rgb;
	} else {
		float weave = 0.96 + 0.04 * sin(p.x * 1.7) * sin(p.y * 1.7);
		c = tail_color.rgb * weave;
	}

	// Brass trim on the cloth's outline: top and bottom edges, the swallowtail edge, and the seam
	// after the flag. The swallowtail band is measured along x, so it is widened to read as thick
	// as the straight edges where the cut runs diagonally.
	float trim = step(min(p.y, size.y - p.y), 2.0);
	trim = max(trim, step(abs(p.x - head), 1.5));
	trim = max(trim, step(fly - p.x, 2.6));
	float glint = active * (0.5 + 0.5 * sin(TIME * 2.5 - p.x * 0.04));
	c = mix(c, brass.rgb * (1.0 + 0.35 * glint), trim);

	float fold = cos(p.x * 0.055 - t) * depth;
	float pleat = sin(p.y * 0.25 + ripple * 0.6);
	c *= 1.0 + 0.16 * fold + 0.04 * pleat;

	c *= 1.0 + 0.15 * hover;
	COLOR = vec4(c, alpha);
}
