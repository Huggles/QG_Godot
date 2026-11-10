// A faction pennant flying from the flagpole: the flag at the hoist, a tail in the faction's colour
// flying out to a swallowtail, brass trim along the edges and across the seam. Pinned at the pole,
// the cloth ripples more toward the fly end, and the folds catch the light as they move.
shader_type canvas_item;

uniform sampler2D flag : source_color, filter_linear_mipmap;
uniform float flag_aspect = 2.0;          // texture width / height; the head shows its hoist side
uniform vec4 tail_color : source_color = vec4(0.5, 0.1, 0.1, 1.0);
uniform vec4 brass : source_color = vec4(0.851, 0.678, 0.361, 1.0);
uniform vec2 size = vec2(260.0, 56.0);    // the cloth's rect in px, set by the row
uniform float head = 80.0;                // px of flag at the hoist
uniform float notch = 18.0;               // depth of the swallowtail cut
uniform float sway = 1.5;                 // ripple amplitude at the fly end, px
uniform float speed = 1.6;
uniform float phase = 0.0;
uniform float hover = 0.0;                // 0..1, brightens the cloth under the mouse
uniform float active = 0.0;               // 0..1, the faction whose turn it is: trim glints

void fragment() {
	vec2 p = UV * size;
	float depth = clamp(p.x / size.x, 0.0, 1.0);
	float t = TIME * speed + phase;

	float ripple = sin(p.x * 0.055 - t) * sway * depth * depth;
	p.y -= ripple;

	// Swallowtail: the fly end cuts back toward the centre line.
	float fly = size.x - notch * (1.0 - abs(p.y / size.y - 0.5) * 2.0);
	float alpha = clamp(fly - p.x, 0.0, 1.0) * clamp(min(p.y, size.y - p.y) + 0.5, 0.0, 1.0);

	vec3 c;
	if (p.x < head) {
		c = texture(flag, vec2(p.x / (size.y * flag_aspect), clamp(p.y / size.y, 0.0, 1.0))).rgb;
	} else {
		float weave = 0.96 + 0.04 * sin(p.x * 1.7) * sin(p.y * 1.7);
		c = tail_color.rgb * weave;
	}

	// Brass trim: top and bottom edges, the seam after the flag, and a line inside the fly end.
	float trim = step(min(p.y, size.y - p.y), 2.0);
	trim = max(trim, step(abs(p.x - head), 1.5));
	trim = max(trim, step(abs(fly - p.x - 4.0), 1.2));
	float glint = active * (0.5 + 0.5 * sin(TIME * 2.5 - p.x * 0.04));
	c = mix(c, brass.rgb * (1.0 + 0.35 * glint), trim);

	float fold = cos(p.x * 0.055 - t) * depth;
	float pleat = sin(p.y * 0.25 + ripple * 0.6);
	c *= 1.0 + 0.16 * fold + 0.04 * pleat;

	c *= 1.0 + 0.15 * hover;
	COLOR = vec4(c, alpha);
}
