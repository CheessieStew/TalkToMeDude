using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Npgsql;
using Npgsql.Logging;

namespace FirstTry.API
{
    public class ReadResult : FunctionResult
    {
        private readonly JArray _data = new JArray();

        public ReadResult(DbDataReader r, string[] names = null)
        {
            if (!r.HasRows)
                return;
            var schema = r.GetColumnSchema();
            if (names == null)
                Console.Error.WriteLine("FUCK FUCK FUCK\n\n\nFUCKFUCKFUCK");
            names = names ?? schema.Select(c => c.ColumnName).ToArray();

            while (r.Read())
            {
                var row = new JObject();
                for (var i = 0; i < r.FieldCount; i++)
                {
                    switch (schema[i].DataType.Name)
                    {
                        case "String":
                            row.Add(names[i], r.GetString(i));
                            break;
                        case "Int32": 
                            row.Add(names[i], r.GetInt32(i));
                            break;
                        case "Int64": 
                            row.Add(names[i], r.GetInt64(i));
                            break;
                        case "DateTime":
                            row.Add(names[i], r.GetDateTime(i).ToString("yyyy-M-d H:m:s"));
                            break;
                        default:
                            row.Add(names[i], 
                                Error("unknown datatypename " + schema[i].DataType.Name).ToJson());
                            break;
                    }
                }
                _data.Add(row);
            }

        }
        
        public override JObject ToJson()
        {
            var b = base.ToJson();
            b.Add("data",_data);
            return b;
        }
    }
}