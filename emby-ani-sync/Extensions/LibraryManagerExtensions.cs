#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace emby_ani_sync.Extensions
{
    public static class LibraryManagerExtensions
    {
        /// <summary>
        /// Checks if the user has access to the list of <see cref="CollectionFolder"/>.
        /// </summary>
        /// <returns>True if the user has access to the list of <see cref="CollectionFolder"/>, false if not.</returns>
        public static bool UserHasAccessToLibraries(
            this ILibraryManager libraryManager,
            HashSet<string> libraryIds,
            User user
        )
        {
            Dictionary<string, string> librariesUserHasAccessTo = libraryManager.GetLibrariesUserHasAccessTo(user);
            return libraryIds.All(id => librariesUserHasAccessTo.ContainsKey(id));
        }

        /// <summary>
        /// Retrieves the ID and name of the <see cref="CollectionFolder"/>s the <see cref="User"/> has access to.
        /// </summary>
        public static Dictionary<string, string> GetLibrariesUserHasAccessTo(
            this ILibraryManager libraryManager,
            User user
        )
        {
            Dictionary<string, string> virtualFolderInfos = new Dictionary<string, string>();
            bool userHasAccessToAllLibraries = user.Policy.EnableAllFolders;
            if (userHasAccessToAllLibraries)
            {
                return libraryManager
                    .GetVirtualFolders()
                    .ToDictionary(
                        virtualFolderInfo => virtualFolderInfo.ItemId,
                        virtualFolderInfo => virtualFolderInfo.Name
                    );
            }

            string[] enabledFolders = user.Policy.EnabledFolders;
            if (enabledFolders == null || enabledFolders.Length == 0)
            {
                if (!userHasAccessToAllLibraries)
                    return virtualFolderInfos;
                return libraryManager
                    .GetVirtualFolders()
                    .ToDictionary(
                        virtualFolderInfo => virtualFolderInfo.ItemId,
                        virtualFolderInfo => virtualFolderInfo.Name
                    );
            }

            foreach (string folderId in enabledFolders)
            {
                var matchingFolder = libraryManager.GetVirtualFolders().FirstOrDefault(vf => vf.ItemId == folderId);
                if (matchingFolder != null)
                {
                    virtualFolderInfos[matchingFolder.ItemId] = matchingFolder.Name;
                }
            }

            return virtualFolderInfos;
        }
    }
}
