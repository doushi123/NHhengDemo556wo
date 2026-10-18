using System.Collections.Generic;
using UnityEngine;

public sealed class UIManager : AppService
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private Transform uiRoot;
    [SerializeField] private List<BasePanel> panelPrefabs = new List<BasePanel>();

    private readonly Dictionary<PanelId, BasePanel> prefabs = new Dictionary<PanelId, BasePanel>();
    private readonly Dictionary<PanelId, BasePanel> instances = new Dictionary<PanelId, BasePanel>();

    protected override void OnInitialize()
    {
        Instance = this;

        foreach (BasePanel prefab in panelPrefabs)
        {
            prefabs.Add(prefab.Id, prefab);
        }
    }

    public BasePanel Open(PanelId id, object data = null)
    {
        BasePanel panel = GetOrCreate(id);
        panel.transform.SetAsLastSibling();
        panel.Open(data);
        return panel;
    }

    public void Close(PanelId id)
    {
        if (!instances.TryGetValue(id, out BasePanel panel))
        {
            return;
        }

        panel.Close();
    }

    public bool IsOpen(PanelId id)
    {
        return instances.TryGetValue(id, out BasePanel panel) && panel.IsOpen;
    }

    protected override void OnShutdown()
    {
        prefabs.Clear();
        instances.Clear();
        Instance = null;
    }

    private BasePanel GetOrCreate(PanelId id)
    {
        if (instances.TryGetValue(id, out BasePanel panel))
        {
            return panel;
        }

        BasePanel prefab = prefabs[id];
        panel = Instantiate(prefab, uiRoot);
        panel.gameObject.SetActive(false);
        instances.Add(id, panel);
        return panel;
    }
}
