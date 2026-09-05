using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The banner at the bottom of the screen that says what is being waited on — a prompt title, or a
/// card's own note about what it just did.
///
/// The panel it sits in is fixed: it is a HUD frame anchored to the bottom edge, and growing it would
/// push it off screen rather than reveal more of it. So the TEXT gives way instead — see
/// <see cref="FitTextToBox"/>. The label itself is sized by <see cref="MenuPanel"/> to the panel's
/// rect and reports no minimum of its own, which is what keeps it from spilling out sideways.
/// </summary>
public partial class PlayerActionLabel : Label, LoadableUI
{    
	public static PlayerActionLabel Instance;
	
	private MenuPanel MenuPanel => GetNode<MenuPanel>("%MenuPanel");

	/// <summary>
	/// The plate behind the banner text. Tinted with whatever the client is busy with, so the banner
	/// says whose prompt it is by colour as well as by wording — see <see cref="FactionFocusTint"/>.
	/// </summary>
	private Control BackgroundPanel => GetNode<Control>("%MenuPanel/BackgroundContainer/Panel");

	private FactionFocusTint _focusTint;

	/// <summary>
	/// The size the banner was authored at, read off the scene before anything overrides it. Every fit
	/// starts here and only goes down, so a short line still draws at the intended size.
	/// </summary>
	private int _baseFontSize;

	/// <summary>
	/// How small the text may get before it stops being worth reading. At the authored panel size this
	/// is around four lines of text, well past anything the game says here.
	/// </summary>
	private const int MinimumFontSize = 11;

	/// <summary>
	/// The theme sizes a fit has to move together: measuring is done with the normal font, so a bold or
	/// italic run left at its own size could still overflow the box the measurement cleared.
	/// </summary>
	private static readonly string[] FontSizeItems =
	{
		"normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size",
		"mono_font_size",
	};

	public override void _Ready()
	{        
		Instance = this;

		_baseFontSize = GetThemeFontSize("normal_font_size");
		// The panel lays this label out in its own sort pass, which can land after the text arrives —
		// and does on the first banner of a game, when the label is still (0,0) at that moment. Fitting
		// again on every resize is what makes that case, and a later window resize, come out right.
		Resized += FitTextToBox;

		_focusTint = new FactionFocusTint(BackgroundPanel);
		_focusTint.Attach();

		// Hide by default until LoadUI is called
		Visible = false;
	}

	/// <summary>
	/// EventBus is a process-wide static, so the tint handler would outlive the game scene if it were
	/// left connected — and one stale handler aborts the whole emission for every handler behind it.
	/// </summary>
	public override void _ExitTree()
	{
		_focusTint?.Detach();
		_focusTint = null;
	}

	public void LoadUI()
	{
		HideNode();        
	}

	public static void ShowText(string text, Faction faction = (Faction)(-1))
	{
		ShowText(text, -1, faction);

	}
	public static void ShowText(string text, int duration, Faction faction = (Faction)(-1)){        
		Instance.ShowTextForDuration(text, duration, faction);
	}

	public void ShowTextForDuration(string text, int duration = -1, Faction faction = (Faction)(-1))
	{
		MenuPanel.MouseFilter = MouseFilterEnum.Stop;
		this.MouseFilter = MouseFilterEnum.Stop;
		Text = text;
		FitTextToBox();
		Visible = true;
		if (MenuPanel != null)
			
		{
			MenuPanel.Visible = true;           
		}
		if(duration > -1){
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Elapsed += (s, e)=>{
				HideNode();
				timer.Dispose();
			};
			timer.Start();            
		}        
	}

	/// <summary>
	/// Shrink the font until the wrapped text fits the box the panel gives this label, so a long
	/// notification is never clipped, scrolled out of sight, or drawn over the panel's edge.
	///
	/// Measured off the font rather than <c>GetContentHeight</c>: the label only shapes its lines when
	/// it next draws, so its own content height is still the PREVIOUS text's at the point this runs —
	/// which is the moment the text is set, sometimes off the main thread from an animation.
	/// </summary>
	private void FitTextToBox()
	{
		Font font = GetThemeFont("normal_font");
		if (font == null) return;

		// Whatever the label's own background reserves is not text space; zero as the scene stands, but
		// padding added there later has to come off the box rather than out of the fit.
		Vector2 padding = GetThemeStylebox("normal")?.GetMinimumSize() ?? Vector2.Zero;
		float width = Size.X - padding.X;
		float height = Size.Y - padding.Y;
		// Before the panel's first sort pass there is no box to measure against. The Resized hook above
		// redoes the fit as soon as there is one.
		if (width < 1f || height < 1f) return;

		// Tags do not print, so what has to fit is the plain text the label parsed out of the BBCode.
		int lineSeparation = GetThemeConstant("line_separation");

		for (int fontSize = _baseFontSize; fontSize > MinimumFontSize; fontSize--)
		{
			if (TextHeight(font, Text, fontSize, width, lineSeparation) <= height)
			{
				ApplyFontSize(fontSize);
				return;
			}
		}

		ApplyFontSize(MinimumFontSize);
	}

	/// <summary>How tall the text draws once wrapped to <paramref name="width"/>.</summary>
	private static float TextHeight(Font font, string text, int fontSize, float width, int lineSeparation)
	{
		Vector2 textSize = font.GetMultilineStringSize(text, HorizontalAlignment.Center, width, fontSize);
		if (lineSeparation == 0) return textSize.Y;

		// The measurement stacks its lines flush; the label draws them a line_separation apart, so that
		// gap has to be added back once per break.
		float lineHeight = font.GetHeight(fontSize);
		int lines = lineHeight > 0f ? Mathf.Max(1, Mathf.RoundToInt(textSize.Y / lineHeight)) : 1;
		return textSize.Y + lineSeparation * (lines - 1);
	}

	private void ApplyFontSize(int fontSize)
	{
		foreach (string item in FontSizeItems)
		{
			AddThemeFontSizeOverride(item, fontSize);
		}
	}

	public static void HideText()
	{
		Instance.HideNode();
	}
	public void HideNode()
	{
		Visible = false;
		if (MenuPanel != null)
		{
			MenuPanel.Visible = false;
		}
		MenuPanel.MouseFilter = MouseFilterEnum.Pass;
		this.MouseFilter = MouseFilterEnum.Pass;
	}
}
