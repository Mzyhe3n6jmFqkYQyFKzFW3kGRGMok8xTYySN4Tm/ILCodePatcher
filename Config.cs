using ILCodePatcher.ConfigLib;
using System;

namespace ILCodePatcher
{
    [Serializable]
    [ConfigInfo("UserData/ILCodePatcher.xml")]
    public class Config
    {
#if DEBUG
        public bool Debug { get; set; } = true;
#else
        public bool Debug { get; set; } = false;
#endif
        public bool PatcherILDebug { get; set; } = false;
    }
}
