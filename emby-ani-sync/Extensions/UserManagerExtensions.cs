#nullable enable
using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace emby_ani_sync.Extensions
{
    public static class UserManagerExtensions
    {
        /// <summary>
        /// Checks permissions to change user Ani-Sync configuration and returns said user.
        /// </summary>
        /// <param name="userManager">Instance of <see cref="IUserManager"/>.</param>
        /// <param name="currentUserId">The ID of the currently logged-in user.</param>
        /// <param name="userId">ID of the <see cref="User"/> the logged-in user would like to access. Null to access the logged-in user.</param>
        /// <returns>User if the requester has permissions to interact with the user. Null if the requester cannot interact with the user.</returns>
        public static User? GetUser(this IUserManager userManager, Guid currentUserId, Guid? userId)
        {
            if (currentUserId == Guid.Empty)
                return null;

            var currentUser = userManager.GetUserById(currentUserId);

            if (currentUser == null)
                return null;

            if (userId == null || userId == Guid.Empty)
                return currentUser;

            var isAdministrator = currentUser.Policy.IsAdministrator;

            if (!userId.Equals(currentUserId) && !isAdministrator)
                return null;

            return currentUser;
        }
    }
}
