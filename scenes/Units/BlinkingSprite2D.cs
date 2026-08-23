using Godot;
using System;

public partial class BlinkingSprite2D : TexturedSprite2D
{
	private Tween _tween;
	private bool _isBlinking = false;

	public override void _Ready()
	{
		Visible = false;
		_tween = CreateTween();
		_tween.SetTrans(Tween.TransitionType.Linear);
		_tween.SetEase(Tween.EaseType.InOut);
		_tween.SetLoops();
		_tween.TweenProperty(this, "modulate:a", 0.5f, GameSettings.DurationLongSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tween.TweenProperty(this, "modulate:a", 1.0f, GameSettings.DurationLongSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tween.Stop(); // Start with blinking stopped
	}

	public void StartBlinking()
	{
		if (!_isBlinking)
		{
			_tween.Play();
			_isBlinking = true;
		}
	}

	public void StopBlinking()
	{
		if (_isBlinking)
		{
			
			_tween.Stop();
			Modulate = new Color(this.Modulate.R, this.Modulate.G, this.Modulate.B, 1); // Reset alpha to fully visible
			_isBlinking = false;
		}
	}

	public new void Show()
	{
		Visible = true;
		StartBlinking();
	}

	public new void Hide()
	{
		StopBlinking();
		Visible = false;
	}
}
