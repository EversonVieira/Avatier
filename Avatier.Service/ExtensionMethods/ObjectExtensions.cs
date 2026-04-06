using System.Text.Json;

namespace Avatier.Service.ExtensionMethods
{
    public static class ObjectExtensions
    {
        public static string ToJson<T>(this T obj) where T : class
        {
            return JsonSerializer.Serialize(obj);
        }

    }
}
