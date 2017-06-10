using System;
using System.Diagnostics;
using FirstTry.API;
using Npgsql;

namespace FirstTry.IO
{
    internal class Connection : FunctionResult
    {
        private readonly NpgsqlConnection _connection;
        public Connection(string database, string login, string password, string host = "localhost")
        {
            _connection = new NpgsqlConnection($"Host={host};Username={login};Password={password};Database={database}");
            Status = ResultStatus.Ok;
            try
            {
                _connection.Open();
            }
            catch (NpgsqlException e)
            {
                Status = ResultStatus.Error;
                ErrorMessage = e.Message;
            }
        }

        public NpgsqlCommand Command(string cmd)
        {
            return new NpgsqlCommand(cmd, _connection);
        }

        public NpgsqlTransaction BeginTransaction()
        {
            return _connection.BeginTransaction();
        }
        
        public void Close()
        {
            _connection.Close();
        }
    }
}