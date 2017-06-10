using System;
using FirstTry.IO;
using FirstTry.Utils;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace FirstTry.API
{
    internal static partial class ApiFunctions
    {
        //Każde z poniższych wywołań powinno zwrócić obiekt JSON zawierający wyłącznie status wykonania: OK/ERROR/NOT IMPLEMENTED.
        private static FunctionResult Organizer(Connection c, JToken call)
        {
            Log("///Organizer function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var secret = call["secret"].SafeToObject<string>();
            var newlogin = call["newlogin"].SafeToObject<string>();
            var newpassword = call["newpassword"].SafeToObject<string>();
            
            if (secret != Secret || AnyNullOrEmpty(newlogin, newpassword))
                return FunctionResult.Error("Bad arguments");
            try
            {
                using (var transaction = c.BeginTransaction())
                {
                    var command1 =
                        c.Command("INSERT INTO participant(login,password) values(@newlogin, @newpassword);");
                    command1.Parameters.AddWithValue("newlogin", newlogin);
                    command1.Parameters.AddWithValue("newpassword", newpassword);
                    var command2 = c.Command(
                        "INSERT INTO organiser values((select id from participant where login = @newlogin));");
                    command2.Parameters.AddWithValue("newlogin", newlogin);
                    if (command1.ExecuteNonQuery() != 1 || command2.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add needed rows");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException)
            {
                return FunctionResult.Error(e.Message);
            }
        }

        private static FunctionResult Event(Connection c, JToken call)
        {
            Log("///Event function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var eventName = call["eventname"].SafeToObject<string>();
            var startTimestamp = call["start_timestamp"].SafeToObject<string>();
            var endTimestamp = call["end_timestamp"].SafeToObject<string>();
            
            if (AnyNullOrEmpty(login, password, startTimestamp, endTimestamp))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) != Authorization.Organizer)
                    return FunctionResult.Error("User is not an organizer.");
                var command = c.Command(
                    "INSERT INTO event(start_time,end_time,name) values(@start, @end, @name);");
                command.Parameters.AddWithValue("start", DateTime.Parse(startTimestamp));
                command.Parameters.AddWithValue("end", DateTime.Parse(endTimestamp));
                command.Parameters.AddWithValue("name", eventName);
                return command.ExecuteNonQuery() != 1 
                    ? FunctionResult.Error("Could not add needed rows") 
                    : FunctionResult.Ok();
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }
        }


        
        private static FunctionResult User(Connection c, JToken call)
        {
            /*(*O) user <login> <password> <newlogin> <newpassword> // rejestracja nowego uczestnika <login> i <password> 
            służą do autoryzacji wywołującego funkcję, który musi posiadać uprawnienia organizatora, <newlogin> <newpassword> są danymi 
            nowego uczestnika, <newlogin> jest unikalny
            */
            Log("///User function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var newlogin = call["newlogin"].SafeToObject<string>();
            var newpassword = call["newpassword"].SafeToObject<string>();
        
            if (AnyNullOrEmpty(login, password, newlogin, newpassword))
                return FunctionResult.Error("Bad arguments"); 
            try
            {
                if (AuthorizeUser(c, login, password) != Authorization.Organizer)
                    return FunctionResult.Error("User is not an organizer.");
                var command = c.Command(
                    "INSERT INTO participant(login,password) values(@login, @password);");
                command.Parameters.AddWithValue("login", newlogin);
                command.Parameters.AddWithValue("password", newpassword);
                return command.ExecuteNonQuery() != 1 
                    ? FunctionResult.Error("Could not add the rows needed") 
                    : FunctionResult.Ok();
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException)
            {
                return FunctionResult.Error(e.Message);
            }
        }
        
        private static FunctionResult Talk(Connection c, JToken call)
        {
            /*
            (*O) talk <login> <password> <speakerlogin> <talk> <title> <start_timestamp> <room> <initial_evaluation> <eventname> 
            // rejestracja referatu/zatwierdzenie referatu spontanicznego, <talk> jest unikalnym identyfikatorem referatu,
             <initial_evaluation> jest oceną organizatora w skali 0-10 – jest to ocena traktowana tak samo jak ocena uczestnika obecnego na referacie,
              <eventname> jest nazwą wydarzenia, którego częścią jest dany referat - może być pustym napisem, co oznacza,
               że referat nie jest przydzielony do jakiegokolwiek wydarzenia
            */
            Log("///Talk function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var talker = call["speakerlogin"].SafeToObject<string>();
            var talk = call["talk"].SafeToObject<string>();
            var title = call["title"].SafeToObject<string>();
            var start = call["start_timestamp"].SafeToObject<string>();
            var room = call["room"].SafeToObject<string>();
            var initialEvaluation = call["initial_evaluation"].SafeToObject<string>();
            var eventName = call["eventname"].SafeToObject<string>() ?? "";
            
            if (AnyNullOrEmpty(login, password, talker, talk, title, start, room, initialEvaluation))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) != Authorization.Organizer)
                    return FunctionResult.Error("User is not an organizer.");
                using (var transaction = c.BeginTransaction())
                {

                    var findEvent = c.Command("select id from event where name = @eventName;");
                    findEvent.Parameters.AddWithValue("eventName", eventName);
                    var eventId = findEvent.ExecuteScalar();
                    if (eventId == null)
                        return FunctionResult.Error("Specified event was not found");

                    var findTalker = c.Command("select id from participant where login = @talker");
                    findTalker.Parameters.AddWithValue("talker", talker);
                    var talkerId = findTalker.ExecuteScalar();
                    if (talkerId == null)
                        return FunctionResult.Error("Specified speaker was not found");
                    
                    var findAgenda = c.Command("select id from agenda where given_id = @talk;");
                    findAgenda.Parameters.AddWithValue("talk", talk);
                    var agendaId = findAgenda.ExecuteScalar();

                    
                    if (agendaId == null)
                    {
                        var insertAgenda = c.Command("insert into agenda(given_id,talker_id,start_time,title) values"
                        +"(@talk,@talker,@start,@title);");
                        insertAgenda.Parameters.AddWithValue("talk", talk);
                        insertAgenda.Parameters.AddWithValue("talker", talkerId);
                        insertAgenda.Parameters.AddWithValue("start", DateTime.Parse(start));
                        insertAgenda.Parameters.AddWithValue("title", title);
                        if (insertAgenda.ExecuteNonQuery() != 1)
                            return FunctionResult.Error("Could not add the rows needed");
                        
                        findAgenda = c.Command("select id from agenda where given_id = @talk;");
                        findAgenda.Parameters.AddWithValue("talk", talk);
                        agendaId = findAgenda.ExecuteScalar();
                    }
                    else
                    {
                        var rejected = c.Command("Select id from rejected_agenda where id=@agenda");
                        rejected.Parameters.AddWithValue("agenda", agendaId);
                        if (rejected.ExecuteScalar() != null)
                            return FunctionResult.Error("This talk was rejected.");
                        var updateAgenda = c.Command("update agenda set talker_id=@talker, start_time=@start,"
                                                     + "title=@title where id = @agenda;");
                                                     
                        updateAgenda.Parameters.AddWithValue("talker", talkerId);
                        updateAgenda.Parameters.AddWithValue("start", DateTime.Parse(start));
                        updateAgenda.Parameters.AddWithValue("title", title);
                        updateAgenda.Parameters.AddWithValue("agenda", agendaId);
                        if (updateAgenda.ExecuteNonQuery() != 1)
                            return FunctionResult.Error("Could not add the rows needed");        
                    }
                    var insertTalk = c.Command("INSERT INTO talk values(@event, @agenda, @rating, @room);");
                    insertTalk.Parameters.AddWithValue("event", eventId);
                    insertTalk.Parameters.AddWithValue("agenda", agendaId);
                    insertTalk.Parameters.AddWithValue("rating", int.Parse(initialEvaluation));
                    insertTalk.Parameters.AddWithValue("room", room);
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }
        }

        private static FunctionResult RegisterUserForEvent(Connection c, JToken call)
        {
            /*
            (*U) register_user_for_event <login> <password> <eventname> // rejestracja uczestnika <login> na wydarzenie <eventname>
            */
            Log("///RegisterUserForEvent function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var eventName = call["eventname"].SafeToObject<string>() ?? "";
            
            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");
                using (var transaction = c.BeginTransaction())
                {
                    var findEvent = c.Command("select id from event where name = @eventName;");
                    findEvent.Parameters.AddWithValue("eventName", eventName);
                    var eventId = findEvent.ExecuteScalar();
                    if (eventId == null)
                        return FunctionResult.Error("Specified event was not found");

                    var findUser = c.Command("select id from participant where login = @login");
                    findUser.Parameters.AddWithValue("login", login);
                    var userId = findUser.ExecuteScalar();
                    if (userId == null)
                        return FunctionResult.Error("Specified user was not found");
                    
                    var insertTalk = c.Command("INSERT INTO participant_signsupfor_event values(@user, @event);");
                    insertTalk.Parameters.AddWithValue("event", eventId);
                    insertTalk.Parameters.AddWithValue("user", userId);
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException)
            {
                return FunctionResult.Error(e.Message);
            }
        }

        private static FunctionResult Attendance(Connection c, JToken call)
        {
            //(*U) attendance <login> <password> <talk> // odnotowanie faktycznej obecności uczestnika <login> na referacie <talk>

            Log("///Attendance function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var talk = call["talk"].SafeToObject<string>();
            
            if (AnyNullOrEmpty(login, password))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");
                using (var transaction = c.BeginTransaction())
                {
                    var findAgenda = c.Command("select id from agenda join talk on (id = agenda_id)"
                                               + " where given_id = @talk;");
                    findAgenda.Parameters.AddWithValue("talk", talk);
                    var talkId = findAgenda.ExecuteScalar();
                    if (talkId == null)
                        return FunctionResult.Error("Specified talk was not found");
                    
                    var findUser = c.Command("select id from participant where login = @login");
                    findUser.Parameters.AddWithValue("login", login);
                    var userId = findUser.ExecuteScalar();
                    if (userId == null)
                        return FunctionResult.Error("Specified user was not found");
                    
                    var insertTalk = c.Command("INSERT INTO participant_attends_talk values(@user, @talk);");
                    insertTalk.Parameters.AddWithValue("talk", talkId);
                    insertTalk.Parameters.AddWithValue("user", userId);
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException)
            {
                return FunctionResult.Error(e.Message);
            }       
        }

        private static FunctionResult Evaluation(Connection c, JToken call)
        {
            //(*U) evaluation <login> <password> <talk> <rating> // ocena referatu <talk> w skali 0-10 przez uczestnika <login>

            Log("///Evaluation function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var talk = call["talk"].SafeToObject<string>();
            var rating = call["rating"].SafeToObject<string>();
            
            if (AnyNullOrEmpty(login, talk, rating, password))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");
                using (var transaction = c.BeginTransaction())
                {
                    var findAgenda = c.Command("select id from agenda join talk on (id = agenda_id)"
                                               + " where given_id = @talk;");
                    findAgenda.Parameters.AddWithValue("talk", talk);
                    var agendaId = findAgenda.ExecuteScalar();
                    if (agendaId == null)
                        return FunctionResult.Error("Specified talk was not found");

                    var findUser = c.Command("select id from participant where login = @login");
                    findUser.Parameters.AddWithValue("login", login);
                    var userId = findUser.ExecuteScalar();
                    if (userId == null)
                        return FunctionResult.Error("Specified user was not found");
                
                    Log($"user {userId} rates {agendaId}");
                    var insertTalk = c.Command("INSERT INTO participant_rates_talk values(@user, @talk, @rating);");
                    insertTalk.Parameters.AddWithValue("talk", agendaId);
                    insertTalk.Parameters.AddWithValue("user", userId);
                    insertTalk.Parameters.AddWithValue("rating", int.Parse(rating));
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                
                return FunctionResult.Error(e.Message);
            }               
        }
        
        private static FunctionResult Proposal(Connection c, JToken call)
        {
            /*
                (U) proposal  <login> <password> <talk> <title> <start_timestamp> // propozycja referatu spontanicznego,
                 <talk> - unikalny identyfikator referatu
            */
            Log("///proposal function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var talk = call["talk"].SafeToObject<string>();
            var title = call["title"].SafeToObject<string>() ?? "";
            var startTimestamp = call["start_timestamp"].SafeToObject<string>();
            if (AnyNullOrEmpty(login, password, talk, startTimestamp))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) == Authorization.Unknown)
                    return FunctionResult.Error("User was not authorized.");
                using (var transaction = c.BeginTransaction())
                {
                    var findUser = c.Command("select id from participant where login = @login");
                    findUser.Parameters.AddWithValue("login", login);
                    var userId = findUser.ExecuteScalar();
                    if (userId == null)
                        return FunctionResult.Error("Specified user was not found");
                    
                    var insertTalk = c.Command("INSERT INTO agenda(given_id,talker_id,start_time,title)"
                                               + " values(@talk, @talker, @start, @title);");
                    insertTalk.Parameters.AddWithValue("talk", talk);
                    insertTalk.Parameters.AddWithValue("talker", userId);
                    insertTalk.Parameters.AddWithValue("start", DateTime.Parse(startTimestamp));
                    insertTalk.Parameters.AddWithValue("title", title);
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }       
        }
        
        private static FunctionResult Reject(Connection c, JToken call)
        {
            /*
                (O) reject <login> <password> <talk> // usuwa referat spontaniczny <talk> z listy zaproponowanych,
            */
            Log("///Reject function///");
            if (c == null || !c.IsOpen)
                return FunctionResult.ConnectionError();
            var login = call["login"].SafeToObject<string>();
            var password = call["password"].SafeToObject<string>();
            var talk = call["talk"].SafeToObject<string>();
            if (AnyNullOrEmpty(login, password, talk))
                return FunctionResult.Error("Bad arguments");
            try
            {
                if (AuthorizeUser(c, login, password) != Authorization.Organizer)
                    return FunctionResult.Error("User is not an organizer.");
                using (var transaction = c.BeginTransaction())
                {
                    var findAgenda = c.Command("select id from agenda where given_id = @talk;");
                    findAgenda.Parameters.AddWithValue("talk", talk);
                    var agendaId = findAgenda.ExecuteScalar();
                    if (agendaId == null)
                        return FunctionResult.Error("Specified talk was not found");
                    var alreadyAccepted = c.Command("select agenda_id from talk where agenda_id = @agenda");
                    alreadyAccepted.Parameters.AddWithValue("agenda", agendaId);
                    if (alreadyAccepted.ExecuteScalar() != null)
                        return FunctionResult.Error("This talk was already accepted");
                    var insertTalk = c.Command("INSERT INTO rejected_agenda values(@talk);");
                    insertTalk.Parameters.AddWithValue("talk", agendaId);
                    
                    if (insertTalk.ExecuteNonQuery() != 1)
                        return FunctionResult.Error("Could not add the rows needed");
                    transaction.Commit();
                    return FunctionResult.Ok();
                }
            }
            catch (Exception e) when (e is PostgresException || e is NpgsqlException || e is FormatException)
            {
                return FunctionResult.Error(e.Message);
            }    
        }
        
        private static FunctionResult Friends(Connection c, JToken call)
        {
            /*
                (U) friends <login1> <password> <login2> // uczestnik <login1> chce nawiązać znajomość z uczestnikiem
                 <login2>, znajomość uznajemy za nawiązaną jeśli obaj uczestnicy chcą ją nawiązać tj. po wywołaniach 
                 friends <login1> <password1> <login2> i friends <login2> <password2> <login1>
            */
            return FunctionResult.NotImplemented();
        }
        
    }
}