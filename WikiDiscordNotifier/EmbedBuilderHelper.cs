﻿using Discord;
using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WikiDiscordNotifier
{
    class EmbedBuilderHelper
    {
        private SimpleLogger _logger;
        private l18n _localization;
        private string _wiki;
        private string _domain;

        public EmbedBuilderHelper(string domain, string wiki, SimpleLogger logger, l18n localization)
        {
            _logger = logger;
            _wiki = wiki;
            _domain = domain;   
            _localization = localization;
        }

        public Embed CreateEmbed(Change data)
        {
            try
            {
                if (data.IsNewUser())
                    return CreateNewUserEmbed(data);
                else if (data.IsFileUpload())
                    return CreateFileUploadEmbed(data);
                else
                    return CreateStandardEmbed(data);
            }
            catch (Exception ex)
            {
                _logger.AddLog(ex.Message);
                return null;
            }
        }

        private Embed CreateStandardEmbed(Change data)
        {
            try
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(GetContributor(data));
                messageBuilder.AppendLine(GetPage(data));
                messageBuilder.AppendLine(GetComment(data));

                if (data.Type == "edit")
                    messageBuilder.AppendLine(GetRevDiff(data));

                messageBuilder.AppendLine(GetDate(data));

                var content = new EmbedFieldBuilder()
                        .WithName(_localization.DefaulContentTitle)
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText(_localization.DefaultFooter)
                        .WithIconUrl(_localization.DefaultFooterLogoUrl);

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
                        .WithName(_localization.FileUploadContentTitle)
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText(_localization.FileUploadFooter)
                        .WithIconUrl(_localization.FileUploadFooterLogoUrl);

                imageUrl = HandleMultipleDash(GetImageUrl(data));

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
                messageBuilder.AppendLine(_localization.NewUserWelcomeMessage);

                var content = new EmbedFieldBuilder()
                        .WithName(String.Format(_localization.NewUserWelcomeMessageContentTitle + " " + data.User + " !"))
                        .WithValue(messageBuilder.ToString())
                        .WithIsInline(false);

                var footer = new EmbedFooterBuilder()
                        .WithText(_localization.NewUserWelcomeMessageFooter)
                        .WithIconUrl(_localization.NewUserWelcomeMessageEmbedLogoUrl);

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
        
        private EmbedAuthorBuilder BuildTitle(Change data)
        {
            if (data.IsNewUser())
                return new EmbedAuthorBuilder()
                    .WithName(_localization.NewUserTitle)
                    .WithIconUrl(_localization.NewUserWelcomeMessageEmbedLogoUrl);
            else if (data.Type == "edit")
                return new EmbedAuthorBuilder()
                    .WithName(_localization.EditTitle)
                    .WithIconUrl(_localization.EditPageEmbedLogoUrl);
            else if (data.Type == "new")
                return new EmbedAuthorBuilder()
                    .WithName(_localization.CreatePageTitle)
                    .WithIconUrl(_localization.NewUserWelcomeMessageEmbedLogoUrl);
            else if (data.Type == "log" && !data.IsFileUpload())
                return new EmbedAuthorBuilder()
                    .WithName(_localization.MaintenanceActionTitle)
                    .WithIconUrl(_localization.MaintenanceActionEmbedLogoUrl);
            else if (data.Type == "log" && data.IsFileUpload())
                return new EmbedAuthorBuilder()
                    .WithName(_localization.FileUploadTitle)
                    .WithIconUrl(_localization.FileUploadEmbedLogoUrl);
            else
                return new EmbedAuthorBuilder();
        }

        private string GetComment(Change data) { return $"**{_localization.Commentary}**: {(String.IsNullOrEmpty(data.Comment) ? String.Empty : data.Comment)}"; }

        private string GetContributor(Change data) { return $"**{_localization.Contributor}**: [{data.User}]({HandleMultipleDash(GetUserPage(data))})"; }

        private string GetPage(Change data) { return $"**{_localization.Page}**: [{data.Title}]({HandleMultipleDash(GetWikiPageUrl(data))})"; }

        private string GetDate(Change data) { return $"**{_localization.Date}**: {data.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")}"; }

        private string GetRevDiff(Change data) { return $"**{_localization.Modification}**: [{_localization.Consult}]({HandleMultipleDash(BuildDiffUrl(data.Title, data.RevId, data.OldRevId))})"; }
        
        private string GetImageUrl(Change data)
        {
            string imageUrl = String.Empty;
            using (WebClient client = new WebClient())
            {
                string pageContent = client.DownloadString(GetWikiPageUrl(data));
                Regex regex = new Regex(@"(fullImageLink).*?(href=)(.*?)(>)");
                Match match = regex.Match(pageContent);
                imageUrl = $"{_domain}/{match.Groups[3].Value.Replace("\"", " ").Trim()}";
            }
            return imageUrl;
        }

        public string GetUserPage(Change data) => $"{_domain}/{_wiki}/User:{data.User.Replace(' ', '_')}";

        private string GetWikiPageUrl(Change data) => $"{_domain}/{_wiki}/{data.Title.Replace(' ', '_')}";

        private string BuildDiffUrl(string paneName, int diff, int oldid) => $"{_domain}/{_wiki}/index.php?title={paneName.Replace(' ', '_')}&diff={diff}&oldid={oldid}";

        private string HandleMultipleDash(string url)
        {
            // replace multiple dash
            string result = String.Empty;
            bool previousWasSlash = false;
            foreach(var c in url)
            {
                if(c == '/' && !previousWasSlash)
                {
                    result += c;
                    previousWasSlash = true;
                }
                else if (c != '/')
                {
                    previousWasSlash = false;
                    result += c;
                }
            }

            return result.Replace("https:/", "https://").Replace("http:/", "http://");
        }
        
    }
}
   
