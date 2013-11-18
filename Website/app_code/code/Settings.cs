using System;
using System.Configuration;

public static class Settings
{
    private static string _useBlobStorage = "";

    public static bool UseBlobStorage
    {
        get
        {
            if (String.IsNullOrWhiteSpace(_useBlobStorage))
            {
                _useBlobStorage = ConfigurationManager.AppSettings.Get("storage:useBlobStorage");
            }

            return _useBlobStorage == "true";
        }
    }

    private static string _blobStorageConnectionString = "";

    public static string BlobStorageConnectionString
    {
        get
        {
            if (String.IsNullOrWhiteSpace(_blobStorageConnectionString))
            {
                _blobStorageConnectionString = ConfigurationManager.AppSettings.Get("storage:blobStorageConnectionString");
            }

            return _blobStorageConnectionString;
        }
    }
}