// Frosted glass for a menu panel's StyleBox: the blurred backdrop shows through, tinted by the
// style's own colour at the style's alpha. Children draw normally (they don't share the material).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 3.5;
// The StyleBox's own fill alpha, for a panel that fades in and out through modulate. COLOR.a then
// falls below it as the panel fades, and the blur fades with it. 0 = never faded, the blur stays solid.
uniform float fill_alpha = 0.0;

void fragment() {
	vec3 frost = textureLod(screen_tex, SCREEN_UV, blur_lod).rgb;
	if (fill_alpha > 0.0) {
		float a = clamp(COLOR.a / fill_alpha, 0.0, 1.0);
		COLOR = vec4(mix(frost, COLOR.rgb, clamp(COLOR.a / max(a, 0.001), 0.0, 1.0)), a);
	} else {
		COLOR = vec4(mix(frost, COLOR.rgb, COLOR.a), smoothstep(0.0, 0.12, COLOR.a));
	}
}
