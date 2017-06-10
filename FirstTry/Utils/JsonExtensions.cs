using Newtonsoft.Json.Linq;

namespace FirstTry.Utils
{
    public static class JsonExtensions
    {
        public static T SafeToObject<T>(this JToken token) => token == null ? default(T) : token.ToObject<T>();
    }
}