﻿using Discord;
using Discord.Webhook;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PolitiLog
{
    class ChangeNotifier
    {
        private SimpleLogger _logger;
        private string _webhookUrl;
        private string _wikiUrl;
        private int _queryLimit;

        public ChangeNotifier(SimpleLogger logger, string webhookUrl, string wikiUrl, int queryLimit)
        {
            _logger = logger;
            _webhookUrl = webhookUrl;
            _wikiUrl = wikiUrl;
            _queryLimit = queryLimit;
        }

        public DateTime SendRevisionSinceLastRevision(DateTime lastChange)
        {
            var changes = GetChangesFromApi(lastChange).ToList();

            SendToWebHook(BuildEmbeds(changes));

            _logger.AddLog(String.Format("{0} new changes to publish", changes.Count));

            // if no changes since last check, return
            if (changes.Count == 0)
                return lastChange;

            return changes.Last().Date;
        }

        private IEnumerable<Embed> BuildEmbeds(List<Change> changes)
        {
            List<Embed> embeds = new List<Embed>();

            // build embeds
            foreach (var change in changes)
                embeds.Add(CreateEmbed(change));

            // remove errors
            embeds.RemoveAll(o => o == null);

            return embeds;
        }

        private IEnumerable<Change> GetChangesFromApi(DateTime lastChange)
        {
            List<Change> revisions = new List<Change>();
            try
            {
                using (WebClient client = new WebClient())
                {
                    // Query api and parse json
                    string queryUrl = String.Format("{0}/w/api.php?action=query&list=recentchanges&rcprop=ids|title|user|comment|timestamp&rclimit={1}&format=json", _wikiUrl, _queryLimit);
                    string json = client.DownloadString(queryUrl);
                    JObject jsonObject = JObject.Parse(json);

                    // for each revisions, only keep edit, new and log type of revision
                    foreach (var change in jsonObject["query"]["recentchanges"])
                        if (change.Value<string>("type") == "edit" || change.Value<string>("type") == "new" || change.Value<string>("type") == "log")
                            if (change.Value<DateTime>("timestamp").ToUniversalTime() > lastChange.ToUniversalTime())
                                revisions.Add(new Change(change));

                    // sort by date
                    revisions.Sort((x, y) => DateTime.Compare(x.Date, y.Date));
                }
            }
            catch (Exception ex)
            {
                _logger.AddLog(ex.Message);
            }
            return revisions;
        }

        private Embed CreateEmbed(Change data)
        {
            try
            {
                if (data.IsNewUser())
                    return CreateNewUserEmbed(data);
                else if (data.IsFileUpload())
                    return CreateFileUploadEmbed(data);

                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(GetContributor(data));
                messageBuilder.AppendLine(GetPage(data));
                messageBuilder.AppendLine(GetComment(data));

                if (data.Type == "edit")
                    messageBuilder.AppendLine(GetRevDiff(data));

                messageBuilder.AppendLine(GetDate(data));

                var content = new EmbedFieldBuilder()
                        .WithName("Description")
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText("Merci pour ton travail !")
                        .WithIconUrl("https://emmanuelistace.be/politibot/heart.png");

                var builder = new EmbedBuilder()
                        .WithAuthor(BuildTitle(data))
                        .AddField(content)
                        .WithFooter(footer)
                        .WithColor(data.Type == "log" ? Color.LightOrange : Color.Green);

                return builder.Build();
            }
            catch (Exception ex)
            {
                _logger.AddLog(ex.Message);
                return null;
            }
        }


        private Embed CreateFileUploadEmbed(Change data)
        {
            try
            {
                var imageUrl = String.Empty;
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(GetContributor(data));
                messageBuilder.AppendLine(GetPage(data));
                messageBuilder.AppendLine(GetComment(data));
                messageBuilder.AppendLine(GetDate(data));

                var content = new EmbedFieldBuilder()
                        .WithName("Description")
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText("Merci pour ton travail !")
                        .WithIconUrl("https://emmanuelistace.be/politibot/heart.png");

                imageUrl = GetImageUrl(data);

                var builder = new EmbedBuilder()
                        .WithAuthor(BuildTitle(data))
                        .AddField(content)
                        .WithFooter(footer)
                        .WithImageUrl(imageUrl)
                        .WithColor(Color.Gold);

                return builder.Build();
            }
            catch (Exception ex)
            {
                _logger.AddLog(ex.Message);
                return null;
            }
        }
        private Embed CreateNewUserEmbed(Change data)
        {
            try
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Afin de nous aider le plus efficacement possible:");
                messageBuilder.AppendLine("• Tu peux consulter les règles du wiki [ici](https://politiwiki.fr/wiki/R%C3%A8gles)");
                messageBuilder.AppendLine("• Et tu peux consulter le guide de contribution [ici](https://politiwiki.fr/wiki/Guide_de_contribution)");
                messageBuilder.AppendLine("A bientôt !");

                var content = new EmbedFieldBuilder()
                        .WithName(String.Format("Bienvenue parmi nous {0} !", data.User))
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText("Merci de nous avoir rejoint !")
                        .WithIconUrl("https://emmanuelistace.be/politibot/heart.png");

                var builder = new EmbedBuilder()
                        .WithAuthor(BuildTitle(data))
                        .AddField(content)
                        .WithFooter(footer)
                        .WithColor(Color.Blue);

                return builder.Build();
            }
            catch (Exception ex)
            {
                _logger.AddLog(ex.Message);
                return null;
            }
        }

        private string GetComment(Change data)
        {
            return String.Format("**Commentaire**: {0}", String.IsNullOrEmpty(data.Comment) ? String.Empty : data.Comment);
        }

        private string GetContributor(Change data)
        {
            return String.Format("**Contributeur·ice**: [{0}](https://politiwiki.fr/wiki/Utilisateur:{1})", data.User, data.User.Replace(' ', '_'));
        }

        private string GetPage(Change data)
        {
            return String.Format("**Page**: [{0}]({1})", data.Title, GetWikiPageUrl(data));
        }

        private string GetDate(Change data)
        {
            return String.Format("**Date**: {0}", data.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"));
        }

        private string GetRevDiff(Change data)
        {
            return String.Format("**Modification**: [Consulter]({0})", BuildDiffUrl(data.Title, data.RevId, data.OldRevId));
        }

        private string GetImageUrl(Change data)
        {
            string imageUrl = String.Empty;
            using (WebClient client = new WebClient())
            {
                string pageContent = client.DownloadString(GetWikiPageUrl(data));
                Regex regex = new Regex(@"(fullImageLink).*?(href=)(.*?)(>)");
                Match match = regex.Match(pageContent);
                imageUrl = String.Format("{0}/{1}", _wikiUrl, match.Groups[3].Value.Replace("\"", " ").Trim());
            }
            return imageUrl;
        }

        private string GetWikiPageUrl(Change data)
        {
            return String.Format("{0}/wiki/{1}", _wikiUrl, data.Title.Replace(' ', '_'));
        }

        private EmbedAuthorBuilder BuildTitle(Change data)
        {
            if(data.IsNewUser())
                return new EmbedAuthorBuilder()
                    .WithName("Un·e nouveau·elle contributeur·ice nous à rejoint !")
                    .WithIconUrl("https://emmanuelistace.be/politibot/hand_wave.png");
            else if (data.Type == "edit")
                return new EmbedAuthorBuilder()
                    .WithName("Edition d'une page")
                    .WithIconUrl("https://emmanuelistace.be/politibot/pencil.png");
            else if (data.Type == "new")
                return new EmbedAuthorBuilder()
                    .WithName("Création d'une page")
                    .WithIconUrl("https://emmanuelistace.be/politibot/add.png");
            else if (data.Type == "log" && !data.IsFileUpload())
                return new EmbedAuthorBuilder()
                    .WithName("Action de maintenance")
                    .WithIconUrl("https://emmanuelistace.be/politibot/settings.png");
            else if (data.Type == "log" && data.IsFileUpload())
                return new EmbedAuthorBuilder()
                    .WithName("Nouveau fichier mis en ligne")
                    .WithIconUrl("https://emmanuelistace.be/politibot/photo.png");
            else
                return new EmbedAuthorBuilder();
        }

        private string BuildDiffUrl(string paneName, int diff, int oldid)
        {
            return String.Format("{0}/w/index.php?title={1}&diff={2}&oldid={3}", _wikiUrl, paneName.Replace(' ', '_'), diff, oldid);
        }

        private void SendToWebHook(IEnumerable<Embed> embedsList)
        {
            foreach (var embed in embedsList)
            {
                try
                {
                    var client = new DiscordWebhookClient(_webhookUrl);
                    client.SendMessageAsync(text: "Nouveau changement sur PolitiWiki !", embeds: new[] { embed }).Wait();
                }
                catch (Exception e){
                    _logger.AddLog(e.Message);
                }
            }
        }
    }
}
   
