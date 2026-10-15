using UnityEngine;

public abstract class AppService : MonoBehaviour
{
    public bool IsInitialized { get; private set; }

    internal void InitializeService()
    {
        if (IsInitialized)
        {
            return;
        }

        OnInitialize();
        IsInitialized = true;
    }

    internal void ShutdownService()
    {
        if (!IsInitialized)
        {
            return;
        }

        OnShutdown();
        IsInitialized = false;
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnShutdown()
    {
    }
}
