using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.ReplyMarkups;

namespace GreyWolfSupportBot
{
    class Program
    {
        public static long SupportId;
        public static long AdminchatId;
        public static List<int> BananaUsers;
        public static List<int> SupportAdmins;
        public static Dictionary<int, bool> BotAdmins;
        public static Dictionary<long, string> UnpreferredGroups;
        public static Dictionary<long, Message> UnpreferredGroupsHandling = new Dictionary<long, Message>();
        public static string Token;

        public static int DefaultPin;
        public static string DefaultWelcome;
        public static string IssuePin;
        public static string IssueWelcome;


        public const string Directory = "C:\\GreyWolfSupportBot";
        public const string Database = "Database.sqlite";
        public static readonly string connectionstring = $"Data Source={Directory}\\{Database};Version=3;";

        public static bool running = true;
        public static readonly DateTime starttime = DateTime.Now;


        static void Main(string[] args)
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory)) System.IO.Directory.CreateDirectory(Directory);
                if (!System.IO.File.Exists($"{Directory}\\{Database}")) SQL.FirstTime();

                Token = SQL.GetToken();
                Bot.Api = new TelegramBotClient(Token);
                Bot.Me = Bot.Api.GetMeAsync().Result;

                SQL.ReadConfig();
                BananaUsers = SQL.GetBananas();
                UnpreferredGroups = SQL.GetUnpreferred();
                BotAdmins = SQL.GetBotAdmins();
                SupportAdmins = GetSupportAdmins();

                Bot.Api.OnMessage += Bot_OnMessage;
                Bot.Api.OnInlineQuery += Bot_OnInlineQuery;
                Bot.Api.OnCallbackQuery += Bot_OnCallbackQuery;
                Bot.Api.StartReceiving();
                Bot.Send("Started Up!", AdminchatId);
                Console.Write("Program running!" + Environment.NewLine + Environment.NewLine);

                while (running)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                List<long> MenuGroupIds = UnpreferredGroupsHandling.Keys.ToList();
                foreach (var id in MenuGroupIds)
                {
                    Bot.Edit("Menu closed (the bot was stopped while the menu was open).", UnpreferredGroupsHandling[id]);
                    UnpreferredGroupsHandling[id] = null;
                    UnpreferredGroupsHandling.Remove(id);
                }
                return;
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    error += "\n\n" + ex.Message;
                }
                error += "\n\n" + ex.StackTrace;
                Bot.Send(error, BotAdmins.First().Key);
            }
        }

        public static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (!running) return;

                Message msg = e.Message;

                if (!string.IsNullOrEmpty(msg.Text) && msg.Date >= starttime.AddSeconds(5))
                {
                    string[] args = msg.Text.Contains(' ')
                                        ? new[] { msg.Text.Split(' ')[0], msg.Text.Remove(0, msg.Text.IndexOf(' ') + 1) }
                                        : new[] { msg.Text, null };

                    if (msg.Chat.Id == SupportId)
                    {
                        if (msg.Text.ToLower().Contains("banana") && !BananaUsers.Contains(msg.From.Id))
                        {
                            BananaUsers.Add(msg.From.Id);
                            SQL.RunNoResultQuery($"insert into bananas values ({msg.From.Id})");
                            Bot.Reply($"#bananacount {BananaUsers.Count}! Thanks for reading the pinned message!", msg);
                        }
                        if (SupportAdmins.Contains(msg.From.Id))
                        {
                            if (msg.Text == IssueWelcome)
                            {
                                var IssueMsg = Bot.Reply(IssuePin, SupportId, DefaultPin);
                                var IssueSuccess = Bot.Pin(SupportId, IssueMsg.MessageId);
                            }
                            else if (msg.Text == DefaultWelcome)
                            {
                                try
                                {
                                    var NormalSuccess = Bot.Pin(SupportId, DefaultPin);
                                }
                                catch (AggregateException ex)
                                {
                                    if (ex.InnerExceptions.Any(x => x.Message.ToLower().Contains("chat_not_modified"))) return;
                                    throw ex;
                                }
                            }
                            else
                            {
                                switch (args[0].ToLower().Replace('@' + Bot.Me.Username.ToLower(), ""))
                                {
                                    case "/reloadadmins":
                                        SupportAdmins = GetSupportAdmins();
                                        BotAdmins = SQL.GetBotAdmins();
                                        Bot.Reply("<b>RELOADED ADMINS:</b>\n\n<b>Support admins:</b>\n" + string.Join("\n", SupportAdmins) + "\n\n<b>Bot admins:</b>\n" + string.Join("\n", BotAdmins), msg);
                                        break;

                                    case "/setpin":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            DefaultPin = msg.ReplyToMessage.MessageId;
                                            SQL.RunNoResultQuery($"update config set defaultpin = {DefaultPin}");
                                            Bot.Reply("Successfully set that message as pin message!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the pin message!", msg);
                                        break;

                                    case "/setissuewelcome":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            if (msg.ReplyToMessage.Text.ToLower().StartsWith("/welcome "))
                                            {
                                                IssueWelcome = msg.ReplyToMessage.Text;
                                                SQL.RunNoResultQuery($"update config set issuewelc = '{IssueWelcome.Replace("'", "''")}'");
                                                Bot.Reply("Issue welcome set!", msg);
                                            }
                                            else Bot.Reply("This is not a welcome defining message - it needs to start with <code>/welcome</code>", msg);
                                        }
                                        else Bot.Reply("You need to reply to the issue welcome!", msg);
                                        break;

                                    case "/setwelcome":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            if (msg.ReplyToMessage.Text.ToLower().StartsWith("/welcome "))
                                            {
                                                DefaultWelcome = msg.ReplyToMessage.Text;
                                                SQL.RunNoResultQuery($"update config set defaultwelc = '{DefaultWelcome.Replace("'", "''")}'");
                                                Bot.Reply("Welcome set!", msg);
                                            }
                                            else Bot.Reply("This is not a welcome defining message - it needs to start with <code>/welcome</code>", msg);
                                        }
                                        else Bot.Reply("You need to reply to the welcome!", msg);
                                        break;

                                    case "/setissuepin":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            IssuePin = msg.ReplyToMessage.Text;
                                            SQL.RunNoResultQuery($"update config set issuepin = '{IssuePin.Replace("'", "''")}'");
                                            Bot.Reply("Issue pin message set!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the issue pin message!", msg);
                                        break;
                                }
                            }
                        }
                    }

                    if (BotAdmins.ContainsKey(msg.From.Id))
                    {
                        long id;

                        switch (args[0].ToLower().Replace('@' + Bot.Me.Username, ""))
                        {
                            case "/shutdown":
                                if (BotAdmins[msg.From.Id])
                                {
                                    Bot.Reply("Shutting down.", msg);
                                    running = false;
                                }
                                else Bot.Reply("You are bot admin but not dev - this means you don't have permission to this command.", msg);
                                break;

                            case "/sql":
                                if (BotAdmins[msg.From.Id])
                                {
                                    try
                                    {
                                        var conn = new SQLiteConnection(connectionstring);

                                        string raw = "";

                                        if (string.IsNullOrEmpty(args[1]))
                                        {
                                            Bot.Reply("You need to enter a query...", msg);
                                            return;
                                        }

                                        string reply = "";

                                        var queries = args[1].Split(';');
                                        foreach (var sql in queries)
                                        {
                                            conn.Open();

                                            using (var comm = conn.CreateCommand())
                                            {
                                                comm.CommandText = sql;
                                                var reader = comm.ExecuteReader();
                                                var result = "";
                                                if (reader.HasRows)
                                                {
                                                    for (int i = 0; i < reader.FieldCount; i++)
                                                        raw += reader.GetName(i) + (i == reader.FieldCount - 1 ? "" : " - ");
                                                    result += raw + Environment.NewLine;
                                                    raw = "";
                                                    while (reader.Read())
                                                    {
                                                        for (int i = 0; i < reader.FieldCount; i++)
                                                            raw += (reader.IsDBNull(i) ? "<i>NULL</i>" : reader[i]) + (i == reader.FieldCount - 1 ? "" : " - ");
                                                        result += raw + Environment.NewLine;
                                                        raw = "";
                                                    }
                                                }
                                                if (reader.RecordsAffected > 0) result += $"\n<i>{reader.RecordsAffected} record(s) affected.</i>";
                                                else if (string.IsNullOrEmpty(result)) result = sql.ToLower().StartsWith("select") || sql.ToLower().StartsWith("update") || sql.ToLower().StartsWith("pragma") || sql.ToLower().StartsWith("delete") ? "<i>Nothing found.</i>" : "<i>Done.</i>";
                                                reply += "\n\n" + result;
                                                conn.Close();
                                            }

                                            if (new[] { "insert into botadmins", "delete from botadmins", "update botadmins" }.Any(x => sql.ToLower().StartsWith(x))) BotAdmins = SQL.GetBotAdmins();
                                            if (new[] { "insert into bananas", "delete from bananas", "update bananas" }.Any(x => sql.ToLower().StartsWith(x))) BananaUsers = SQL.GetBananas();
                                            if (new[] { "insert into config", "delete from config", "update config" }.Any(x => sql.ToLower().StartsWith(x))) SQL.ReadConfig();
                                            if (new[] { "insert into unpreferredgroups", "delete from unpreferredgroups", "update unpreferredgroups" }.Any(x => sql.ToLower().StartsWith(x))) UnpreferredGroups = SQL.GetUnpreferred();
                                        }
                                        Bot.Reply(reply, msg);
                                    }
                                    catch (SQLiteException sqle)
                                    {
                                        Exception exc = sqle;
                                        while (exc.InnerException != null) exc = exc.InnerException;

                                        Bot.Reply("<b>SQLite Error!</b>\n\n" + exc.Message, msg);
                                    }
                                    catch (Exception exc)
                                    {
                                        string error = exc.Message;
                                        while (exc.InnerException != null)
                                        {
                                            exc = exc.InnerException;
                                            error += "\n\n" + exc.Message;
                                        }
                                        error += exc.StackTrace;

                                        Bot.Reply(error, msg);
                                    }
                                }
                                else Bot.Reply("You are bot admin but not dev - this means you don't have permission to this command.", msg);
                                break;

                            case "/unprefer":
                                if (args[1].Split(' ').Count() >= 1 && long.TryParse(args[1].Split(' ')[0], out id))
                                {
                                    if (UnpreferredGroupsHandling.ContainsKey(id))
                                    {
                                        Bot.Edit("Menu closed (a new menu for that group was opened).", UnpreferredGroupsHandling[id]);
                                        UnpreferredGroupsHandling.Remove(id);
                                    }

                                    var reason = args[1].Remove(0, args[1].IndexOf(' '));
                                    if (UnpreferredGroups.ContainsKey(id))
                                    {
                                        var Markup = new InlineKeyboardMarkup
                                        (
                                            new InlineKeyboardButton[]
                                            {
                                                new InlineKeyboardCallbackButton("Yes", "EditReason|" + id.ToString() + "|" + reason),
                                                new InlineKeyboardCallbackButton("No", "EditReasonNo|" + id.ToString()),
                                            }
                                        );

                                        var HandleMessage = Bot.Reply($"{id} was already unpreferred for {UnpreferredGroups[id]}! Do you want to overwrite the reason?", msg, Markup);
                                        UnpreferredGroupsHandling.Add(id, HandleMessage);
                                    }
                                    else
                                    {
                                        UnpreferredGroups.Add(id, reason);
                                        SQL.RunNoResultQuery($"insert into unpreferredgroups values ({id}, '{reason.Replace("'", "''")}')");
                                        Bot.Reply($"{id} was unpreferred for {reason}", msg);
                                    }
                                }
                                else Bot.Reply("Wrong Syntax!\n\n/unprefer [groupid] [reason]", msg);
                                break;

                            case "/prefer":
                                if (long.TryParse(args[1], out id))
                                {
                                    if (UnpreferredGroups.ContainsKey(id))
                                    {
                                        Bot.Reply($"Preferring {id} which was unpreferred for {UnpreferredGroups[id]}.", msg);
                                        UnpreferredGroups.Remove(id);
                                        SQL.RunNoResultQuery($"delete from unpreferredgroups where id = {id}");
                                    }
                                    else Bot.Reply($"{id} wasn't even unpreferred!", msg);
                                }
                                else Bot.Reply("Wrong syntax!\n\n/prefer [groupid]", msg);
                                break;

                            case "/getreason":
                                if (long.TryParse(args[1], out id))
                                {
                                    if (UnpreferredGroups.ContainsKey(id))
                                    {
                                        Bot.Reply($"Group {id} is unpreferred for: {UnpreferredGroups[id]}", msg);
                                    }
                                    else Bot.Reply($"I don't have a reason for group {id} stored!", msg);
                                }
                                else Bot.Reply("Wrong syntax!\n\n/getreason [groupid]", msg);
                                break;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    error += "\n\n" + ex.Message;
                }
                error += "\n\n" + ex.StackTrace;
                Console.Write(error);
                Bot.Send(error, BotAdmins.First().Key);
                return;
            }
        }

        public static void Bot_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            try
            {
                if (!running) return;

                InlineQuery query = e.InlineQuery;

                if (SupportAdmins.Contains(query.From.Id))
                {
                    if (new[] { IssuePin, IssueWelcome, DefaultWelcome }.All(x => x != "dummy") && DefaultPin != 0)
                    {
                        Bot.Api.AnswerInlineQueryAsync(query.Id, InlineResults.Admin).Wait();
                    }
                    else Bot.Api.AnswerInlineQueryAsync(query.Id, InlineResults.NotSet);
                }
                else
                {
                    Bot.Api.AnswerInlineQueryAsync(query.Id, InlineResults.NotAdmin).Wait();
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    error += "\n\n" + ex.Message;
                }
                error += "\n\n" + ex.StackTrace;
                Console.Write(error);
                Bot.Send(error, BotAdmins.First().Key);
                return;
            }
        }

        public static void Bot_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            try
            {
                if (!running) return;

                var query = e.CallbackQuery;
                var args = e.CallbackQuery.Data.Split('|');
                long id = long.Parse(args[1]);

                switch (args[0])
                {
                    case "EditReason":
                        if (!BotAdmins.ContainsKey(query.From.Id))
                        {
                            Bot.Api.AnswerCallbackQueryAsync(query.Id, "You are not a bot admin!", true).Wait();
                            return;
                        }

                        var reason = args[2];

                        if (UnpreferredGroups.ContainsKey(id))
                        {
                            UnpreferredGroups[id] = reason;
                            SQL.RunNoResultQuery($"update unpreferredgroups set reason = '{reason.Replace("'", "''")}' where id = {id}");
                        }
                        else
                        {
                            UnpreferredGroups.Add(id, reason);
                            SQL.RunNoResultQuery($"insert into unpreferredgroups values ({id}, '{reason.Replace("'", "''")}')");
                        }

                        if (UnpreferredGroupsHandling.ContainsKey(id))
                        {
                            var text = UnpreferredGroupsHandling[id].Text;
                            Bot.Edit(text + "\n\n" + query.From.FirstName + ": Yes!\n\nReason edited!", UnpreferredGroupsHandling[id]);
                            UnpreferredGroupsHandling.Remove(id);
                        }
                        Bot.Api.AnswerCallbackQueryAsync(query.Id, "Reason edited.");
                        break;

                    case "EditReasonNo":
                        if (!BotAdmins.ContainsKey(query.From.Id))
                        {
                            Bot.Api.AnswerCallbackQueryAsync(query.Id, "You are not a bot admin!", true).Wait();
                            return;
                        }

                        if (UnpreferredGroupsHandling.ContainsKey(id))
                        {
                            var text = UnpreferredGroupsHandling[id].Text;
                            Bot.Edit(text + "\n\n" + query.From.FirstName + ": No!\n\nReason wasn't edited!", UnpreferredGroupsHandling[id]);
                            UnpreferredGroupsHandling.Remove(id);
                        }
                        Bot.Api.AnswerCallbackQueryAsync("Reason not edited.");
                        break;
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    error += "\n\n" + ex.Message;
                }
                error += "\n\n" + ex.StackTrace;
                Console.Write(error);
                Bot.Send(error, BotAdmins.First().Key);
                return;
            }
        }


        static class Bot
        {
            public static ITelegramBotClient Api;
            public static User Me;

            public static Message Send(string text, long chatid, IReplyMarkup replyMarkup = null, ParseMode parseMode = ParseMode.Html, bool disableWebPagePreview = true)
            {
                try
                {
                    var t = Api.SendTextMessageAsync(chatid, text, parseMode, disableWebPagePreview, replyMarkup: replyMarkup);
                    t.Wait();
                    return t.Result;
                }
                catch
                {
                    return null;
                }
            }

            public static Message Reply(string text, Message message, IReplyMarkup replyMarkup = null, ParseMode parseMode = ParseMode.Html, bool disableWebPagePreview = true)
            {
                return Reply(text, message.Chat.Id, message.MessageId, replyMarkup, parseMode, disableWebPagePreview);
            }

            public static Message Reply(string text, long chatid, int messageid, IReplyMarkup replyMarkup = null, ParseMode parseMode = ParseMode.Html, bool disableWebPagePreview = true)
            {
                try
                {
                    var t = Api.SendTextMessageAsync(chatid, text, parseMode, disableWebPagePreview, replyMarkup: replyMarkup, replyToMessageId: messageid);
                    t.Wait();
                    return t.Result;
                }
                catch
                {
                    return null;
                }
            }

            public static bool Pin(long chatid, int messageid, bool disableNotification = true)
            {
                var t = Api.PinChatMessageAsync(chatid, messageid, disableNotification);
                t.Wait();
                return t.Result;
            }

            public static Message Edit(string newtext, Message message, IReplyMarkup replyMarkup = null, ParseMode parseMode = ParseMode.Html, bool disableWebPagePreview = true)
            {
                if (message.Text == newtext) return message;
                return Edit(newtext, message.Chat.Id, message.MessageId, replyMarkup, parseMode, disableWebPagePreview);
            }

            public static Message Edit(string newtext, long chatid, int messageid, IReplyMarkup replyMarkup = null, ParseMode parseMode = ParseMode.Html, bool disableWebPagePreview = true)
            {
                try
                {
                    var t = Api.EditMessageTextAsync(chatid, messageid, newtext, parseMode, disableWebPagePreview, replyMarkup);
                    t.Wait();
                    return t.Result;
                }
                catch
                {
                    return null;
                }
            }
        }

        static class InlineResults
        {
            public static InlineQueryResultArticle[] NotAdmin
            {
                get
                {
                    return new[]
                    {
                        new InlineQueryResultArticle() { Id = "NotAdmin", Title = "You are not support admin!", InputMessageContent = new InputTextMessageContent() { MessageText = "Ooops! I just tried to use the support bot inline, but I am not a support admin!" } }
                    };
                }
            }

            public static InlineQueryResultArticle[] Admin
            {
                get
                {
                    return new[]
                    {
                        new InlineQueryResultArticle() { Id = "IssueWelcome", Title = "Issue Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = IssueWelcome } },
                        new InlineQueryResultArticle() { Id = "NormalWelcome", Title = "Standard Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = DefaultWelcome } },
                    };
                }
            }

            public static InlineQueryResultArticle[] NotSet
            {
                get
                {
                    return new[]
                    {
                        new InlineQueryResultArticle() { Id = "NotSet", Title = "Unusable yet, click for info", InputMessageContent = new InputTextMessageContent() { MessageText = "At least one of default pin, default welcome, issue pin, issue welcome wasn't set yet. @Olgabrezel come fix it!" } },
                    };
                }
            }
        }

        public static class SQL
        {
            public static void FirstTime()
            {
                bool rightFormat = false;
                string error = "";
                string token;
                int owner;
                string support;
                string adminchat;

                Console.WriteLine("First use configuration");

                do
                {
                    Console.Write($"\n\n{error}Enter the bot token, as given by @Botfather:\n");
                    token = Console.ReadLine();
                    rightFormat = token.Split(':').Count() == 2 && int.TryParse(token.Split(':')[0], out int dummy);
                    error = "Invalid! ";
                }
                while (!rightFormat);

                rightFormat = false;
                error = "";

                do
                {
                    Console.Write($"\n\n{error}Enter your user ID:\n");
                    string dummy = Console.ReadLine();
                    rightFormat = int.TryParse(dummy, out owner) && owner > 0;
                    error = "Invalid! ";
                }
                while (!rightFormat);

                rightFormat = false;
                error = "";

                do
                {
                    Console.Write($"\n\n{error}Enter the chat ID of the support chat:\n");
                    support = Console.ReadLine();
                    rightFormat = long.TryParse(support, out long dummy) && dummy < 0;
                    error = "Invalid! ";
                }
                while (!rightFormat);

                rightFormat = false;
                error = "";

                do
                {
                    Console.Write($"\n\n{error}Enter the chat ID of the admin chat:\n");
                    adminchat = Console.ReadLine();
                    rightFormat = long.TryParse(adminchat, out long dummy) && dummy < 0;
                    error = "Invalid! ";
                }
                while (!rightFormat);

                SQLiteConnection.CreateFile($"{Directory}\\{Database}");
                RunNoResultQuery("create table botadmins (id int primary key not null unique, isdev boolean not null default 0)");
                RunNoResultQuery("create table bananas (id int primary key not null unique)");
                RunNoResultQuery("create table config (token varchar(255), defaultpin int, defaultwelc varchar(255), issuepin varchar(255), issuewelc varchar(255), supportid varchar(255), adminchat varchar(255))");
                RunNoResultQuery("create table unpreferredgroups (id varchar(255) unique primary key, reason varchar(255))");
                RunNoResultQuery($"insert into botadmins values ({owner}, 1)");
                RunNoResultQuery($"insert into config values ('{token}', 0, 'dummy', 'dummy', 'dummy', '{support}', '{adminchat}')");

                Console.Clear();
            }

            public static void RunNoResultQuery(string query)
            {
                var conn = new SQLiteConnection(connectionstring);
                var comm = new SQLiteCommand(query, conn);
                conn.Open();
                comm.ExecuteNonQuery();
                conn.Close();
            }

            public static Dictionary<int, bool> GetBotAdmins()
            {
                var query = "select id, isdev from botadmins";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var admins = new Dictionary<int, bool>();
                while (reader.Read())
                {
                    admins.Add((int)reader[0], (bool)reader[1]);
                }
                return admins;
            }

            public static List<int> GetBananas()
            {
                var query = "select id from bananas";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var bananas = new List<int>();
                while (reader.Read())
                {
                    bananas.Add((int)reader[0]);
                }
                return bananas;
            }

            public static Dictionary<long, string> GetUnpreferred()
            {
                var query = "select id, reason from unpreferredgroups";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var unpreferred = new Dictionary<long, string>();
                while (reader.Read())
                {
                    unpreferred.Add(long.Parse((string)reader[0]), (string)reader[1]);
                }
                return unpreferred;
            }

            public static string GetToken()
            {
                var query = "select token from config";
                var conn = new SQLiteConnection(connectionstring);
                var comm = new SQLiteCommand(query, conn);
                conn.Open();
                var reader = comm.ExecuteReader();
                reader.Read();
                return (string)reader[0];
            }

            public static void ReadConfig()
            {
                var query = "select * from config";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                reader.Read();
                DefaultPin = (int)reader[1];
                DefaultWelcome = (string)reader[2];
                IssuePin = (string)reader[3];
                IssueWelcome = (string)reader[4];
                SupportId = long.Parse((string)reader[5]);
                AdminchatId = long.Parse((string)reader[6]);
                conn.Close();
            }
        }

        public static List<int> GetSupportAdmins()
        {
            return Bot.Api.GetChatAdministratorsAsync(SupportId).Result.Select(x => x.User.Id).ToList();
        }
    }
}
