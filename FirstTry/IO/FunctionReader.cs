using System;
using System.Collections.Generic;
using FirstTry.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FirstTry.IO
{
    
    internal static class FunctionReader
    {
        public static IEnumerable<ApiFunction> Read()
        {
            var i = 1;
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                JObject call = null;
                try
                {
                    Console.Error.WriteLine($"read line {i}");
                    call = JObject.Parse(line);
                }
                catch (JsonReaderException)
                {
                }
                yield return ApiFunctions.Get(call);
                i++;
            }
        }
    }

}