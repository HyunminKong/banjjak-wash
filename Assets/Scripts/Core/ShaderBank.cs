using UnityEngine;

namespace Banjjak
{
    /// <summary>Keeps references to every shader the game creates materials from, so builds include them.</summary>
    public class ShaderBank : ScriptableObject
    {
        public Shader dirtLit;
        public Shader foliage;
        public Shader particles;
        public Shader skybox;
        public Shader unlit;

        static ShaderBank inst;
        public static ShaderBank I
        {
            get
            {
                if (inst == null) inst = Resources.Load<ShaderBank>("GameShaders");
                return inst;
            }
        }
    }

    public static class Layers
    {
        public const int Default = 0;
        public const int Rig = 8;
        public const int SprayOnly = 9;
        public const int Player = 10;
        public const int PlayerOnly = 11;
        public const int SprayMask = (1 << Default) | (1 << SprayOnly);
    }
}
