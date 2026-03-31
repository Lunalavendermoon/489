using System.Collections.Generic;
using UnityEngine;

public class PlayerCursorController : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public GameManager gm;
    public EffectsManager fx;

    [Header("Dot Detection")]
    public LayerMask dotMask;

    [Header("Magnet Carry (Hold LMB)")]
    public bool magnetPickupEnabled = true;
    public float magnetPickupRadius = 0.45f;
    public int maxMagnetDots = 8;

    [Tooltip("If true, dots trail behind each other (juicy). If false, all follow cursor directly.")]
    public bool chainFollow = true;

    [Header("Carry Feel")]
    public float followLerp = 18f;

    private readonly List<Dot> carriedDots = new List<Dot>();
    private bool holdingMouse;

    public IReadOnlyList<Dot> CarriedDots => carriedDots;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (gm == null) return;

        Vector2 cursorPos = GetCursorWorld();
        // Always update cursor position (used by core in both normal and super modes)
        transform.position = new Vector3(cursorPos.x, cursorPos.y, transform.position.z);

        // Handle supermode collision
        if (gm.state == GameManager.GameState.Super)
        {
            gm?.TrySuperClickCore(cursorPos, gm.core?.coreRadius ?? 0.8f, gm.core?.CorePosition ?? Vector2.zero);
            return;
        }

        // Normal state input (original logic)
        if (gm.state != GameManager.GameState.Normal) return;

        if (Input.GetMouseButtonDown(0))
            holdingMouse = true;

        if (Input.GetMouseButtonUp(0))
        {
            holdingMouse = false;

            // Release dots
            for (int i = 0; i < carriedDots.Count; i++)
                if (carriedDots[i] != null) carriedDots[i].SetCarried(false);

            carriedDots.Clear();
            return;
        }

        CleanupCarriedDots();

        if (!holdingMouse) return;

        // Magnet pickup while holding
        if (magnetPickupEnabled)
            MagnetPickup(cursorPos);

        // Follow behavior for carried dots
        if (carriedDots.Count > 0)
        {
            if (!chainFollow)
            {
                for (int i = 0; i < carriedDots.Count; i++)
                    if (carriedDots[i] != null) carriedDots[i].FollowCursor(cursorPos, followLerp);
            }
            else
            {
                Vector2 target = cursorPos;
                for (int i = 0; i < carriedDots.Count; i++)
                {
                    var d = carriedDots[i];
                    if (d == null) continue;

                    d.FollowCursor(target, followLerp);
                    target = d.transform.position;
                }
            }
        }
    }

    private Vector2 GetCursorWorld()
    {
        if (cam == null) return Vector2.zero;
        Vector3 m = Input.mousePosition;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, cam.nearClipPlane));
        return new Vector2(w.x, w.y);
    }

    private void MagnetPickup(Vector2 cursor)
    {
        if (carriedDots.Count >= maxMagnetDots) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(cursor, magnetPickupRadius, dotMask);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            if (carriedDots.Count >= maxMagnetDots) break;
            if (hits[i] == null) continue;

            Dot d = hits[i].GetComponentInParent<Dot>();
            if (d == null) continue;
            if (!d.CanPickup) continue;
            if (carriedDots.Contains(d)) continue;

            d.SetCarried(true);
            carriedDots.Add(d);
            fx?.PickupPop();
        }
    }

    private void CleanupCarriedDots()
    {
        for (int i = carriedDots.Count - 1; i >= 0; i--)
        {
            if (carriedDots[i] == null || !carriedDots[i].gameObject.activeInHierarchy)
                carriedDots.RemoveAt(i);
        }
    }

    public void RemoveCarriedDot(Dot d)
    {
        if (d == null) return;
        carriedDots.Remove(d);
    }

    public bool IsCarryingCapturedCurve(out CurveString curve)
    {
        // Curves removed; always false.
        curve = null;
        return false;
    }

    public void ClearCarryAll()
    {
        for (int i = 0; i < carriedDots.Count; i++)
            if (carriedDots[i] != null) carriedDots[i].SetCarried(false);

        carriedDots.Clear();
        holdingMouse = false;
    }
}
