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
}