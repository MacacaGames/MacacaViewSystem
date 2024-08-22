using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class ReferenceLinker : MonoBehaviour
{
    Dictionary<string, Component> componentCache;
    Dictionary<string, GameObject> gameObjectCache;

    [SerializeField]
    bool useCache = false;

    [SerializeField]
    PairStruct[] data = new PairStruct[] { };

    void InitCache()
    {
        if (componentCache == null)
            componentCache = new Dictionary<string, Component>();

        if (gameObjectCache == null)
            gameObjectCache = new Dictionary<string, GameObject>();
    }


    public GameObject this[string key]
    {
        get { return Get(key); }
    }

    public bool Has(string key)
    {
        return ArrayDictionary.GetKeyLoc(ref data, key) >= 0;
    }

    public GameObject Get(string key)
    {
        InitCache();
        if (gameObjectCache.TryGetValue(key, out GameObject result) == false)
        {
            result = GetWithoutCache();
            gameObjectCache.Add(key, result);
        }

        return result;

        GameObject GetWithoutCache()
        {
            int KeyLoc = ArrayDictionary.GetKeyLoc(ref data, key);
            if (KeyLoc >= 0)
            {
                result = data[KeyLoc].value;
                return result;
            }
            else
            {
                LogError(string.Format("Can not get GameObject with key({0}) in [{1}].", key, this.name));
                return null;
            }
        }
    }

    public T Get<T>(string key) where T : Component
    {
        GameObject GetGameObject = Get(key);
        if (GetGameObject != null)
        {
            if(useCache == true)
            {
                if(componentCache.TryGetValue(key, out Component result) == false)
                {
                    result = GetWithoutCache();
                    componentCache.Add(key, result);
                }
                return result as T;
            }
            else
            {
                return GetWithoutCache();
            }
        }
        else
        {
            return null;
        }


        T GetWithoutCache()
        {
            T Component = GetGameObject.GetComponent<T>();
            if (Component != null)
            {
                return Component;
            }
            else
            {
                LogError(string.Format("Can not get Component with key({0}) in [{1}].", key, this.name));
                return null;
            }
        }
    }

    //------------------------------------------------------------------//

    //void Log(string LogText) { Debug.Log(LogText); }
    void LogError(string logText) { Debug.LogError(logText); }

    //------------------------------------------------------------------//

    class ArrayDictionary
    {
        public static void Set(ref PairStruct[] refPair, string key, GameObject value)
        {
            int KeyLoc = GetKeyLoc(ref refPair, key);
            if (KeyLoc == -1)
            {
                System.Array.Resize(ref refPair, refPair.Length + 1);
                refPair[refPair.Length - 1] = new PairStruct() { key = key, value = value };

            }
            else
            {
                refPair[KeyLoc] = new PairStruct() { key = key, value = value };
            }
        }

        public static bool Remove(ref PairStruct[] refPair, string key)
        {
            int KeyLoc = GetKeyLoc(ref refPair, key);
            if (KeyLoc >= 0)
            {
                refPair = System.Array.FindAll(refPair, El => El.key != key);
            }
            return KeyLoc >= 0;
        }

        public static int GetKeyLoc(ref PairStruct[] refPair, string key)
        {
            int KeyLoc = -1;
            for (int i = 0; i < refPair.Length; i++)
            {
                if (refPair[i].key == key) { KeyLoc = i; break; }
            }
            return KeyLoc;
        }
    }

    [System.Serializable]
    public struct PairStruct
    {
        public string key;
        public GameObject value;
    }
}



