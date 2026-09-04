using UnityEngine;

public class BaseMonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    _instance= obj.AddComponent<T>();
                    obj.name= typeof(T).ToString()+ " (Singleton)";
                }
            }
            return _instance;
        }
    }
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
/// <summary>
/// 纯C#单例，不依赖GameObject
/// </summary>
public class BaseSingleton<T> where T : class, new()
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            _instance ??= new T();
            return _instance;
        }
    }
}
