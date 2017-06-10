using System;
using FirstTry.IO;
using FirstTry.Utils;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace FirstTry.API
{
    internal static partial class ApiFunctions
    {


        
        private static FunctionResult UserPlan(Connection c, JToken call)
        {
            /*
            (*N) user_plan <login> <limit> // zwraca plan najbliższych referatów z wydarzeń, na które dany uczestnik jest 
            zapisany (wg rejestracji na wydarzenia) posortowany wg czasu rozpoczęcia, wypisuje pierwsze <limit> referatów,
             przy czym 0 oznacza, że należy wypisać wszystkie
            // Atrybuty zwracanych krotek: 
               <login> <talk> <start_timestamp> <title> <room>            
            */
            Log("///UserPlan function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var login = call["login"].SafeToObject<string>();
            var limit = call["limit"].SafeToObject<string>() ?? "0";
                    
            if (AnyNullOrEmpty(login, limit))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                var limitnumber = int.Parse(limit);
                var select = c.Command("with user_events as (select event_id from participant_signsupfor_event "
                                       + "join participant on (participant_id = participant.id) "
                                       + "where login = @login) "
                                       + "select login, given_id, start_time, title, room from talk " 
                                       + "join agenda on (agenda_id = agenda.id) "
                                       + "join participant on talker_id = participant.id "
                                       + "where start_time > now() AND event_id in (select * from user_events) "
                                       + "order by start_time ASC  "
                                       + (limitnumber > 0 ? "limit @limit;" : ";"));
                select.Parameters.AddWithValue("@login", login);
                select.Parameters.AddWithValue("@limit", limitnumber);

                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"login", "talk", "start_timestamp", "title", "room"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }
        }

        private static FunctionResult DayPlan(Connection c, JToken call)
        {
            /*
            (*N) day_plan <timestamp> // zwraca listę wszystkich referatów zaplanowanych na dany dzień posortowaną rosnąco wg sal, 
            w drugiej kolejności wg czasu rozpoczęcia
                //  <talk> <start_timestamp> <title> <room>      
            */
            Log("///DayPlan function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var timestr = call["timestamp"].SafeToObject<string>();
            
                    
            if (AnyNullOrEmpty(timestr))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                
                var time = DateTime.Parse(timestr);
                var select = c.Command("select given_id, start_time, title, room from "
                                       + "talk join agenda on (agenda_id = agenda.id) "
                                       + "where start_time::date = @time::date "
                                       + "order by room, start_time asc;");
                
                select.Parameters.AddWithValue("@time", time);

                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "start_timestamp", "title", "room"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }        
        }
        
        private static FunctionResult BestTalks(Connection c, JToken call)
        {
            /*
            (*N) best_talks <start_timestamp> <end_timestamp> <limit> <all> // zwraca referaty rozpoczynające się w  
            danym przedziale czasowym posortowane malejąco wg średniej oceny uczestników, przy czym jeśli <all> jest równe 1
             należy wziąć pod uwagę wszystkie oceny, w przeciwnym przypadku tylko oceny uczestników, którzy byli na referacie obecni, 
             wypisuje pierwsze <limit> referatów, przy czym 0 oznacza, że należy wypisać wszystkie
                //  <talk> <start_timestamp> <title> <room>          
            */
            
            
            Log("///BestTalks function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var startstr = call["start_timestamp"].SafeToObject<string>();
            var endstr = call["end_timestamp"].SafeToObject<string>();
            var limit = call["limit"].SafeToObject<string>() ?? "0";
            var all = call["all"].SafeToObject<string>() ?? "";
            if (AnyNullOrEmpty(startstr, endstr))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                var limitnumber = int.Parse(limit);
                var start = DateTime.Parse(startstr);
                var end = DateTime.Parse(endstr);
                
                var select = c.Command("with ratings(participant_id, talk_id, rating) as  "
                                       + "(select null, agenda_id, organiser_rating from talk "
                                       + "UNION select * from participant_rates_talk "
                                       + (all != "1" ? "where (participant_id, talk_id) in (select * from participant_attends_talk))" : ")")
                                       + "select given_id, start_time, title, talk.room from "
                                       + "talk join agenda on (agenda_id = agenda.id) "
                                       + "join ratings on (talk_id = agenda.id) "
                                       + "where start_time <= @end AND start_time >= @start "
                                       + "group by (agenda_id, agenda.id) "
                                       + "order by avg(rating) DESC "
                                       + (limitnumber > 0 ? "limit @limit;" : ";"));
                
                select.Parameters.AddWithValue("limit", limitnumber);
                select.Parameters.AddWithValue("start", start);
                select.Parameters.AddWithValue("end", end);
                
                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "start_timestamp", "title", "room"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }       
        }
        
        private static FunctionResult MostPopularTalks(Connection c, JToken call)
        {
            /*
            (*N) most_popular_talks <start_timestamp> <end_timestamp> <limit> // zwraca referaty rozpoczynające się w 
            podanym przedziału czasowego posortowane malejąco wg obecności, wypisuje pierwsze <limit> referatów,
             przy czym 0 oznacza, że należy wypisać wszystkie
        //  <talk> <start_timestamp> <title> <room>     
            */
            Log("///MostPopularTalks function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var startstr = call["start_timestamp"].SafeToObject<string>();
            var endstr = call["end_timestamp"].SafeToObject<string>();
            var limit = call["limit"].SafeToObject<string>() ?? "0";
            if (AnyNullOrEmpty(startstr, endstr))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                var limitnumber = int.Parse(limit);
                var start = DateTime.Parse(startstr);
                var end = DateTime.Parse(endstr);
                
                var select = c.Command("select given_id, start_time, title, room from "
                                       + "talk join agenda on (agenda_id = agenda.id) "
                                       + "join participant_attends_talk on (talk_id = agenda.id) "
                                       + "where start_time <= @end AND start_time >= @start "
                                       + "group by (agenda.id, agenda_id) order by count (participant_id) DESC "
                                       + (limitnumber > 0 ? "limit @limit;" : ";"));
                
                select.Parameters.AddWithValue("limit", limitnumber);
                select.Parameters.AddWithValue("start", start);
                select.Parameters.AddWithValue("end", end);
                
                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "start_timestamp", "title", "room"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }       
        }
        
        private static FunctionResult AttendedTalks(Connection c, JToken call)
        {
            /*
            (*U) attended_talks <login> <password> // zwraca dla danego uczestnika referaty, na których był obecny 
                //  <talk> <start_timestamp> <title> <room>         
            */
            Log("///AttendedTalks function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            
            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");

                var select = c.Command("select given_id, start_time, title, room from "
                                       + "talk join agenda on (agenda_id = agenda.id) "
                                       + "join participant_attends_talk on (talk_id = agenda.id) "
                                       + "join participant on (participant_id = participant.id) "
                                       + "where login = @login;");
                
                select.Parameters.AddWithValue("login", login);
                
                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "start_timestamp", "title", "room"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }              
        }
        
        private static FunctionResult AbandonedTalks(Connection c, JToken call)
        {
            /*
            (*O) abandoned_talks <login> <password>  <limit> 
            // zwraca listę referatów posortowaną malejąco wg liczby uczestników <number> zarejestrowanych na wydarzenie 
            obejmujące referat, którzy nie byli na tym referacie obecni, wypisuje pierwsze <limit> referatów, przy czym 0
             oznacza, że należy wypisać wszystkie
                //  <talk> <start_timestamp> <title> <room> <number>         
                strzelam, że referaty, na których nie odnotowano nieobecności nie powinny być częścią wyniku{ "abandoned_talks" : {"login": "Org2", "password": "pwd" }}

            */
            Log("///AbandonedTalks function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var limit = call["limit"].SafeToObject<string>() ?? "0";

            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                var limitnumber = int.Parse(limit);
                if (AuthorizeUser(c, login, password) != Authorization.Organizer)
                    return FunctionResult.Error("User was not authorized.");

                var select = c.Command("with missed_attendance(p_id, t_id) as( "
                                       + "select participant_id, agenda_id from "
                                       + "participant_signsupfor_event "
                                       + "join talk using (event_id) "
                                       + "where (participant_id, agenda_id) not in (select * from participant_attends_talk)) "
                                       + "select given_id, start_time, title, room, count(p_id)"
                                       + "from talk join agenda on (agenda_id = agenda.id) "
                                       + "join missed_attendance on (t_id = agenda.id)"
                                       + "group by (agenda.id, agenda_id) "
                                       + "order by count(p_id) DESC "
                                       + (limitnumber > 0 ? "limit @limit;" : ";"));
                                       
                select.Parameters.AddWithValue("limit", limitnumber);

                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "start_timestamp", "title", "room", "number"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }  
        }
        
        private static FunctionResult RecentlyAddedTalks(Connection c, JToken call)
        {
            /*
                (N) recently_added_talks <limit> // zwraca listę ostatnio zarejestrowanych referatów, wypisuje ostatnie 
                <limit> referatów wg daty zarejestrowania, przy czym 0 oznacza, że należy wypisać wszystkie
                //  <talk> <speakerlogin> <start_timestamp> <title> <room>     
            */
            return FunctionResult.NotImplemented();
        }
        
        private static FunctionResult RejectedTalks(Connection c, JToken call)
        {
            /*
                (U/O) rejected_talks <login> <password> // jeśli wywołujący ma uprawnienia organizatora zwraca listę wszystkich
                 odrzuconych referatów spontanicznych, w przeciwnym przypadku listę odrzuconych referatów wywołującego ją uczestnika 
                //  <talk> <speakerlogin> <start_timestamp> <title>        
            */
            Log("///RejectedTalks function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();

            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                var auth = AuthorizeUser(c, login, password);
                if (auth == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");

                var select = c.Command("select given_id, login, start_time, title from "
                                   + "rejected_agenda ra join agenda on(ra.id = agenda.id) "
                                   + "join participant on (talker_id = participant.id) "
                                   + (auth == Authorization.User ? "where login = @login;" : ";"));
                select.Parameters.AddWithValue("login", login);

                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "speakerlogin", "start_timestamp", "title"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }  
        }
        
        private static FunctionResult Proposals(Connection c, JToken call)
        {
            /*
            (O) proposals <login> <password> // zwraca listę propozycji referatów spontanicznych do zatwierdzenia lub
             odrzucenia, zatwierdzenie lub odrzucenie referatu polega na wywołaniu przez organizatora
              funkcji talk lub reject z odpowiednimi parametrami
            //  <talk> <speakerlogin> <start_timestamp> <title>       
            */
            Log("///Proposals function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();

            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User is not an organizer.");

                var select = c.Command("select given_id, login, start_time, title from "
                                       + "agenda join participant on (talker_id = participant.id) "
                                       + "where agenda.id not in ((select agenda_id from talk) "
                                       + "UNION (select id from rejected_agenda));");
               
                var reader = select.ExecuteReader();
            
                var res = new ReadResult(reader, new[] {"talk", "speakerlogin", "start_timestamp", "title"});
                reader.Close();
                return res;
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }  
        }
        
        private static FunctionResult FriendsTalks(Connection c, JToken call)
        {
            /*
            (U) friends_talks <login> <password> <start_timestamp> <end_timestamp> <limit> // lista referatów  rozpoczynających
            się w podanym przedziale czasowym wygłaszanych przez znajomych danego uczestnika posortowana wg czasu rozpoczęcia, 
            wypisuje pierwsze <limit> referatów, przy czym 0 oznacza, że należy wypisać wszystkie
            //  <talk> <speakerlogin> <start_timestamp> <title> <room>      
            */
            return FunctionResult.NotImplemented();
        }
        private static FunctionResult FriendsEvents(Connection c, JToken call)
        {
            /*
            (U) friends_events <login> <password> <eventname> // lista znajomych uczestniczących w danym wydarzeniu
            //  <login> <eventname> <friendlogin>     
            */
            return FunctionResult.NotImplemented();
        }
        
        private static FunctionResult RecommendedTalks(Connection c, JToken call)
        {
            /*
            (U) recommended_talks <login> <password> <start_timestamp> <end_timestamp> <limit> 
            // zwraca referaty rozpoczynające się w podanym przedziale czasowym, które mogą zainteresować danego uczestnika 
            (zaproponuj parametr <score> obliczany na podstawie dostępnych danych – ocen, obecności, znajomości itp.), wypisuje
             pierwsze <limit> referatów wg nalepszego <score>, przy czym 0 oznacza, że należy wypisać wszystkie
            //  <talk> <speakerlogin> <start_timestamp> <title> <room> <score>   
            */
            return FunctionResult.NotImplemented();
        }
    }
}