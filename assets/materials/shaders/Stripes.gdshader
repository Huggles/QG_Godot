shader_type canvas_item;

// Draws repeating stripes cycling through an arbitrary list of colors.

// GLSL arrays are fixed size, so MAX_COLORS is the capacity and color_count
// decides how many of the entries are actually used.
const int MAX_COLORS = 4;

// Godot does not allow default values on uniform arrays, so the colors have to
// be filled in on the material (inspector, or set_shader_parameter("colors", [...])).
uniform vec4 colors[MAX_COLORS] : source_color;
// How many entries of `colors` to cycle through.
uniform int color_count : hint_range(1, 8) = 3;

// Width of a single stripe in UV units (one full cycle is color_count * stripe_width).
uniform float stripe_width : hint_range(0.01, 1.0) = 0.1;
// Stripe direction in degrees. 0 = vertical stripes, 90 = horizontal.
uniform float angle_degrees : hint_range(0.0, 360.0) = 45.0;
// Scrolls the stripes over time; 0 disables the animation.
uniform float scroll_speed = 0.0;

void fragment() {
	int count = clamp(color_count, 1, MAX_COLORS);

	float a = radians(angle_degrees);
	vec2 dir = vec2(cos(a), sin(a));

	// Project the UV onto the stripe direction, then bucket into bands.
	float pos = dot(UV, dir) + TIME * scroll_speed;
	float band = mod(floor(pos / stripe_width), float(count));
	// mod() can return count itself for tiny negative inputs, so clamp the index.
	int index = clamp(int(band), 0, count - 1);

	COLOR = colors[index] * COLOR;
}
