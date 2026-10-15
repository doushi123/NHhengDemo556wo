using UnityEngine;

public class BasePanel : MonoBehaviour
{
    [SerializeField] private PanelId id;

    public PanelId Id => id;
    public bool IsOpen => gameObject.activeSelf;

    public virtual void Open(object data = null)
    {
        gameObject.SetActive(true);
    }

    public virtual void Close()
    {
        gameObject.SetActive(false);
    }
}
