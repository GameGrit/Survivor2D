using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

public class SaveManager : BaseSingleton<SaveManager>
{
    private const string SaveFileExt = ".dat";

    private string GetPath<T>()
    {
        return Path.Combine(Application.persistentDataPath, typeof(T).Name + SaveFileExt);
    }

    public void Save<T>(T data)
    {
        if (data == null) return;
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(GetPath<T>(), json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存失败 {typeof(T).Name}：{ex.Message}");
        }
    }

    public T Load<T>()
    {
        string path = GetPath<T>();
        if (!File.Exists(path)) return default;
        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载失败 {typeof(T).Name}：{ex.Message}");
            return default;
        }
    }

    public void Delete<T>()
    {
        string path = GetPath<T>();
        if (File.Exists(path)) File.Delete(path);
    }
}
