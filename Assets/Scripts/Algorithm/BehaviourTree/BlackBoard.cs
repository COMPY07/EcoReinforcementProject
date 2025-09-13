using System.Collections.Generic;

public class BlackBoard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();
    
    public void Set<T>(string key, T value)
    {
        data[key] = value;
    }
    
    public T Get<T>(string key)
    {
        return data.ContainsKey(key) ? (T)data[key] : default(T);
    }
    
    public bool HasKey(string key)
    {
        return data.ContainsKey(key);
    }
}