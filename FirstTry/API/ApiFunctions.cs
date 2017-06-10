using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using FirstTry.IO;
using FirstTry.Utils;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace FirstTry.API
{
    internal delegate FunctionResult ApiFunction(Connection c);

     internal static partial class ApiFunctions
    {
        private static readonly Dictionary<string, Func<Connection, JToken, FunctionResult>> Functions;
        private const string Secret = "d8578edf8458ce06fbc5bb76a58c5ca4";
        
        static ApiFunctions()
        {
            Functions = new Dictionary<string, Func<Connection, JToken, FunctionResult>>
            {
                {"open", Connect},
                {"organizer", Organizer},
                {"event", Event},
                {"user", User},
                {"talk", Talk},
                {"register_user_for_event", RegisterUserForEvent},
                {"attendance", Attendance},
                {"evaluation", Evaluation},
                {"reject", Reject},
                {"proposal", Proposal},
                {"friends", Friends},
                {"user_plan", UserPlan},
                {"day_plan", DayPlan},
                {"best_talks", BestTalks},
                {"most_popular_talks", MostPopularTalks},
                {"attended_talks", AttendedTalks},
                {"abandoned_talks", AbandonedTalks},
                {"recently_added_talks", RecentlyAddedTalks},
                {"rejected_talks", RejectedTalks},
                {"proposals", Proposals},
                {"friends_talks", FriendsTalks},
                {"friends_events", FriendsEvents},
                {"recommended_talks", RecommendedTalks}
            };
        }

        private static void Log(string str)
        {
            Console.Error.WriteLine(str);
        }
        
        public static ApiFunction Get(JToken call)
        {

            if (call == null || call.Count() != 1)
                return InvalidJson();
            try
            {
                var f = Functions[call.First.Path];
                return c => f(c, call[call.First.Path]);
            }
            catch (KeyNotFoundException)
            {
                return UnknownFunction();
            }
        }

        private static ApiFunction InvalidJson()
        {
            return c =>
            {
                Log("///Invalid json///");
                return FunctionResult.NotImplemented();
            };
        }

        private static ApiFunction UnknownFunction()
        {
            return c =>
            {
                Log("///Unknown function///");
                return FunctionResult.NotImplemented();
            };
        }
        
        private static Authorization AuthorizeUser(Connection c, string login, string password)
        {
            // this should, obviously, be a little more secure
            // but the program is kind of a mock anyway, so we just store the passwords directly
            var exists = c.Command("select id from participant where login=@login and password = @password;");
            exists.Parameters.AddWithValue("login", login);
            exists.Parameters.AddWithValue("password", password);
            using (c.BeginTransaction())
            {
                var res = exists.ExecuteScalar();
                if (res == null)
                {
                    Log("NOT VALIDATED");
                    return Authorization.Unknown;
                }
                var org = c.Command("select * from organiser where id = @uid;");
                org.Parameters.AddWithValue("uid", res);
                var orgRes = org.ExecuteScalar();
                if (orgRes == null)
                {
                    Log("USER VALIDATED");
                    return Authorization.User;
                }
                Log("ORG VALIDATED");
                return Authorization.Organizer;
            }
        }
        
        private static FunctionResult Connect(Connection _, JToken call)
        {
            Log("///Connect function///");
            var login = call["login"].SafeToObject<string>();
            var database = call["baza"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var res = new Connection(database,login,password); 
            return TrySetUp(res) ? res : FunctionResult.Error("Could not initialize the database");
        }

        private static bool TrySetUp(Connection c)
        {
            try
            {
                var q = c.Command("select count(*) from pg_tables where tablename = 'talk';").ExecuteScalar();
                int init;
                int.TryParse(q.ToString(), out init);
                if (init == 1)
                    return true;
                Log($"database needs initialization");
                if (!File.Exists("./SetUp.sql"))
                    return false;
                
                c.Command(File.ReadAllText("./SetUp.sql")).ExecuteNonQuery();
                return true;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException)
            {
                return false;
            }
        }
       
        private enum Authorization
        {
            Organizer,
            User,
            Unknown
        }

        private static bool AnyNullOrEmpty(params string[] strs)
        {
            return strs.Any(string.IsNullOrEmpty);
        }
    }
}