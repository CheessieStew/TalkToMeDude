using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FirstTry.API
{
    public class FunctionResult
    {
        public enum ResultStatus
        {
            Ok,
            Error,
            NotImplemented
        }

        public ResultStatus Status;

        public string StatusString
        {
            get
            {
                switch(Status)
                {
                    case ResultStatus.Ok:
                        return "OK";
                    case ResultStatus.Error:
                        return "ERROR";
                    case ResultStatus.NotImplemented:
                        return "NOT IMPLEMENTED";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        public string ErrorMessage;
        
        public override string ToString()
        {
            return $"{{\"status\": \"{Status}\" }}";
        }

        public virtual JObject ToJson()
        {
            var res = new JObject {{"status", StatusString}};
            if (Status == ResultStatus.Error && !string.IsNullOrEmpty(ErrorMessage))
                res.Add("message", ErrorMessage);
            return res;
        }

        public static FunctionResult ConnectionError()
        {
            return Error("Connection was not established");
        }
        
        public static FunctionResult Error(string message = "")
        {
            return new FunctionResult {Status = ResultStatus.Error, ErrorMessage = message.Replace('"','\'')};
        }

        public static FunctionResult NotImplemented()
        {
            return new FunctionResult {Status = ResultStatus.NotImplemented};
        }

        public static FunctionResult Ok()
        {
            return new FunctionResult {Status = ResultStatus.Ok};
        }
    }
}