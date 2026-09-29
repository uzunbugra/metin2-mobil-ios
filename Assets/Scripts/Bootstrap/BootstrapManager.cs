using Metin2.Core.Config;
using Metin2.Core.Logging;
#if UNITY_5_3_OR_NEWER || UNITY_EDITOR
using UnityEngine;

namespace Metin2.Bootstrap
{
    public class BootstrapManager : MonoBehaviour
    {
        private ILogger _logger;
        public NetworkConfig Config { get; private set; }

        private void Awake()
        {
            _logger = new UnityLogger("Bootstrap");
            Config = NetworkConfig.CreateDefault(EnvironmentType.Local);
            _logger.LogInfo("Metin2 Mobile Client initialized successfully.");
        }
    }
}
#endif
