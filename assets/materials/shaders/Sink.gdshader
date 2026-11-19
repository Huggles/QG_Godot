shader_type canvas_item;

// Sinks a sprite below a wavy waterline. 0 = afloat, 1 = fully under.
uniform float sink : hint_range(0.0, 1.0) = 0.0;
uniform float wave_amplitude = 0.015;
uniform float wave_frequency = 16.0;
uniform float foam_width = 0.05;
uniform vec4 foam_color : source_color = vec4(0.92, 0.97, 1.0, 1.0);
uniform vec4 water_tint : source_color = vec4(0.2, 0.35, 0.5, 1.0);

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR;
	// Waves only once the hull touches water, so sink = 0 leaves the sprite untouched.
	float wave = sin(UV.x * wave_frequency + TIME * 6.0) * wave_amplitude * min(sink * 10.0, 1.0);
	float line = 1.0 - sink * 1.05 + wave;
	if (UV.y > line) {
		discard;
	}
	float near = smoothstep(line - foam_width * 3.0, line, UV.y);
	color.rgb = mix(color.rgb, water_tint.rgb, near * 0.35);
	float foam = smoothstep(line - foam_width, line, UV.y);
	color.rgb = mix(color.rgb, foam_color.rgb, foam * 0.85);
	COLOR = color;
}
