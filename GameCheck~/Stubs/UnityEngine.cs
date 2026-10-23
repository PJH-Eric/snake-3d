// UnityEngine 的「簽名替身」：只為了在沒有 Unity 的機器上做編譯檢查（型別、拼字、參數數量）。
// 簽名依 Unity 6 公開 API 手寫，行為一律是空的——不可執行。
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T FindAnyObjectByType<T>() where T : Object { return null; }
        public static T Instantiate<T>(T o) where T : Object { return o; }
        public static implicit operator bool(Object o) { return o != null; }
    }
    public class Component : Object
    {
        public Transform transform; public GameObject gameObject;
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
        public T[] GetComponentsInChildren<T>() { return null; }
        public T GetComponentInParent<T>() { return default(T); }
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator r) { return null; }
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
    }
    public class Coroutine { }
    public class GameObject : Object
    {
        public Transform transform; public int layer; public bool activeSelf;
        public GameObject() { } public GameObject(string name) { } public GameObject(string name, params Type[] comps) { }
        public T AddComponent<T>() where T : Component { return default(T); }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
        public void SetActive(bool b) { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    [AttributeUsage(AttributeTargets.All)] public sealed class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 a, Vector2 b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); }
        public override bool Equals(object o) { return base.Equals(o); }
        public override int GetHashCode() { return 0; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public Vector3 normalized { get { return this; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; }
        public static float Distance(Vector3 a, Vector3 b) { return 0; }
        public static float Dot(Vector3 a, Vector3 b) { return 0; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return a; }
    }
    public struct Vector4 { public float x, y, z, w; public float sqrMagnitude { get { return x * x + y * y + z * z + w * w; } } public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } }
    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion(); } }
        public static Quaternion Euler(float x, float y, float z) { return new Quaternion(); }
        public static Quaternion AngleAxis(float a, Vector3 axis) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 f, Vector3 up) { return new Quaternion(); }
        public static Quaternion LookRotation(Vector3 f) { return new Quaternion(); }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
        public static Vector3 operator *(Quaternion a, Vector3 b) { return b; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
    }
    public struct Matrix4x4
    {
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) { return new Matrix4x4(); }
        public static Matrix4x4 identity { get { return new Matrix4x4(); } }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin { get { return x; } } public float xMax { get { return x + width; } }
        public float yMin { get { return y; } } public float yMax { get { return y + height; } }
        public Vector2 center { get { return new Vector2(x + width / 2, y + height / 2); } }
        public Vector2 size { get { return new Vector2(width, height); } }
        public static bool operator ==(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
        public static bool operator !=(Rect a, Rect b) { return !(a == b); }
        public override bool Equals(object o) { return base.Equals(o); }
        public override int GetHashCode() { return 0; }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color Lerp(Color a, Color b, float t) { return a; }
        public static Color operator *(Color a, float d) { return new Color(a.r * d, a.g * d, a.b * d, a.a * d); }
        public static Color operator *(Color a, Color b) { return a; }
        public static Color operator +(Color a, Color b) { return a; }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public static implicit operator Color32(Color c) { return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(c.a * 255)); }
        public static implicit operator Vector4(Color c) { return new Vector4(c.r, c.g, c.b, c.a); }
        public static bool operator ==(Color a, Color b) { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }
        public static bool operator !=(Color a, Color b) { return !(a == b); }
        public override bool Equals(object o) { return base.Equals(o); }
        public override int GetHashCode() { return 0; }
    }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }

    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f, Rad2Deg = 57.29578f, PI = 3.14159274f;
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Clamp(float v, float a, float b) { return Math.Min(b, Math.Max(a, v)); }
        public static int Clamp(int v, int a, int b) { return Math.Min(b, Math.Max(a, v)); }
        public static float Clamp01(float v) { return Clamp(v, 0, 1); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Atan2(float a, float b) { return (float)Math.Atan2(a, b); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Exp(float f) { return (float)Math.Exp(f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Sign(float f) { return f >= 0 ? 1 : -1; }
        public static float Repeat(float t, float l) { return t - (float)Math.Floor(t / l) * l; }
        public static float DeltaAngle(float a, float b) { return b - a; }
        public static float SmoothStep(float a, float b, float t) { return a; }
        public static bool Approximately(float a, float b) { return Math.Abs(a - b) < 1e-6f; }
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position, localPosition, localScale; public Quaternion rotation, localRotation; public Transform parent;
        public int childCount; public Vector3 forward;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
        public Transform GetChild(int i) { return null; }
        public void SetAsFirstSibling() { } public void SetAsLastSibling() { }
        public void LookAt(Vector3 t, Vector3 up) { } public void LookAt(Vector3 t) { }
        public IEnumerator GetEnumerator() { return null; }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot, offsetMin, offsetMax; public Rect rect;
        public void GetWorldCorners(Vector3[] c) { }
        public void SetSizeWithCurrentAnchors(RectTransform.Axis a, float s) { }
        public enum Axis { Horizontal, Vertical }
    }
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform r, Vector2 p, Camera c, out Vector2 l) { l = p; return true; }
        public static bool RectangleContainsScreenPoint(RectTransform r, Vector2 p, Camera c) { return true; }
        public static bool RectangleContainsScreenPoint(RectTransform r, Vector2 p) { return true; }
    }
    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public class Camera : Behaviour
    {
        public CameraClearFlags clearFlags; public Color backgroundColor; public float fieldOfView, nearClipPlane, farClipPlane, depth, aspect;
        public bool allowHDR, allowMSAA; public int cullingMask; public Rect rect; public static Camera main;
    }
    public class AudioListener : Behaviour { }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public class Light : Behaviour { public LightType type; public Color color; public float intensity, shadowStrength, shadowBias, shadowNormalBias; public LightShadows shadows; }
    public class Renderer : Component { public Material material, sharedMaterial; public bool enabled; public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; }
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh mesh, sharedMesh; }
    public class TextMesh : Component
    {
        public string text; public float characterSize, lineSpacing, offsetZ; public int fontSize; public Color color; public Font font; public FontStyle fontStyle;
        public TextAnchor anchor; public TextAlignment alignment;
    }
    public enum TextAlignment { Left, Center, Right }
    public class Material : Object
    {
        public Material(Shader s) { } public Color color; public Shader shader; public Texture mainTexture; public bool enableInstancing; public int renderQueue;
        public void SetColor(string n, Color c) { } public void SetFloat(string n, float f) { } public void SetTexture(string n, Texture t) { }
        public void EnableKeyword(string k) { } public void DisableKeyword(string k) { }
        public bool HasProperty(string n) { return true; }
    }
    public class Shader : Object { public static Shader Find(string n) { return null; } }
    public class Texture : Object { public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum TextureFormat { RGBA32 = 4, RGB24 = 3, ARGB32 = 5, R8 = 63 }
    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { } public Texture2D(int w, int h, TextureFormat f, bool mip) { }
        public void SetPixels32(Color32[] c) { } public void SetPixels(Color[] c) { } public void SetPixel(int x, int y, Color c) { } public void Apply() { } public void Apply(bool m) { } public void Apply(bool m, bool n) { }
        public Color32[] GetPixels32() { return null; } public Color[] GetPixels() { return null; }
        public int anisoLevel;
    }
    public class Sprite : Object
    {
        public Texture2D texture; public Vector4 border;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot) { return null; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) { return null; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint ext, SpriteMeshType mt, Vector4 border) { return null; }
    }
    public enum SpriteMeshType { FullRect, Tight }
    public class Mesh : Object
    {
        public Vector3[] vertices; public int[] triangles; public Vector2[] uv; public Vector3[] normals; public Color[] colors; public Color32[] colors32;
        public void Clear() { } public void RecalculateNormals() { } public void RecalculateBounds() { } public void SetTriangles(int[] t, int sub) { } public void SetTriangles(List<int> t, int sub) { } public void SetUVs(int ch, List<Vector2> uvs) { } public void SetNormals(List<Vector3> n) { } public void SetColors(List<Color> c) { } public void SetVertices(List<Vector3> v) { }
        public int vertexCount; public Rendering.IndexFormat indexFormat;
        public void CombineMeshes(CombineInstance[] c, bool merge, bool useMatrices) { } public void CombineMeshes(CombineInstance[] c) { }
        public void UploadMeshData(bool markNoLongerReadable) { }
    }
    public struct CombineInstance { public Mesh mesh; public Matrix4x4 transform; public int subMeshIndex; }
    public class Font : Object { public static Font CreateDynamicFontFromOSFont(string n, int s) { return null; } public Material material; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public class TextAsset : Object { public string text; }
    public static class Resources
    {
        public static T Load<T>(string p) where T : Object { return null; }
        public static T GetBuiltinResource<T>(string p) where T : Object { return null; }
    }
    public static class Graphics
    {
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] mats, int count) { }
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, Matrix4x4[] mats, int count, MaterialPropertyBlock b, Rendering.ShadowCastingMode s, bool r, int layer, Camera cam) { }
        public static void DrawMeshInstanced(Mesh m, int sub, Material mat, List<Matrix4x4> mats) { }
        public static void DrawMesh(Mesh m, Matrix4x4 mat, Material material, int layer) { }
    }
    public class MaterialPropertyBlock { }
    public static class Time
    {
        public static float deltaTime, unscaledDeltaTime, unscaledTime, time, realtimeSinceStartup, timeScale;
        public static double realtimeSinceStartupAsDouble;
    }
    public static class Screen { public static int width, height; public static Rect safeArea; }
    public static class Application
    {
        public static int targetFrameRate; public static bool isMobilePlatform, isEditor; public static SystemLanguage systemLanguage;
        public static void Quit() { }
    }
    public enum SystemLanguage { English, ChineseTraditional, ChineseSimplified, Chinese, Unknown }
    public static class QualitySettings { public static int vSyncCount; public static float shadowDistance; }
    public static class ColorUtility { public static bool TryParseHtmlString(string s, out Color c) { c = default(Color); return true; } public static string ToHtmlStringRGBA(Color c) { return ""; } public static string ToHtmlStringRGB(Color c) { return ""; } }
    public enum KeyCode { None, Space, Return, KeypadEnter, Escape, A, D, S, W, LeftArrow, RightArrow, UpArrow, DownArrow, LeftShift, RightShift }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public TouchPhase phase; public int fingerId; public Vector2 position; }
    public static class Input
    {
        public static bool GetKey(KeyCode k) { return false; } public static bool GetKeyDown(KeyCode k) { return false; } public static bool GetKeyUp(KeyCode k) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; } public static bool GetMouseButtonDown(int b) { return false; }
        public static int touchCount; public static Touch GetTouch(int i) { return default(Touch); } public static bool touchSupported; public static Vector3 mousePosition;
    }
    public static class PlayerPrefs
    {
        public static string GetString(string k, string d) { return d; } public static void SetString(string k, string v) { } public static void Save() { }
        public static int GetInt(string k, int d) { return d; } public static void SetInt(string k, int v) { }
    }
    public static class JsonUtility { public static T FromJson<T>(string j) { return default(T); } public static string ToJson(object o) { return ""; } }
    public static class GUIUtility { public static string systemCopyBuffer; }
    public static class Handheld { public static void Vibrate() { } }
    public static class RenderSettings { public static Color ambientSkyColor, ambientEquatorColor, ambientGroundColor, ambientLight; public static Rendering.AmbientMode ambientMode; public static bool fog; public static Color fogColor; }
    public class WaitForSecondsRealtime : CustomYieldInstruction { public WaitForSecondsRealtime(float s) { } }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class YieldInstruction { }
    public abstract class CustomYieldInstruction : IEnumerator { public object Current { get { return null; } } public bool MoveNext() { return false; } public void Reset() { } }
    public class AudioClip : Object
    {
        public static AudioClip Create(string n, int len, int ch, int freq, bool stream) { return null; }
        public bool SetData(float[] d, int off) { return true; }
    }
    public class AudioSource : Behaviour
    {
        public AudioClip clip; public bool loop, playOnAwake, isPlaying; public float volume, spatialBlend;
        public void Play() { } public void Stop() { } public void PlayOneShot(AudioClip c) { } public void PlayOneShot(AudioClip c, float v) { }
    }
    public static class Random
    {
        public static float value; public static float Range(float a, float b) { return a; } public static int Range(int a, int b) { return a; }
    }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    [AttributeUsage(AttributeTargets.All)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
    public enum IndexFormat { UInt16, UInt32 }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 a);
    public class UnityEvent { public void AddListener(UnityAction a) { } public void RemoveListener(UnityAction a) { } public void RemoveAllListeners() { } public void Invoke() { } }
    public class UnityEvent<T0> { public void AddListener(UnityAction<T0> a) { } public void RemoveListener(UnityAction<T0> a) { } public void RemoveAllListeners() { } public void Invoke(T0 a) { } }
}

namespace UnityEngine.Networking
{
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public Result result; public int timeout;
        public static UnityWebRequest Get(string u) { return null; }
        public UnityWebRequestAsyncOperation SendWebRequest() { return null; }
        public static string UnEscapeURL(string s) { return s; }
        public static string EscapeURL(string s) { return s; }
        public void Dispose() { }
    }
    public class UnityWebRequestAsyncOperation : AsyncOperation { }
    public class AsyncOperation : YieldInstruction { }
}
