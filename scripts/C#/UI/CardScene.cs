using Godot;
using System;
using System.Diagnostics;

public partial class CardScene : Control
{
	public static readonly PackedScene CardScenePackedPath = GD.Load<PackedScene>("res://scenes/cards/CardScene.tscn");
	public static Vector2 DEFAULT_SIZE = new Vector2(500, 700);
	public static Vector2 DEFAULT_TEXT_BLOCK_SIZE = new Vector2(500, 250);
	public float CalculatedScale => Size.Y / DEFAULT_SIZE.Y;

	public int CardId = -1;
	public CardState CardState => CardState.ForId(CardId);
	public FactionState FactionState => FactionState.ForEnum(CardState.Faction);

	private bool triggersEmphasis = true;

	private bool showActivatableOverlay = false;

	private bool mousePassthrough = false;

	private VBoxContainer textBackgroundContainerNode => GetNode<VBoxContainer>("%TextBackgroundBox");
	private VBoxContainer textContainerNode => GetNode<VBoxContainer>("%TextContainer");
	private VBoxContainer textInnerContainerNode => GetNode<VBoxContainer>("%TextInnerContainer");
	private RichTextLabel titleNode => GetNode<RichTextLabel>("%Title");
	private RichTextLabel textNode => GetNode<RichTextLabel>("%Text");
	private TextureRect cardTextureNode => GetNode<TextureRect>("%CardTexture");
	private TextureRect textBoxTexture => GetNode<TextureRect>("%TextBoxTexture");
	private Button cardButton => GetNode<Button>("%CardButton");
	private ColorRect activatableColorOverlay => GetNode<ColorRect>("ActivatableColorOverlay");

	[Signal] public delegate void SelectedEventHandler(int cardId);

	public override void _Ready()
	{     
		cardButton.Pressed += CardButton_Pressed;
		cardButton.MouseEntered += CardButton_MouseEntered;
		cardButton.MouseExited += CardButton_MouseExited;

		SetActivatable(!showActivatableOverlay);
	}

	private void CardButton_Pressed()
	{
		EmitSignal(SignalName.Selected, this.CardId);
	}
	// Both handlers are null-guarded on Current: a CardScene can now be rendered outside the hand
	// (a Bulletin in a modal or the trigger context), where FactionHandDisplay.Current may not exist.
	// MouseExited in particular ignored triggersEmphasis, so TriggersEmphasis(false) was not enough.
	private void CardButton_MouseEntered()
	{
		if (triggersEmphasis)
		{
			FactionHandDisplay.Current?.ShowCardEmphasis(CardId);
		}

	}
	private void CardButton_MouseExited()
	{
		DebugUtilities.PrintPeerFinest($"Mouse exited card with ID: {CardId}");
		FactionHandDisplay.Current?.HideCardEmphasis();
	}

	public void ShowCard(int cardId)
	{
		if (cardId > -1)
		{
			// ShowFace clears CardId, so assign after it — CardId is what the Selected signal carries.
			ShowFace(CardFace.ForCard(CardState.ForId(cardId)));
			CardId = cardId;
			return;
		}

		Visible = true;
		MouseFilter = mousePassthrough ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
		CardId = cardId;
		titleNode.Text = "Card Not Found";
		textNode.Text = $"Card Id = {CardId}";
		SetClickable(false);
		RecalculateSizes();
	}

	/// <summary>
	/// Render an arbitrary card front. The render primitive ShowCard is built on, and the way anything
	/// without a CardState — a Bulletin for an automatic step mutator — gets drawn as a card.
	/// CardId is cleared so a leftover id from a previous ShowCard cannot be emitted by Selected.
	/// </summary>
	public void ShowFace(CardFace face)
	{
		Visible = true;
		MouseFilter = mousePassthrough ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
		CardId = -1;

		cardTextureNode.Texture = face.Front;
		if (!string.IsNullOrEmpty(face.Text))
		{
			textContainerNode.Visible = true;
			textBackgroundContainerNode.Visible = true;
			titleNode.Text = face.Title;
			textNode.Text = face.Text;
		}
		else
		{
			textContainerNode.Visible = false;
			textBackgroundContainerNode.Visible = false;
		}
		RecalculateSizes();
	}

	public void RecalculateSizes()
	{
		textInnerContainerNode.CustomMinimumSize = new Vector2(Size.X, DEFAULT_TEXT_BLOCK_SIZE.Y * CalculatedScale);
		textBoxTexture.CustomMinimumSize = new Vector2(Size.X, DEFAULT_TEXT_BLOCK_SIZE.Y * CalculatedScale);
		titleNode.AddThemeFontSizeOverride("normal_font_size", (int)(30f * CalculatedScale));
		textNode.AddThemeFontSizeOverride("normal_font_size", (int)(20f * CalculatedScale));
	}

	public void SetClickable(bool clickable)
	{
		cardButton.Disabled = !clickable;
	}
	public void SetActivatable(bool activatable)
	{
		showActivatableOverlay = !activatable;
		if(activatableColorOverlay != null)
		{
			activatableColorOverlay.Visible = !activatable;
		}
		
	}    
	public void TriggersEmphasis(bool triggersEmphasis)
	{
		this.triggersEmphasis = triggersEmphasis;
	}

	/// <summary>
	/// Make the whole card mouse-transparent, for a card that is pure decoration — the game history
	/// hover popup floats over the 3D board and must not eat clicks meant for it.
	///
	/// Sticky rather than one-shot because ShowCard/ShowFace re-assign MouseFilter on every render,
	/// so a single assignment from outside would be undone by the next ShowCard. Covers %CardButton
	/// too: a *disabled* Button still consumes mouse events, because MOUSE_FILTER_STOP stops
	/// propagation whether or not the control acts on the event.
	/// </summary>
	public void SetMousePassthrough(bool passthrough)
	{
		mousePassthrough = passthrough;
		MouseFilterEnum filter = passthrough ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
		MouseFilter = filter;
		if (cardButton != null)
		{
			cardButton.MouseFilter = filter;
		}
	}
}
