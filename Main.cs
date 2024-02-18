using ILCodePatcher.ConfigLib;
using MelonLoader;

namespace ILCodePatcher
{
    class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ConfigManage.Init<Config>();
        }

        public override void OnApplicationQuit()
        {
            ConfigManage.SaveAll();
        }
    }
}
