shader_type canvas_item;

// Out-of-supply unit: holds the faction color (the sprite's modulate), then
// slowly fades toward black and back once per cycle. Runs on real time, so the
// game speed setting does not affect it.

// Seconds for one full color -> black -> color cycle.
uniform float cycle_seconds : hint_range(0.1, 10.0) = 6.0;
// Fraction of the cycle spent dark, fades included.
uniform float dark_fraction : hint_range(0.0, 1.0) = 0.35;
// Fraction of the cycle each fade (in and out) takes. 0 is a hard snap.
uniform float fade_fraction : hint_range(0.0, 0.5) = 0.15;
// How black "black" gets. 1 is fully black; below 1 the unit stays visible.
uniform float darkness : hint_range(0.0, 1.0) = 0.45;

void fragment() {
	// COLOR is TEXTURE * modulate here, so it already carries the faction color.
	float t = fract(TIME / max(cycle_seconds, 0.001));
	float fade = max(fade_fraction, 0.0001);
	float dark = smoothstep(0.0, fade, t) * (1.0 - smoothstep(dark_fraction - fade, dark_fraction, t));
	COLOR.rgb *= 1.0 - darkness * dark;
}
