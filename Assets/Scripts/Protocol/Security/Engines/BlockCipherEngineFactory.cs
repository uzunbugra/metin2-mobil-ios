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
                || suite == CipherSuite.SHACAL2;
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
