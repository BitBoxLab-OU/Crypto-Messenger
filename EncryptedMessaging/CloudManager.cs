using System;
using System.Collections.Generic;
using System.Text;

namespace EncryptedMessaging
{
    public interface ICloudManager
    {
        /// <summary>
        /// Save a data to the cloud in a set type, with a name key
        /// </summary>
        /// <param name="type">The group to which the data belongs</param>
        /// <param name="name">The unique key assigned to the object</param>
        /// <param name="data">An array of data to save </param>
        /// <param name="shared">If true, save the data in a common area among all contacts, otherwise they will be saved in a private area accessible only to the current user</param>
        void SaveDataOnCloud(string type, string name, byte[] data, bool shared = false);
        /// <summary>
        /// Sends a previously saved data request command, and if it exists an event will be generated OnDataLoad
        /// </summary>
        /// <param name="type">The group to which the data belongs</param>
        /// <param name="name">The unique key assigned to the object</param>
        /// <param name="ifSizeIsDifferent">Upload the data only if the size has changed (It is an empirical method to avoid creating communication traffic for data we already have, it would be more correct to use a hash, but this creates a computational load on the cloud)</param>
        /// <param name="shared">If true, load the data from a common area among all contacts, otherwise they will be load from a private area accessible only to the current user</param>
        void LoadDataFromCloud(string type, string name, int? ifSizeIsDifferent = null, bool shared = false);

        /// <summary>
        /// Upload from the cloud all the data saved in a specific type group. An event OnDataLoad will be generated for each data
        /// </summary>
        /// <param name="type">The group to which the data belongs</param>
        /// <param name="shared">If true, load the data from a common area among all contacts, otherwise they will be load from a private area accessible only to the current user</param>
        void LoadAllDataFromCloud(string type, bool shared = false);

        /// <summary>
        /// Delete a data that has been saved on the cloud
        /// </summary>
        /// <param name="type">The group to which the data belongs</param>
        /// <param name="name">The unique key assigned to the object</param>
        /// <param name="shared">If true, an object in the common area will be deleted, otherwise an object will be deleted from the private area accessible only to the current user</param>
        void DeleteDataOnCloud(string type, string name, bool shared = false);

        
        Contact Cloud { get; set; }

        void OnCommand(ushort command, byte[][] parameters);
       
        Action<ushort, byte[][]> SendCommand { set; get; }

        //void SendCommand(Action<  float , byte[][] >);

    }
}
