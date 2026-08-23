shader_type canvas_item;
render_mode unshaded;

// Pulses the sprite's size and opacity over time to draw attention to it.

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
	float t = TIME * blink_speed;

	// Scale around the centre of the sprite. The sprite can only shrink
	// (scale <= 1.0), since a canvas_item cannot draw outside its own rect.
	float scale = mix(min_scale, max_scale, pulse(t));
	vec2 uv = (UV - 0.5) / max(scale, 0.001) + 0.5;

	// Clamp the lookup and mask instead of sampling outside the texture, so
	// the shrunken sprite gets transparent margins rather than smeared edges.
	vec4 col = texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0)));
	col.a *= step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

	col.a *= mix(min_alpha, max_alpha, pulse(t + alpha_offset));

	COLOR = col * modulate;
}
