using System;

namespace Metin2.Core.Config
{
    /// <summary>
    /// Configuration model for server endpoints and connection parameters.
    /// Endpoints are verified against survey reports (Auth Port default: 11002).
    /// </summary>
    public class NetworkConfig
    {
        public const int DefaultAuthPort = 11002;
        public const int DefaultGamePort = 13000;
        public const int DefaultTimeoutMs = 5000;

        public EnvironmentType Environment { get; set; } = EnvironmentType.Local;
        public string AuthHost { get; set; } = "127.0.0.1";
        public int AuthPort { get; set; } = DefaultAuthPort;
        public string GameHost { get; set; } = "127.0.0.1";
        public int GamePort { get; set; } = DefaultGamePort;
        public int ConnectTimeoutMs { get; set; } = DefaultTimeoutMs;
        public int ReadTimeoutMs { get; set; } = DefaultTimeoutMs;

        public static NetworkConfig CreateDefault(EnvironmentType env = EnvironmentType.Local)
        {
            return env switch
            {
                EnvironmentType.Local => new NetworkConfig
                {
                    Environment = EnvironmentType.Local,
                    AuthHost = "127.0.0.1",
                    AuthPort = DefaultAuthPort,
                    GameHost = "127.0.0.1",
                    GamePort = DefaultGamePort
                },
                EnvironmentType.Dev => new NetworkConfig
                {
                    Environment = EnvironmentType.Dev,
                    AuthHost = "10.0.2.2", // Android emulator host loopback
                    AuthPort = DefaultAuthPort,
                    GameHost = "10.0.2.2",
                    GamePort = DefaultGamePort
                },
                EnvironmentType.Staging => new NetworkConfig
                {
                    Environment = EnvironmentType.Staging,
                    AuthHost = "staging.metin2mobile.local",
                    AuthPort = DefaultAuthPort,
                    GameHost = "staging.metin2mobile.local",
                    GamePort = DefaultGamePort
                },
                EnvironmentType.Production => new NetworkConfig
                {
                    Environment = EnvironmentType.Production,
                    AuthHost = "auth.metin2mobile.live",
                    AuthPort = DefaultAuthPort,
                    GameHost = "game.metin2mobile.live",
                    GamePort = DefaultGamePort
                },
                _ => throw new ArgumentOutOfRangeException(nameof(env), env, null)
            };
        }
    }
}
