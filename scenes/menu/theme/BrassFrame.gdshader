// Living brass trim drawn over a panel or button: corner brackets, a light that travels round the
// border, a slow outer glow and an optional glint across the face. The rect is the panel grown by
// `inset` pixels on every side, so the effects can reach outside the panel's edge.
shader_type canvas_item;

uniform vec4 brass : source_color = vec4(0.851, 0.678, 0.361, 1.0);
uniform vec4 spark : source_color = vec4(1.0, 0.93, 0.74, 1.0);
uniform float inset = 10.0;
uniform float energy = 1.0;          // 0..1, driven by hover for buttons

uniform float bracket_alpha = 0.9;
uniform float bracket_idle = 0.55;    // bracket strength at zero energy
uniform float bracket_len = 22.0;
uniform float bracket_gap = 4.0;

uniform float sweep_alpha = 0.85;
uniform float sweep_speed = 260.0;   // px per second along the perimeter
uniform float sweep_width = 90.0;

uniform float glow_alpha = 0.18;
uniform float glow_size = 9.0;
uniform float pulse_period = 4.0;

uniform float sheen_alpha = 0.0;
uniform float sheen_period = 5.0;
uniform float phase = 0.0;

float perimeter_pos(vec2 p, vec2 s) {
	float dl = p.x, dr = s.x - p.x, dt = p.y, db = s.y - p.y;
	float m = min(min(dl, dr), min(dt, db));
	if (m == dt) return p.x;
	if (m == dr) return s.x + p.y;
	if (m == db) return s.x + s.y + (s.x - p.x);
	return 2.0 * s.x + s.y + (s.y - p.y);
}

void fragment() {
	vec2 full = 1.0 / fwidth(UV);                 // overlay size in px
	vec2 p = UV * full - vec2(inset);             // position relative to the panel's corner
	vec2 s = full - vec2(2.0 * inset);            // panel size
	float t = TIME + phase;

	// Signed distance to the panel edge: >0 inside, <0 outside.
	float edge = min(min(p.x, s.x - p.x), min(p.y, s.y - p.y));
	float cx = min(p.x, s.x - p.x), cy = min(p.y, s.y - p.y);

	vec4 col = vec4(0.0);

	// Corner brackets, standing just off the panel.
	float g = bracket_gap;
	bool inBand = edge < -g + 0.5 && edge > -g - 1.5;
	bool nearCorner = (cx > -g - 2.0 && cx < bracket_len && cy > -g - 2.0 && cy < bracket_len);
	bool horiz = cy < -g + 0.5 && cy > -g - 1.5 && cx > -g - 1.5 && cx < bracket_len;
	bool vert  = cx < -g + 0.5 && cx > -g - 1.5 && cy > -g - 1.5 && cy < bracket_len;
	if ((horiz || vert) && nearCorner && inBand) {
		col = vec4(brass.rgb, bracket_alpha * mix(bracket_idle, 1.0, energy));
	}

	// A light travelling round the border line (the panel's outermost two pixels).
	if (edge >= 0.0 && edge < 2.0) {
		float per = 2.0 * (s.x + s.y);
		float head = mod(t * sweep_speed, per);
		float d = abs(perimeter_pos(p, s) - head);
		d = min(d, per - d);
		float k = exp(-(d * d) / (sweep_width * sweep_width)) * sweep_alpha * energy;
		col.rgb = mix(col.rgb, spark.rgb, col.a > 0.0 ? k : 1.0);
		col.a = max(col.a, k);
	}

	// Soft glow just outside the panel, breathing slowly.
	if (edge < 0.0 && col.a < 0.01) {
		float pulse = 0.65 + 0.35 * sin(t * 6.2831 / pulse_period);
		float a = exp(edge / glow_size * 2.2) * glow_alpha * pulse * energy;
		col = vec4(brass.rgb, a);
	}

	// A diagonal glint sweeping across the face now and then.
	if (sheen_alpha > 0.0 && edge >= 0.0) {
		float cyc = fract(t / sheen_period);
		float pos = mix(-0.4, 1.6, clamp(cyc * 2.2, 0.0, 1.0)) * (s.x + s.y * 0.6);
		float d = (p.x + p.y * 0.6) - pos;
		float k = exp(-(d * d) / (900.0)) * sheen_alpha * energy;
		col.rgb = mix(col.rgb, spark.rgb, col.a > 0.0 ? k : 1.0);
		col.a = max(col.a, k);
	}

	COLOR = col;
}
