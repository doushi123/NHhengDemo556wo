using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class AppRoot : MonoBehaviour
{
    [Tooltip("按列表顺序初始化，关闭时顺序相反。")]
    [SerializeField] private List<AppService> services = new List<AppService>();

    public static AppRoot Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (AppService service in services)
        {
            service.InitializeService();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        for (int i = services.Count - 1; i >= 0; i--)
        {
            services[i].ShutdownService();
        }

        Instance = null;
    }
}
