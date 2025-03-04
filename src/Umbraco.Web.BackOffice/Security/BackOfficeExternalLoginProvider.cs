using System;
using Microsoft.Extensions.Options;

namespace Umbraco.Cms.Web.BackOffice.Security
{
    /// <summary>
    /// An external login (OAuth) provider for the back office
    /// </summary>
    public class BackOfficeExternalLoginProvider : IEquatable<BackOfficeExternalLoginProvider>
    {
        public BackOfficeExternalLoginProvider(
            string authenticationType,
            IOptionsMonitor<BackOfficeExternalLoginProviderOptions> properties)
        {
            if (properties is null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            AuthenticationType = authenticationType ?? throw new ArgumentNullException(nameof(authenticationType));
            Options = properties.Get(authenticationType);
        }

        /// <summary>
        /// The authentication "Scheme"
        /// </summary>
        public string AuthenticationType { get; }

        public BackOfficeExternalLoginProviderOptions Options { get; }

        public override bool Equals(object? obj) => Equals(obj as BackOfficeExternalLoginProvider);
        public bool Equals(BackOfficeExternalLoginProvider? other) => other != null && AuthenticationType == other.AuthenticationType;
        public override int GetHashCode() => HashCode.Combine(AuthenticationType);
    }

}
