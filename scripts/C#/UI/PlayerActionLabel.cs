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
/// rect; text that still does not fit at the minimum size is cut with an ellipsis rather than
/// widening it.
/// </summary>
public partial class PlayerActionLabel : Label, LoadableUI
{    
	public static PlayerActionLabel Instance;
	
	private MenuPanel MenuPanel => GetNode<MenuPanel>("%MenuPanel");

	/// <summary>
	/// The plate behind the banner text. Tinted with the faction on turn, so the banner
	/// says who the game is on by colour as well as by wording — see <see cref="TurnFactionTint"/>.
	/// </summary>
	private Control BackgroundPanel => GetNode<Control>("%MenuPanel/BackgroundContainer/Panel");

	private TurnFactionTint _turnTint;

	/// <summary>
	/// The size the banner was authored at, read off the scene before anything overrides it. Every fit
	/// starts here and only goes down, so a short line still draws at the intended size.
	/// </summary>
	private int _baseFontSize;

	/// <summary>
	/// How small the text may get. Low on purpose: the panel never grows, so fitting the line matters
	/// more than its size, and the longest guidance lines need it.
	/// </summary>
	private const int MinimumFontSize = 8;

	public override void _Ready()
	{
		Instance = this;

		// LabelSettings, when set, overrides the theme: its size is the one that actually draws.
		_baseFontSize = LabelSettings?.FontSize ?? GetThemeFontSize("font_size");
		// The panel lays this label out in its own sort pass, which can land after the text arrives —
		// and does on the first banner of a game, when the label is still (0,0) at that moment. Fitting
		// again on every resize is what makes that case, and a later window resize, come out right.
		Resized += FitTextToBox;

		_turnTint = new TurnFactionTint(BackgroundPanel);
		_turnTint.Attach();

		// Hide by default until LoadUI is called
		Visible = false;
	}

	/// <summary>
	/// EventBus is a process-wide static, so the tint handler would outlive the game scene if it were
	/// left connected — and one stale handler aborts the whole emission for every handler behind it.
	/// </summary>
	public override void _ExitTree()
	{
		_turnTint?.Detach();
		_turnTint = null;

		// Same release as ModalStack.Current. Without it the static outlives the HUD it points at, and
		// the null guards on ShowText/HideText would wave a freed node through into ObjectDisposedException.
		if (Instance == this) Instance = null;
	}

	public void LoadUI()
	{
		HideNode();        
	}

	public static void ShowText(string text, Faction faction = (Faction)(-1))
	{
		ShowText(text, -1, faction);

	}
	/// <summary>
	/// Null-conditional like <c>InputTimerDisplay.Current?</c>: the banner is cosmetic, and every caller
	/// is on the logic path, so a peer whose HUD is not up yet must lose the caption rather than the
	/// prompt behind it.
	/// </summary>
	public static void ShowText(string text, int duration, Faction faction = (Faction)(-1)){
		// A restore replays the whole game; only the prompt it resumes on, shown after, belongs in chat.
		if (!ReplayContext.IsFastForwarding)
			ChatService.Instance?.PostGameMessage(text, ChatColorFor(faction));
		Instance?.ShowTextForDuration(text, duration, faction);
	}

	/// <summary>
	/// The faction's colour, lightened: several are too dark to read on the chat panel. Null for no
	/// faction (e.g. "Waiting on …"), which leaves the chat's own grey.
	/// </summary>
	private static Color? ChatColorFor(Faction faction) =>
		StaticGameData.FactionDataMap.TryGetValue(faction, out FactionData data)
			? data.FactionColor.Lightened(0.35f)
			: null;

	public void ShowTextForDuration(string text, int duration = -1, Faction faction = (Faction)(-1))
	{
		MenuPanel.MouseFilter = MouseFilterEnum.Stop;
		this.MouseFilter = MouseFilterEnum.Stop;
		// One line, always: a line break would still split it even with wrapping off.
		Text = text.Replace("\r", "").Replace("\n", " ");
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
	/// Shrink the font until the text fits the box on ONE line (the label never wraps), so a long
	/// notification is never clipped, scrolled out of sight, or drawn over the panel's edge.
	///
	/// Measured off the font rather than <c>GetContentHeight</c>: the label only shapes its lines when
	/// it next draws, so its own content height is still the PREVIOUS text's at the point this runs —
	/// which is the moment the text is set, sometimes off the main thread from an animation.
	/// </summary>
	private void FitTextToBox()
	{
		// A Label's theme names are font/font_size/line_spacing, and LabelSettings overrides all three.
		Font font = LabelSettings?.Font ?? GetThemeFont("font");
		if (font == null) return;

		// The outline draws outside the glyphs on every side, so it comes off the box too.
		float outline = LabelSettings?.OutlineSize ?? GetThemeConstant("outline_size");
		Vector2 padding = (GetThemeStylebox("normal")?.GetMinimumSize() ?? Vector2.Zero) + new Vector2(outline, outline);
		float width = Size.X - padding.X;
		float height = Size.Y - padding.Y;
		// Before the panel's first sort pass there is no box to measure against. The Resized hook above
		// redoes the fit as soon as there is one.
		if (width < 1f || height < 1f) return;

		for (int fontSize = _baseFontSize; fontSize > MinimumFontSize; fontSize--)
		{
			if (Fits(font, Text, fontSize, width, height))
			{
				ApplyFontSize(fontSize);
				return;
			}
		}

		ApplyFontSize(MinimumFontSize);
	}

	/// <summary>Whether the text fits the box on one line: the banner never wraps, it shrinks.</summary>
	private static bool Fits(Font font, string text, int fontSize, float width, float height)
	{
		Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
		return textSize.X <= width && textSize.Y <= height;
	}

	/// <summary>Into LabelSettings when there is one (local to the scene, so this banner's own copy).</summary>
	private void ApplyFontSize(int fontSize)
	{
		if (LabelSettings != null) LabelSettings.FontSize = fontSize;
		else AddThemeFontSizeOverride("font_size", fontSize);
	}

	/// <summary>Guarded like <see cref="ShowText(string, int, Faction)"/> — every client runs this off the
	/// input-response path, whether or not its HUD is up.</summary>
	public static void HideText()
	{
		Instance?.HideNode();
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
