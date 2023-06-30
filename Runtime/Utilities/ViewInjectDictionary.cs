
using System.Collections;
using System.Collections.Generic;


public abstract class ViewInjectDictionary
{
    public abstract object GetValue(string key);
    public abstract void TryAdd(string key, object value);
    public abstract bool ContainsKey(string key);
}

/// <summary>
/// A official custom wrapper for ViewSystem model binding when there are multiple instance of a type whould like to sent to a ViewElementBehaviour.
/// 
/// For instance, the ViewElementBehaviour is defined like this
/// public class MyViewBehaviour: ViewElementBehaviour{
///     
///     [ViewElementInject]
///     string testStringInject1;
///     [ViewElementInject]
///     string testStringInject2;
/// 
/// }
/// 
/// This cause runtime error! Due you set one kind type (stirng) twice
/// ViewController.FullPageChanger()
///     .SetPage("MyPage")
///     .SetPageModel("value1", "value2");
///  
/// Use this way instead!
/// var datas = new ViewInjectDictionary<string>();
/// datas.TryAdd("testStringInject1", "value1"); // The Key is the field/property name, the value is the value to set
/// datas.TryAdd("testStringInject2", "value2"); // The Key is the field/property name, the value is the value to set
/// ViewController.FullPageChanger()
///     .SetPage("MyPage")
///     .SetPageModel(datas);
/// 
/// /// /// </summary>
/// <typeparam name="T">The target type</typeparam>
public class ViewInjectDictionary<T> : ViewInjectDictionary
{
    Dictionary<string, T> dictionary = new Dictionary<string, T>();

    public override bool ContainsKey(string key)
    {
        return dictionary.ContainsKey(key);
    }

    public override object GetValue(string key)
    {
        if (dictionary.TryGetValue(key, out T result))
        {
            return result;
        }
        return null;
    }

    public override void TryAdd(string key, object value)
    {
        dictionary.TryAdd(key, (T)value);
    }
}