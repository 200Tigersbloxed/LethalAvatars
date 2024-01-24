using System;
using System.Linq;
using System.Reflection;

namespace LethalAvatars.SDK.Editor.Internals;

internal static class Reflecting
{
    public static object InvokePrivateMethod(Type t, string methodName, object[] parameters, object reference = null)
    {
        MethodInfo methodInfo = t.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        if(methodInfo == null) return null;
        return methodInfo.Invoke(reference, parameters);
    }
    
    public static Type FindType(string typeName)
    {
        Type t;
        try
        {
            t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .First(type => type.Name.Contains(typeName));
        }
        catch (Exception)
        {
            t = null;
        }
        return t;
    }
}