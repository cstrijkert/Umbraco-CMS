// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Extensions
{
    /// <summary>
    /// Extension methods for the IPublicAccessService
    /// </summary>
    public static class PublicAccessServiceExtensions
    {
        public static bool RenameMemberGroupRoleRules(this IPublicAccessService publicAccessService, string? oldRolename, string? newRolename)
        {
            var hasChange = false;
            if (oldRolename == newRolename) return false;

            var allEntries = publicAccessService.GetAll();

            foreach (var entry in allEntries)
            {
                //get rules that match
                var roleRules = entry.Rules
                    .Where(x => x.RuleType == Constants.Conventions.PublicAccess.MemberRoleRuleType)
                    .Where(x => x.RuleValue == oldRolename);
                var save = false;
                foreach (var roleRule in roleRules)
                {
                    //a rule is being updated so flag this entry to be saved
                    roleRule.RuleValue = newRolename ?? String.Empty;
                    save = true;
                }
                if (save)
                {
                    hasChange = true;
                    publicAccessService.Save(entry);
                }
            }

            return hasChange;
        }

        public static bool HasAccess(this IPublicAccessService publicAccessService, int documentId, IContentService contentService, string username, IEnumerable<string> currentMemberRoles)
        {
            var content = contentService.GetById(documentId);
            if (content == null) return true;

            var entry = publicAccessService.GetEntryForContent(content);
            if (entry == null) return true;

            return HasAccess(entry, username, currentMemberRoles);
        }

        /// <summary>
        /// Checks if the member with the specified username has access to the path which is also based on the passed in roles for the member
        /// </summary>
        /// <param name="publicAccessService"></param>
        /// <param name="path"></param>
        /// <param name="username"></param>
        /// <param name="rolesCallback">A callback to retrieve the roles for this member</param>
        /// <returns></returns>
        public static async Task<bool> HasAccessAsync(this IPublicAccessService publicAccessService, string path, string username, Func<Task<IEnumerable<string>>> rolesCallback)
        {
            if (rolesCallback == null) throw new ArgumentNullException("roles");
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Value cannot be null or whitespace.", "username");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Value cannot be null or whitespace.", "path");

            var entry = publicAccessService.GetEntryForContent(path.EnsureEndsWith(path));
            if (entry == null) return true;

            var roles = await rolesCallback();

            return HasAccess(entry, username, roles);
        }

        private static bool HasAccess(PublicAccessEntry entry, string username, IEnumerable<string> roles)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException($"'{nameof(username)}' cannot be null or empty.", nameof(username));
            }

            if (roles is null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            return entry.Rules.Any(x =>
                (x.RuleType == Constants.Conventions.PublicAccess.MemberUsernameRuleType && username.Equals(x.RuleValue, StringComparison.OrdinalIgnoreCase))
                || (x.RuleType == Constants.Conventions.PublicAccess.MemberRoleRuleType && roles.Contains(x.RuleValue))
            );
        }
    }
}
