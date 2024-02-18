using ILCodePatcher.ConfigLib;
using MelonLoader;
using System;

namespace ILCodePatcher
{
    public static class Util
    {
        internal static Config Config
        {
            get
            {
                return ConfigManage.GetDefault();
            }
        }

        public static void Debug(object obj)
        {
            if (!Config.Debug)
                return;

            Msg(obj);
        }

        public static void Debug(string txt)
        {
            if (!Config.Debug)
                return;

            Msg(txt);
        }

        public static void Debug(string txt, params object[] args)
        {
            if (!Config.Debug)
                return;

            Msg(txt, args);
        }

        public static void DebugInfo(object obj)
        {
            if (!Config.Debug)
                return;

            Info(obj);
        }

        public static void DebugInfo(string txt)
        {
            if (!Config.Debug)
                return;

            Info(txt);
        }

        public static void DebugInfo(string txt, params object[] args)
        {
            if (!Config.Debug)
                return;

            Info(txt, args);
        }

        public static void Msg(object obj)
        {
            MelonLogger.Msg(obj);
        }

        public static void Msg(string txt)
        {
            MelonLogger.Msg(txt);
        }

        public static void Msg(string txt, params object[] args)
        {
            MelonLogger.Msg(string.Format(txt, args));
        }

        public static void Error(string txt, Exception e = null)
        {
            if (e != null)
                MelonLogger.Error($"{txt}\n{e}");
            else
                MelonLogger.Error(txt);
        }

        public static void Info(object obj)
        {
            MelonLogger.Msg(obj);
        }

        public static void Info(string txt)
        {
            MelonLogger.Msg(txt);
        }

        public static void Info(string txt, params object[] args)
        {
            MelonLogger.Msg(string.Format(txt, args));
        }
    }
}
