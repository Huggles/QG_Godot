// A faction banner hanging from its rod: the flag as the head, a tail in the faction's colour below it
// ending in a swallowtail, brass trim down the sides and across the seam. The cloth ripples, more so
// toward the hem, and the folds catch the light as they move.
shader_type canvas_item;

uniform sampler2D flag : source_color, filter_linear_mipmap;
uniform vec4 tail_color : source_color = vec4(0.5, 0.1, 0.1, 1.0);
uniform vec4 brass : source_color = vec4(0.851, 0.678, 0.361, 1.0);
uniform vec2 size = vec2(88.0, 120.0);   // the cloth's rect in px, set by the row
uniform float flag_height = 44.0;         // px of the head showing the flag
uniform float notch = 16.0;               // depth of the swallowtail cut
uniform float sway = 1.5;                 // ripple amplitude at the hem, px
uniform float speed = 1.6;
uniform float phase = 0.0;
uniform float hover = 0.0;                // 0..1, brightens the cloth under the mouse
uniform float active = 0.0;               // 0..1, the faction whose turn it is: trim glints

void fragment() {
	vec2 p = UV * size;
	float depth = clamp(p.y / size.y, 0.0, 1.0);
	float t = TIME * speed + phase;

	// Pinned along the rod, free at the hem.
	float ripple = sin(p.y * 0.075 - t) * sway * depth * depth;
	p.x -= ripple;

	// Swallowtail: the hem rises toward the centre line.
	float hem = size.y - notch * (1.0 - abs(p.x / size.x - 0.5) * 2.0);
	float alpha = clamp(hem - p.y, 0.0, 1.0) * clamp(min(p.x, size.x - p.x) + 0.5, 0.0, 1.0);

	vec3 c;
	if (p.y < flag_height) {
		c = texture(flag, vec2(clamp(p.x / size.x, 0.0, 1.0), p.y / flag_height)).rgb;
	} else {
		// A faint weave so the tail reads as cloth, not a flat fill.
		float weave = 0.96 + 0.04 * sin(p.x * 1.7) * sin(p.y * 1.7);
		c = tail_color.rgb * weave;
	}

	// Brass trim: side borders, the seam under the flag, and a hem line.
	float trim = 0.0;
	trim = max(trim, step(min(p.x, size.x - p.x), 2.0));
	trim = max(trim, step(abs(p.y - flag_height), 1.5));
	trim = max(trim, step(abs(hem - p.y - 3.0), 1.2));
	float glint = active * (0.5 + 0.5 * sin(TIME * 2.5 - p.y * 0.05));
	c = mix(c, brass.rgb * (1.0 + 0.35 * glint), trim);

	// Folds: light and shade travelling with the ripple, stronger toward the hem.
	float fold = cos(p.y * 0.075 - t) * depth;
	float pleat = sin(p.x * 0.22 + ripple * 0.6);
	c *= 1.0 + 0.16 * fold + 0.05 * pleat;

	c *= 1.0 + 0.15 * hover;
	COLOR = vec4(c, alpha);
}
