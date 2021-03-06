﻿using System;
using System.IO;

using Newtonsoft.Json;

namespace ACE.Common
{
    public struct ConfigServerNetwork
    {
        public string Host { get; set; }

        public uint Port { get; set; }
    }

    public struct ConfigAccountDefaults
    {
        public bool OverrideCharacterPermissions { get; set; }

        public uint DefaultAccessLevel { get; set; }
    }

    public struct ConfigServer
    {
        public string WorldName { get; set; }

        public string Welcome { get; set; }

        public ConfigServerNetwork Network { get; set; }

        public ConfigAccountDefaults Accounts { get; set; }

        public string DatFilesDirectory { get; set; }
    }

    public struct ConfigMySqlDatabase
    {
        public string Host { get; set; }

        public uint Port { get; set; }

        public string Database { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }
    }

    public struct ConfigMySql
    {
        public ConfigMySqlDatabase Authentication { get; set; }

        public ConfigMySqlDatabase Character { get; set; }

        public ConfigMySqlDatabase World { get; set; }
    }

    public struct Config
    {
        public ConfigServer Server { get; set; }

        public ConfigMySql MySql { get; set; }
    }

    public static class ConfigManager
    {
        public static Config Config { get; private set; }

        public static void Initialise()
        {
            try
            {
                Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"Config.json"));
            }
            catch (Exception exception)
            {
                Console.WriteLine("An exception occured while loading the configuration file!");
                Console.WriteLine($"Exception: {exception.Message}");

                // environment.exit swallows this exception for testing purposes.  we want to expose it.
                throw;
            }
        }
    }
}
