// Frosted glass for a menu panel's StyleBox: the blurred backdrop shows through, tinted by the
// style's own colour at the style's alpha. Children draw normally (they don't share the material).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 3.5;

void fragment() {
	vec3 frost = textureLod(screen_tex, SCREEN_UV, blur_lod).rgb;
	COLOR = vec4(mix(frost, COLOR.rgb, COLOR.a), smoothstep(0.0, 0.12, COLOR.a));
}
