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

// Width of a single stripe, in the node's own pixels (one full cycle is
// color_count * stripe_width_pixels). Because it is measured in node pixels rather
// than UV, it does not change with the size or aspect of the texture underneath.
uniform float stripe_width_pixels : hint_range(1.0, 400.0) = 80.0;
// Stripe direction in degrees. 0 = vertical stripes, 90 = horizontal.
uniform float angle_degrees : hint_range(0.0, 360.0) = 45.0;
// Scrolls the stripes over time, in pixels per second; 0 disables the animation.
uniform float scroll_speed = 0.0;

// Position within the node, in the node's local pixels. Deliberately not UV: UV is
// normalized 0..1 per axis, so on a non-square texture its two axes are in different
// units (which sheared the angle) and its scale follows the texture's dimensions
// (which made stripe width depend on how big the source image happened to be).
// Local pixels are square and texture-independent, so both problems go away at once.
varying vec2 local_position;

void vertex() {
	local_position = VERTEX;
}

void fragment() {
	int count = clamp(color_count, 1, MAX_COLORS);

	float a = radians(angle_degrees);
	vec2 dir = vec2(cos(a), sin(a));

	// Project the position onto the stripe direction, then bucket into bands.
	float pos = dot(local_position, dir) + TIME * scroll_speed;
	float band = mod(floor(pos / stripe_width_pixels), float(count));
	// mod() can return count itself for tiny negative inputs, so clamp the index.
	int index = clamp(int(band), 0, count - 1);

	COLOR = colors[index] * COLOR;
}
