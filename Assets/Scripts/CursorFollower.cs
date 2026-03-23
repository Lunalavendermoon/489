// CursorFollower.cs
using UnityEngine;

public class CursorFollower : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    [Tooltip("Optional reference to the CoreController so we can detect hovering over the core collider.")]
    public CoreController core;
    [Tooltip("Optional reference to PlayerCursorController to detect if carrying notes.")]
    public PlayerCursorController cursor;

    [Header("Cursor Sprites")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Sprite when idle (not hovering, not carrying notes).")]
    public Sprite idleCursorSprite;
    [Tooltip("Sprite when carrying notes.")]
    public Sprite normalCursorSprite;
    [Tooltip("Sprite when hovering over the core collider.")]
    public Sprite hoverCursorSprite;

    public float zDepth = 0f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // if spriteRenderer exists and we have an idle sprite, ensure it's set
        if (spriteRenderer != null && idleCursorSprite != null)
            spriteRenderer.sprite = idleCursorSprite;
    }

    private void Update()
    {
        if (cam == null) return;

        Vector3 m = Input.mousePosition;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, cam.nearClipPlane));
        w.z = zDepth;
        transform.position = w;

        // Update sprite based on state: hovering > carrying > idle
        if (spriteRenderer != null)
        {
            Vector2 world2 = w;
            bool overCore = (core != null) && Vector2.Distance(world2, core.CorePosition) <= core.coreRadius;
            bool isCarrying = (cursor != null) && (cursor.CarriedDots != null) && (cursor.CarriedDots.Count > 0);

            if (overCore && hoverCursorSprite != null)
            {
                spriteRenderer.sprite = hoverCursorSprite;
            }
            else if (isCarrying && normalCursorSprite != null)
            {
                spriteRenderer.sprite = normalCursorSprite;
            }
            else if (idleCursorSprite != null)
            {
                spriteRenderer.sprite = idleCursorSprite;
            }
        }
    }
}
