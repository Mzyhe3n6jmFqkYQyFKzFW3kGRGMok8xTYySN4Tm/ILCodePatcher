using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace ILCodePatcher.ConfigLib
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ConfigInfo : Attribute
    {
        public string ConfigPath { get; internal set; }

        public ConfigInfo(string configPath)
        {
            ConfigPath = configPath;
        }
    }

    public static class ConfigManage
    {
        internal static Dictionary<Type, object> configs = new();

        internal class ConfigClass<T>
        {
            internal T config;
            private Type configType;
            private ConfigInfo configInfo;

            internal ConfigClass()
            {
                configType = typeof(T);
                configInfo = configType.GetCustomAttribute<ConfigInfo>();

                if (configInfo == null)
                    throw new ArgumentNullException();

                config = Load();

                if (config == null)
                {
                    config = Activator.CreateInstance<T>();
                    Save(config);
                }

                if (!configs.ContainsKey(configType))
                {
                    configs.Add(configType, config);
                }
            }

            private T Load()
            {
                T config;

                if (configs.ContainsKey(configType))
                    return (T)configs[configType];
                else
                {
                    if (!File.Exists(configInfo.ConfigPath))
                        return default;

                    using (FileStream stream = File.OpenRead(configInfo.ConfigPath))
                        config = (T)new XmlSerializer(typeof(T)).Deserialize(stream);
                }

                return config;
            }

            internal void Save(T config)
            {
                if (File.Exists(configInfo.ConfigPath))
                    File.Delete(configInfo.ConfigPath);

                using (FileStream stream = new FileStream(configInfo.ConfigPath, FileMode.CreateNew))
                    new XmlSerializer(typeof(T)).Serialize(stream, config);
            }
        }

        public static T Get<T>()
        {
            var manage = new ConfigClass<T>();

            return manage.config;
        }

        public static Config GetDefault()
        {
            var manage = new ConfigClass<Config>();

            return manage.config;
        }

        public static void Init<T>()
        {
            new ConfigClass<T>();
        }

        public static void Save<T>()
        {
            var type = typeof(T);

            if (!configs.ContainsKey(type))
                return;

            var configInfo = type.GetCustomAttribute<ConfigInfo>();

            if (File.Exists(configInfo.ConfigPath))
                File.Delete(configInfo.ConfigPath);

            using (FileStream stream = new FileStream(configInfo.ConfigPath, FileMode.CreateNew))
                new XmlSerializer(type).Serialize(stream, configs[type]);
        }

        public static void SaveAll()
        {
            foreach (var config in configs)
            {
                var configInfo = config.Key.GetCustomAttribute<ConfigInfo>();

                MelonLogger.Msg($"Config Saved: {configInfo.ConfigPath}");

                if (File.Exists(configInfo.ConfigPath))
                    File.Delete(configInfo.ConfigPath);

                using (FileStream stream = new FileStream(configInfo.ConfigPath, FileMode.CreateNew))
                    new XmlSerializer(config.Key).Serialize(stream, config.Value);
            }
        }
    }
}
