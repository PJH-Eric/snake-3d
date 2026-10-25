using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.EventSystems
{
    public class EventSystem : MonoBehaviour
    {
        public static EventSystem current; public GameObject currentSelectedGameObject; public bool sendNavigationEvents;
        public void SetSelectedGameObject(GameObject g) { }
    }
    public class BaseInputModule : MonoBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerEventData { public PointerEventData(EventSystem eventSystem) { } public int pointerId; public Vector2 position; public Vector2 delta; }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData e); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData e); }
    public interface IDragHandler { void OnDrag(PointerEventData e); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData e); }
}

namespace UnityEngine.UI
{
    public abstract class Graphic : MonoBehaviour
    {
        public Color color; public bool raycastTarget; public RectTransform rectTransform; public Material material;
        public void SetAllDirty() { } public void SetLayoutDirty() { }
    }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite; public Type type; public float fillAmount; public FillMethod fillMethod; public int fillOrigin; public bool fillClockwise; public bool preserveAspect;
    }
    public class RawImage : MaskableGraphic { public Texture texture; public Rect uvRect; }
    public class Text : MaskableGraphic
    {
        public string text; public Font font; public int fontSize; public FontStyle fontStyle; public TextAnchor alignment; public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow;
        public bool supportRichText; public float lineSpacing; public float preferredWidth, preferredHeight; public bool resizeTextForBestFit;
    }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public class Selectable : MonoBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public Graphic targetGraphic; public ColorBlock colors; public bool interactable; public Transition transition;
    }
    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor; public float fadeDuration, colorMultiplier;
    }
    public class Button : Selectable { public Button.ButtonClickedEvent onClick; public class ButtonClickedEvent : UnityEvent { } }
    public class Toggle : Selectable { }
    public class Slider : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public RectTransform fillRect, handleRect; public Direction direction; public float minValue, maxValue, value; public SliderEvent onValueChanged;
        public class SliderEvent : UnityEvent<float> { }
    }
    public class Scrollbar : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public RectTransform handleRect; public Direction direction; public float value, size;
    }
    public class ScrollRect : MonoBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        public bool horizontal, vertical, inertia; public MovementType movementType; public float elasticity, scrollSensitivity, decelerationRate;
        public RectTransform viewport, content; public Scrollbar verticalScrollbar; public ScrollbarVisibility verticalScrollbarVisibility; public float verticalNormalizedPosition;
    }
    public class InputField : Selectable
    {
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public Text textComponent; public Graphic placeholder; public LineType lineType; public int characterLimit; public int caretWidth; public bool customCaretColor;
        public Color caretColor, selectionColor; public string text; public bool isFocused; public InputField.OnChangeEvent onValueChanged; public InputField.EndEditEvent onEndEdit;
        public class OnChangeEvent : UnityEvent<string> { } public class EndEditEvent : UnityEvent<string> { }
    }
    public class RectMask2D : MonoBehaviour { }
    public class Canvas : Behaviour
    {
        public RenderMode renderMode; public int sortingOrder; public float scaleFactor;
        public static void ForceUpdateCanvases() { }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class CanvasScaler : MonoBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public ScaleMode uiScaleMode; public float scaleFactor; }
    public class GraphicRaycaster : MonoBehaviour { }
    public class Shadow : MonoBehaviour { public Color effectColor; public Vector2 effectDistance; }
    public class Outline : Shadow { }
    public class RectOffset { public int left, right, top, bottom; public int horizontal, vertical; public RectOffset(int l, int r, int t, int b) { left = l; right = r; top = t; bottom = b; } }
    public interface ILayoutElement { void CalculateLayoutInputHorizontal(); void CalculateLayoutInputVertical(); float minWidth { get; } float preferredWidth { get; } float flexibleWidth { get; } float minHeight { get; } float preferredHeight { get; } float flexibleHeight { get; } int layoutPriority { get; } }
    public class LayoutElement : MonoBehaviour { public float minWidth, minHeight, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight; }
    public static class LayoutUtility
    {
        public static float GetPreferredSize(RectTransform r, int axis) { return 0; } public static float GetMinSize(RectTransform r, int axis) { return 0; }
        public static float GetPreferredHeight(RectTransform r) { return 0; }
    }
    public abstract class LayoutGroup : MonoBehaviour, ILayoutElement
    {
        public RectOffset padding; public TextAnchor childAlignment; protected RectTransform rectTransform; protected List<RectTransform> rectChildren;
        public virtual void CalculateLayoutInputHorizontal() { } public virtual void CalculateLayoutInputVertical() { }
        public virtual void SetLayoutHorizontal() { } public virtual void SetLayoutVertical() { }
        protected void SetLayoutInputForAxis(float min, float pref, float flex, int axis) { }
        protected void SetChildAlongAxis(RectTransform r, int axis, float pos, float size) { }
        public float minWidth { get { return 0; } } public float preferredWidth { get { return 0; } } public float flexibleWidth { get { return 0; } }
        public float minHeight { get { return 0; } } public float preferredHeight { get { return 0; } } public float flexibleHeight { get { return 0; } } public int layoutPriority { get { return 0; } }
    }
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing; public bool childControlWidth, childControlHeight, childForceExpandWidth, childForceExpandHeight;
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class ContentSizeFitter : MonoBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit, verticalFit;
    }
}
