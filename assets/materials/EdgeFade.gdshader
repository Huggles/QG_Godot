// Fades a texture to transparent towards its edges, so art blends into whatever is behind it.
shader_type canvas_item;

// How far in from each edge the fade reaches, as a fraction of the width and of the height.
uniform float fade_x : hint_range(0.0, 0.5) = 0.12;
uniform float fade_y : hint_range(0.0, 0.5) = 0.22;

void fragment() {
	vec4 colour = texture(TEXTURE, UV);
	float x = smoothstep(0.0, fade_x, UV.x) * smoothstep(0.0, fade_x, 1.0 - UV.x);
	float y = smoothstep(0.0, fade_y, UV.y) * smoothstep(0.0, fade_y, 1.0 - UV.y);
	COLOR = vec4(colour.rgb, colour.a * x * y);
}
