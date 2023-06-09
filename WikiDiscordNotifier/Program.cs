﻿using CommandLine;
using System;

namespace WikiDiscordNotifier
{
    partial class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Parser.Default.ParseArguments<CommandLineOptions>(args)
                    .WithParsed<CommandLineOptions>(option =>
                    {
                        try
                        {
                            SimpleLogger logger = new SimpleLogger(option.Silent);
                            logger.AddLog("-----------------");
                            logger.AddLog("Start application");

                            FileHelper fileHelper = new FileHelper(logger, "./last_change", option.LogFile);

                            var localization = fileHelper.Loadl10n(option.Language);

                            DateTime lastChangeDate = fileHelper.GetLastChangeDateTime().ToUniversalTime();

                            logger.AddLog(String.Format("Last change dates from {0}", lastChangeDate.ToString("o")));

                            ChangeNotifier changeNotfier = new ChangeNotifier(logger, option.WebHook, option.Domain, option.Api, option.Wiki, option.Limit, localization);

                            DateTime newestDate = changeNotfier.SendRevisionSinceLastRevision(lastChangeDate);

                            logger.AddLog("Stop Application");

                            fileHelper.SaveNewDate(newestDate.ToUniversalTime());

                            if (!option.Silent)
                                fileHelper.SaveLogs();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    });
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
   
