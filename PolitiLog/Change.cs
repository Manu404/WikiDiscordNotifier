﻿using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace PolitiLog
{
    public class Change
    {
        public string User { get; private set; }
        public string Title { get; private set; }
        public string Comment { get; private set; }
        public DateTime Date { get; private set; }
        public string Type { get; private set; }
        public string Page { get; private set; }
        public int RevId { get; private set; }
        public int OldRevId { get; private set; }
        public int PageId { get; private set; }

        public Change(JToken data)
        {
            User = data.Value<string>("user");
            Title = data.Value<string>("title");
            Comment = data.Value<string>("comment");
            Page = data.Value<string>("page");
            Type = data.Value<string>("type");
            Date = data.Value<DateTime>("timestamp");
            RevId = data.Value<int>("revid");
            OldRevId = data.Value<int>("old_revid");
            PageId = data.Value<int>("pageid");
        }

        public bool IsNewUser()
        {
            return PageId == 0 && RevId == 0 && OldRevId == 0 
                    && String.Equals(Title, String.Format("Utilisateur:{0}", User)) 
                    && String.IsNullOrEmpty(Comment);
        }
    }
}
   
