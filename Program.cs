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
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.ReplyMarkups;

namespace GreyWolfSupportBot
{
    class Program
    {
        public static long SupportId;
        public static Chat Support;
        public static List<int> BananaUsers;
        public static List<int> SupportAdmins;
        public static List<int> BotAdmins;
        public static string Token;

        public static int DefaultPin;
        public static string DefaultWelcome;
        public static string IssuePin;
        public static string IssueWelcome;


        public const string Directory = "C:\\GreyWolfSupportBot";
        public const string Database = "Database.sqlite";
        public static readonly string connectionstring = $"Data Source={Directory}\\{Database};Version=3;";

        public static bool running = true;
        public static readonly DateTime starttime = DateTime.UtcNow;


        static void Main(string[] args)
        {
            if (!System.IO.Directory.Exists(Directory)) System.IO.Directory.CreateDirectory(Directory);
            if (!System.IO.File.Exists($"{Directory}\\{Database}")) SQL.FirstTime();

            Token = SQL.GetToken();
            Bot.Api = new TelegramBotClient(Token);
            Bot.Me = Bot.Api.GetMeAsync().Result;

            SQL.ReadConfig();
            BananaUsers = SQL.GetBananas();
            BotAdmins = SQL.GetBotAdmins();
            Support = Bot.Api.GetChatAsync(SupportId).Result;
            SupportAdmins = GetSupportAdmins();

            Bot.Api.OnMessage += Bot_OnMessage;
            Bot.Api.OnInlineQuery += Bot_OnInlineQuery;
            Bot.Api.StartReceiving();
            Bot.Send("Started Up!", Support.Id);
            Console.Write("Program running!" + Environment.NewLine + Environment.NewLine);

            while (running)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        public static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                Message msg = e.Message;

                if (!string.IsNullOrEmpty(msg.Text) && msg.Date.AddSeconds(-5) >= starttime)
                {
                    if (msg.Chat.Id == Support.Id)
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
                                var IssueMsg = Bot.Reply(IssuePin, Support.Id, DefaultPin);
                                var IssueSuccess = Bot.Pin(Support.Id, IssueMsg.MessageId);
                            }
                            else if (msg.Text == DefaultWelcome)
                            {
                                try
                                {
                                    var NormalSuccess = Bot.Pin(Support.Id, DefaultPin);
                                }
                                catch (AggregateException ex)
                                {
                                    if (ex.InnerExceptions.Any(x => x.Message.ToLower().Contains("chat_not_modified"))) return;
                                    throw ex;
                                }
                            }
                            else
                            {
                                switch (msg.Text.ToLower().Replace('@' + Bot.Me.Username.ToLower(), ""))
                                {
                                    case "/reloadadmins":
                                        SupportAdmins = GetSupportAdmins();
                                        Bot.Reply("Reloaded admins:\n\n" + string.Join("\n", SupportAdmins), msg);
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
                                            IssueWelcome = msg.ReplyToMessage.Text;
                                            SQL.RunNoResultQuery($"update config set issuewelc = '{IssueWelcome.Replace("'", "''")}'");
                                            Bot.Reply("Issue welcome set!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the issue welcome!", msg);
                                        break;

                                    case "/setwelcome":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            DefaultWelcome = msg.ReplyToMessage.Text;
                                            SQL.RunNoResultQuery($"update config set defaultwelc = '{DefaultWelcome.Replace("'", "''")}'");
                                            Bot.Reply("Welcome set!", msg);
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

                    if (BotAdmins.Contains(msg.From.Id))
                    {
                        switch (msg.Text.ToLower().Split(' ')[0])
                        {
                            case "/shutdown":
                                Bot.Reply("Shutting down.", msg);
                                running = false;
                                break;

                            case "/sql":
                                try
                                {
                                    var conn = new SQLiteConnection(connectionstring);

                                    string raw = "";

                                    string[] args = msg.Text.Contains(' ')
                                        ? new[] { msg.Text.Split(' ')[0], msg.Text.Remove(0, msg.Text.IndexOf(' ')) }
                                        : new[] { msg.Text, null };

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
                Bot.Send(error, BotAdmins[0]);
                return;
            }
        }

        public static void Bot_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            try
            {
                InlineQuery query = e.InlineQuery;

                if (SupportAdmins.Contains(query.From.Id))
                {
                    Bot.Api.AnswerInlineQueryAsync(query.Id, InlineResults.Admin).Wait();
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
                Bot.Send(error, BotAdmins[0]);
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
        }

        public static class SQL
        {
            public static void FirstTime()
            {
                string token = ""; // INSERT TOKEN HERE BEFORE FIRST USE. RUN PROGRAM, STOP, REMOVE TOKEN AGAIN.
                int owner = 0; // INSERT OWNER ID HERE BEFORE FIRST USE. RUN PROGRAM, STOP, REMOVE ID AGAIN.
                string support = ""; // INSERT SUPPORT ID HERE BEFORE FIRST USE. RUN PROGRAM, STOP, REMOVE ID AGAIN.

                if (string.IsNullOrEmpty(token) | owner == 0 | string.IsNullOrEmpty(support)) throw new NotImplementedException("You need to enter a token, owner ID and support ID before first use (see lines above)!");

                SQLiteConnection.CreateFile($"{Directory}\\{Database}");
                RunNoResultQuery("create table botadmins (id int primary key not null unique)");
                RunNoResultQuery("create table bananas (id int primary key not null unique)");
                RunNoResultQuery("create table config (token varchar(255), defaultpin int, defaultwelc varchar(255), issuepin varchar(255), issuewelc varchar(255), supportid varchar(255))");
                RunNoResultQuery($"insert into botadmins values ({owner})");
                RunNoResultQuery($"insert into config values ('{token}', 10, 'dummy welcome 1', 'dummy pin', 'dummy welcome 2', '{support}')");
            }

            public static void RunNoResultQuery(string query)
            {
                var conn = new SQLiteConnection(connectionstring);
                var comm = new SQLiteCommand(query, conn);
                conn.Open();
                comm.ExecuteNonQuery();
            }

            public static List<int> GetBotAdmins()
            {
                var query = "select id from botadmins";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var admins = new List<int>();
                while (reader.Read())
                {
                    admins.Add((int)reader[0]);
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
            }
        }

        public static List<int> GetSupportAdmins()
        {
            return Bot.Api.GetChatAdministratorsAsync(Support.Id).Result.Select(x => x.User.Id).ToList();
        }
    }
}
