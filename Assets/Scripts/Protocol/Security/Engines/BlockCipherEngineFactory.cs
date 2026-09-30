using System;
using Metin2.Protocol.Exceptions;

namespace Metin2.Protocol.Security.Engines
{
    /// <summary>
    /// Creates block-cipher engines for ported suites. Unported suites throw
    /// <see cref="CipherEngineNotImplementedException"/> naming the suite —
    /// never a silent fallback. Checklist: docs/sprints/SPRINT_03-cipher-engines.md.
    /// No UnityEngine dependency.
    /// </summary>
    public static class BlockCipherEngineFactory
    {
        public static bool IsSupported(CipherSuite suite)
        {
            return suite == CipherSuite.TEA
                || suite == CipherSuite.RC6
                || suite == CipherSuite.IDEA
                || suite == CipherSuite.RC5
                || suite == CipherSuite.SHACAL2
                || suite == CipherSuite.Blowfish
                || suite == CipherSuite.TripleDES
                || suite == CipherSuite.Twofish
                || suite == CipherSuite.Serpent
                || suite == CipherSuite.MARS
                || suite == CipherSuite.CAST256
                || suite == CipherSuite.Camellia;
        }

        public static IBlockCipherEngine Create(CipherSuite suite, byte[] key)
        {
            switch (suite)
            {
                case CipherSuite.TEA:
                    return new TeaEngine(key);
                case CipherSuite.RC6:
                    return new Rc6Engine(key);
                case CipherSuite.IDEA:
                    return new IdeaEngine(key);
                case CipherSuite.RC5:
                    return new Rc5Engine(key);
                case CipherSuite.SHACAL2:
                    return new Shacal2Engine(key);
                case CipherSuite.Blowfish:
                    return new BlowfishEngine(key);
                case CipherSuite.TripleDES:
                    return new TripleDesEngine(key);
                case CipherSuite.Twofish:
                    return new TwofishEngine(key);
                case CipherSuite.Serpent:
                    return new SerpentEngine(key);
                case CipherSuite.MARS:
                    return new MarsEngine(key);
                case CipherSuite.CAST256:
                    return new Cast256Engine(key);
                case CipherSuite.Camellia:
                    return new CamelliaEngine(key);
                default:
                    throw new CipherEngineNotImplementedException(CipherSuiteTable.GetName(suite));
            }
        }

        /// <summary>
        /// Adapter matching the CipherSession engine-factory signature. Keys are bound
        /// per direction (not per suite), so equal suites on both directions still work.
        /// </summary>
        public static Func<CipherDirectionMaterial, IBlockCipherEngine> ForSession()
        {
            return direction => Create(direction.Suite, direction.Key);
        }
    }
}
