using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Collections.Generic;
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
        public static readonly Chat Support = Bot.Api.GetChatAsync(/*-1001060486754*/-1001127004418).Result;
        public static List<int> BananaUsers = GetBananaUsers();
        public static List<int> SupportAdmins = GetSupportAdmins();

        public static Config conf;

        public static bool learning = false;
        public static List<int> temp;

        public static bool running = true;
        public static readonly DateTime starttime = DateTime.UtcNow;


        static void Main(string[] args)
        {
            Bot.Api.OnMessage += Bot_OnMessage;
            Bot.Api.OnInlineQuery += Bot_OnInlineQuery;
            ReadConfig();
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
                            WriteBananaUsers();
                            Bot.Reply($"#bananacount {BananaUsers.Count}! Thanks for reading the pinned message!", msg);
                        }
                        if (SupportAdmins.Contains(msg.From.Id))
                        {
                            if (msg.Text == conf.IssueWelcome)
                            {
                                var IssuePin = Bot.Reply(conf.IssuePinText, Support.Id, conf.PinmessageId);
                                var IssueSuccess = Bot.Pin(Support.Id, IssuePin.MessageId);
                            }
                            else if (msg.Text == conf.StandardWelcome)
                            {
                                try
                                {
                                    var NormalSuccess = Bot.Pin(Support.Id, conf.PinmessageId);
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
                                            conf.PinmessageId = msg.ReplyToMessage.MessageId;
                                            WriteConfig();
                                            Bot.Reply("Successfully set that message as pin message!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the pin message!", msg);
                                        break;

                                    case "/setissuewelcome":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            conf.IssueWelcome = msg.ReplyToMessage.Text;
                                            WriteConfig();
                                            Bot.Reply("Issue welcome set!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the issue welcome!", msg);
                                        break;

                                    case "/setwelcome":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            conf.StandardWelcome = msg.ReplyToMessage.Text;
                                            WriteConfig();
                                            Bot.Reply("Welcome set!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the welcome!", msg);
                                        break;

                                    case "/setissuepin":
                                        if (msg.ReplyToMessage != null && !string.IsNullOrEmpty(msg.ReplyToMessage.Text))
                                        {
                                            conf.IssuePinText = msg.ReplyToMessage.Text;
                                            WriteConfig();
                                            Bot.Reply("Issue pin message set!", msg);
                                        }
                                        else Bot.Reply("You need to reply to the issue pin message!", msg);
                                        break;
                                }
                            }
                        }
                    }
                    if (learning && msg.ForwardFrom != null && !temp.Contains(msg.ForwardFrom.Id)) temp.Add(msg.ForwardFrom.Id);

                    if (SupportAdmins.Contains(msg.From.Id))
                    {
                        switch (msg.Text.ToLower().Replace('@' + Bot.Me.Username.ToLower(), ""))
                        {
                            case "/startlearning":
                                learning = true;
                                temp = new List<int>();
                                Bot.Reply("Banana learning active.", msg);
                                break;

                            case "/finishlearning":
                                learning = false;
                                Bot.Reply(temp.Count + " bananas learnt.", msg);
                                foreach (var t in temp.Where(x => !BananaUsers.Contains(x))) BananaUsers.Add(t);
                                WriteBananaUsers();
                                temp = null;
                                break;
                        }
                    }

                    if (msg.From.Id == conf.ServerOwner)
                    {
                        switch (msg.Text.ToLower())
                        {
                            case "/shutdown":
                                Bot.Reply("Shutting down.", msg);
                                running = false;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                {
                    Console.Write(ex.Message + Environment.NewLine);
                    ex = ex.InnerException;
                }
                Console.Write(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
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
                while (ex.InnerException != null)
                {
                    Console.Write(ex.Message + Environment.NewLine);
                    ex = ex.InnerException;
                }
                Console.Write(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                return;
            }
        }


        static class Bot
        {
            public static ITelegramBotClient Api = new TelegramBotClient(Token);
            public static User Me = Api.GetMeAsync().Result;

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

            private static string Token
            {
                get
                {
                    ReadConfig();
                    return conf.BotToken;
                }
            }

        }

        static class InlineResults
        {
            public static readonly InlineQueryResultArticle[] NotAdmin = new[] { new InlineQueryResultArticle() { Id = "NotAdmin", Title = "You are not support admin!", InputMessageContent = new InputTextMessageContent() { MessageText = "Ooops! I just tried to use the support bot inline, but I am not a support admin!" } } };
            public static readonly InlineQueryResultArticle[] Admin = new[]
            {
                new InlineQueryResultArticle() { Id = "IssueWelcome", Title = "Issue Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = conf.IssueWelcome } },
                new InlineQueryResultArticle() { Id = "NormalWelcome", Title = "Standard Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = conf.StandardWelcome } },
            };
        }

        public class Config
        {
            public int ServerOwner { get; set; }
            public string IssuePinText { get; set; }
            public string IssueWelcome { get; set; }
            public string StandardWelcome { get; set; }
            public int PinmessageId { get; set; }
            public string BotToken { get; set; }
        }
        public static void WriteConfig()
        {
            System.IO.File.WriteAllText("config.txt", JsonConvert.SerializeObject(conf));
        }
        public static void ReadConfig()
        {
            conf = JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText("config.txt"));
        }

        public static List<int> GetBananaUsers()
        {
            var bu = JsonConvert.DeserializeObject<List<int>>(System.IO.File.ReadAllText("bananas.txt"));
            if (bu is null) return new List<int>();
            return bu;
        }

        public static void WriteBananaUsers()
        {
            System.IO.File.WriteAllText("bananas.txt", JsonConvert.SerializeObject(BananaUsers));
        }

        public static List<int> GetSupportAdmins()
        {
            return Bot.Api.GetChatAdministratorsAsync(Support.Id).Result.Select(x => x.User.Id).ToList();
        }
    }
}
