using System;
using NUnit.Framework;
using Metin2.Core.Logging;

namespace Metin2.Tests.EditMode.Core
{
    [TestFixture]
    public class SecretRedactorTests
    {
        [Test]
        public void Redact_KeyValuePassword_IsMasked()
        {
            string input = "User login attempt: username=player1, password=SuperSecretPassword123!";
            string redacted = SecretRedactor.Redact(input);

            Assert.IsFalse(redacted.Contains("SuperSecretPassword123!"));
            Assert.IsTrue(redacted.Contains("password=[REDACTED]"));
        }

        [Test]
        public void Redact_PasswdColon_IsMasked()
        {
            string input = "Auth credentials: login=admin, passwd: secret_password_value";
            string redacted = SecretRedactor.Redact(input);

            Assert.IsFalse(redacted.Contains("secret_password_value"));
            Assert.IsTrue(redacted.Contains("passwd:[REDACTED]"));
        }

        [Test]
        public void Redact_JsonCredentials_IsMasked()
        {
            string input = "{\"username\": \"testuser\", \"password\": \"my_secret_token\"}";
            string redacted = SecretRedactor.Redact(input);

            Assert.IsFalse(redacted.Contains("my_secret_token"));
            Assert.IsTrue(redacted.Contains("\"password\": \"[REDACTED]\""));
        }

        [Test]
        public void Redact_BearerToken_IsMasked()
        {
            string input = "Authorization header: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            string redacted = SecretRedactor.Redact(input);

            Assert.IsFalse(redacted.Contains("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"));
            Assert.AreEqual("Authorization header: Bearer [REDACTED]", redacted);
        }

        [Test]
        public void Redact_LoginKey_IsMasked()
        {
            string input = "Received auth success: dwLoginKey=123456789, bResult=1";
            string redacted = SecretRedactor.Redact(input);

            Assert.IsFalse(redacted.Contains("123456789"));
            Assert.IsTrue(redacted.Contains("dwLoginKey=[REDACTED]"));
        }

        [Test]
        public void Redact_SafeMessage_RemainsUnchanged()
        {
            string safeMsg = "Connected to auth server at 127.0.0.1:11002. Phase shift: Handshake -> Auth";
            string result = SecretRedactor.Redact(safeMsg);

            Assert.AreEqual(safeMsg, result);
        }

        [Test]
        public void Mask_HidesCharactersWithAsterisks()
        {
            string masked = SecretRedactor.Mask("secretpassword", 2, 2);
            Assert.AreEqual("se**********rd", masked);

            string fullMask = SecretRedactor.Mask("hidden", 0, 0);
            Assert.AreEqual("******", fullMask);
        }

        [Test]
        public void UnityLogger_LogError_RedactsSensitiveDataInsideException()
        {
            // Arrange: Exception containing credentials in its message
            var inner = new ArgumentException("Inner error: token=Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
            var outer = new InvalidOperationException("Failed auth with password=SuperSecretExceptionPassword and dwLoginKey=987654321", inner);

            // Act
            string formatted = UnityLogger.FormatLogMessage("AuthService", "Connection attempt aborted", outer);

            // Assert: No secrets leaked in formatted log message
            Assert.IsFalse(formatted.Contains("SuperSecretExceptionPassword"), "Exception password must be redacted.");
            Assert.IsFalse(formatted.Contains("987654321"), "Exception dwLoginKey must be redacted.");
            Assert.IsFalse(formatted.Contains("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"), "Exception bearer token must be redacted.");

            Assert.IsTrue(formatted.Contains("password=[REDACTED]"));
            Assert.IsTrue(formatted.Contains("dwLoginKey=[REDACTED]"));
            Assert.IsTrue(formatted.Contains("Bearer [REDACTED]"));
        }
    }
}
