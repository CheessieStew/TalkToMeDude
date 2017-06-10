using System;
using FirstTry.API;
using FirstTry.IO;
using FirstTry.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FirstTry
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var functions = FunctionReader.Read().HeadAndTail();
            
            var res = functions.Item1(null);
            var connection = res as Connection;
            Console.WriteLine(res.ToJson().ToString(Formatting.None));

            foreach(var f in functions.Item2)
            {
                var result = f(connection).ToJson().ToString(Formatting.None);
                Console.WriteLine(result);
                Console.Error.WriteLine(result);
                
            }
            Console.Error.WriteLine("eof encountered");
        }
    }
}