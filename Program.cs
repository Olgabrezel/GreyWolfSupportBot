using Newtonsoft.Json;
using System;
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
        public static string IssuePinText = "<b>We are having issues!</b>";
        public static string IssueWelcome = "/welcome <b>$name</b> (ID: <code>$id</code>)! We are having issues!";
        public static string StandardWelcome = "/welcome <b>$name</b> (ID: <code>$id</code>)! There are no issues right now!";
        public static int PinmessageId = 6;
        


        static void Main(string[] args)
        {
            Bot.Api.OnMessage += Bot_OnMessage;
            Bot.Api.OnInlineQuery += Bot_OnInlineQuery;
            Bot.Api.StartReceiving();
            Bot.Send("Started Up!", Support.Id);

            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        public static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                Message msg = e.Message;

                if (msg.Chat.Id == Support.Id)
                {
                    if (msg.Text.ToLower().StartsWith("banana") && !BananaUsers.Contains(msg.From.Id))
                    {
                        BananaUsers.Add(msg.From.Id);
                        WriteBananaUsers();
                        Bot.Reply($"#bananacount {BananaUsers.Count}! Thanks for reading the pinned message!", msg);
                    }
                    if (SupportAdmins.Contains(msg.From.Id))
                    {
                        if (msg.Text == IssueWelcome)
                        {
                            var IssuePin = Bot.Reply(IssuePinText, Support.Id, PinmessageId);
                            var IssueSuccess = Bot.Pin(Support.Id, IssuePin.MessageId);
                        }
                        else if (msg.Text == StandardWelcome)
                        {
                            var NormalSuccess = Bot.Pin(Support.Id, PinmessageId);
                        }
                        else
                        {
                            switch (msg.Text.ToLower())
                            {
                                case "/reloadadmins":
                                    SupportAdmins = GetSupportAdmins();
                                    Bot.Reply("Reloaded admins:\n\n" + string.Join("\n", SupportAdmins), msg);
                                    break;
                            }
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
                    var t = Api.SendTextMessageAsync(chatid, text, parseMode, disableWebPagePreview, replyMarkup: replyMarkup);
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
                    return System.IO.File.ReadAllText("token.txt");
                }
            }
        }

        static class InlineResults
        {
            public static readonly InlineQueryResultArticle[] NotAdmin = new[] { new InlineQueryResultArticle() { Id = "NotAdmin", Title = "You are not support admin!", InputMessageContent = new InputTextMessageContent() { MessageText = "Ooops! I just tried to use the support bot inline, but I am not a support admin!" } } };
            public static readonly InlineQueryResultArticle[] Admin = new[]
            {
                new InlineQueryResultArticle() { Id = "IssueWelcome", Title = "Issue Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = IssueWelcome } },
                new InlineQueryResultArticle() { Id = "NormalWelcome", Title = "Standard Welcome & Pin", InputMessageContent = new InputTextMessageContent() { MessageText = StandardWelcome } },
            };
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
