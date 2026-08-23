shader_type canvas_item;
render_mode unshaded;

// Spins the icon like a coin turning in 3D, with an optional size / opacity
// pulse layered on top.

uniform float spin_speed : hint_range(0.0, 5.0) = 0.4; // full turns per second
uniform int spin_axis : hint_enum("Vertical", "Horizontal") = 0;
uniform float perspective : hint_range(0.0, 1.0) = 0.6; // 0 = flat squash, 1 = strong foreshortening
uniform float edge_shade : hint_range(0.0, 1.0) = 0.4; // darkening as the face turns away
uniform float back_shade : hint_range(0.0, 1.0) = 0.2; // extra darkening of the back face

uniform float blink_speed : hint_range(0.0, 10.0) = 1.0; // full pulses per second
uniform float min_scale : hint_range(0.05, 1.0) = 0.7;
uniform float max_scale : hint_range(0.05, 1.0) = 1.0;
uniform float min_alpha : hint_range(0.0, 1.0) = 0.35;
uniform float max_alpha : hint_range(0.0, 1.0) = 1.0;
uniform float alpha_offset : hint_range(0.0, 1.0) = 0.0; // shifts the alpha pulse (in pulses)

// COLOR already holds TEXTURE * modulate by the time fragment() runs, so the
// modulate is carried across separately to avoid multiplying the texture in twice.
varying vec4 modulate;

// 0 -> 1 -> 0, sine eased so the pulse lingers at both extremes.
float pulse(float phase) {
	return 0.5 - 0.5 * cos(phase * TAU);
}

void vertex() {
	modulate = COLOR;
}

void fragment() {
	float angle = TIME * spin_speed * TAU;
	float ca = cos(angle);
	float sa = sin(angle);

	// 1 / camera distance, in units of the icon's half-width. 0 is orthographic,
	// which squashes without the near half growing.
	float inv_depth = perspective * 1.25;

	// The near half of a foreshortened quad projects taller than the quad
	// itself; shrink by that factor so the spin never clips the node's rect.
	float fit = 1.0 - 0.5 * abs(sa) * inv_depth;

	float scale = mix(min_scale, max_scale, pulse(TIME * blink_speed));
	vec2 p = (UV - 0.5) / (max(scale, 0.001) * fit);
	if (spin_axis == 1) {
		p = p.yx;
	}

	// Inverse of the perspective projection of a quad rotated about its
	// vertical axis: map the screen point back onto the quad's own surface.
	// Sign follows through, so the face mirrors itself past edge-on for free.
	float denom = ca - p.x * sa * inv_depth;
	denom = denom < 0.0 ? min(denom, -0.0001) : max(denom, 0.0001);
	float u = p.x / denom;
	float v = p.y * (1.0 + u * sa * inv_depth);

	vec2 local = vec2(u, v);
	if (spin_axis == 1) {
		local = local.yx;
	}

	// Clamp the lookup and mask instead of sampling outside the texture, so
	// whatever falls off the turning quad reads as transparent, not smeared.
	vec2 uv = local + 0.5;
	vec4 col = texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0)));
	col.a *= step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

	// Fake lighting: dim as the face turns edge-on, dim further on the back.
	float shade = 1.0 - edge_shade * (1.0 - abs(ca));
	if (ca < 0.0) {
		shade *= 1.0 - back_shade;
	}
	col.rgb *= shade;

	col.a *= mix(min_alpha, max_alpha, pulse(TIME * blink_speed + alpha_offset));

	COLOR = col * modulate;
}
