using UnityEngine;
using UnityEngine.EventSystems;

public class BattleFocusCloseClickBox : MonoBehaviour, IPointerClickHandler
{
    private BattleStandingFocusController owner;
    private Collider worldCollider;
    private Collider2D worldCollider2D;

    public void Bind(BattleStandingFocusController controller)
    {
        owner = controller;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ClearFocus();
    }

    private void OnMouseUpAsButton()
    {
        ClearFocus();
    }

    private void CacheColliders()
    {
        if (worldCollider == null)
            worldCollider = GetComponent<Collider>();

        if (worldCollider2D == null)
            worldCollider2D = GetComponent<Collider2D>();
    }

    private void ClearFocus()
    {
        if (owner != null)
            owner.ClearFocus();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawDebugBox();
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugBox();
    }

    private void DrawDebugBox()
    {
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.85f);

        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);
            return;
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
            return;
        }

        BoxCollider2D box2D = GetComponent<BoxCollider2D>();
        if (box2D != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box2D.offset, new Vector3(box2D.size.x, box2D.size.y, 0.05f));
            Gizmos.matrix = oldMatrix;
        }
    }
#endif
}
