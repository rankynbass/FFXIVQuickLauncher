﻿using AdysTech.CredentialManager;
using Newtonsoft.Json;
using Serilog;
using System;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XIVLauncher.Accounts
{
    public class XivAccount
    {
        private const string CREDS_PREFIX_OLD = "FINAL FANTASY XIV";
        private const string CREDS_PREFIX_NEW = "XIVLAUNCHER";

        [JsonIgnore]
        public string Id => $"{UserName}-{UseOtp}-{UseSteamServiceAccount}";

        public override string ToString() => Id;

        public string UserName { get; private set; }

        [JsonIgnore]
        public string Password
        {
            get
            {
                var credentials = CredentialManager.GetCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");

                if (credentials != null)
                {
                    var saved = CredentialManager.SaveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}", new NetworkCredential
                    {
                        UserName = credentials.UserName,
                        Password = credentials.Password,
                    });

                    if (saved)
                    {
                        try
                        {
                            CredentialManager.RemoveCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");
                        }
                        catch (Win32Exception)
                        {
                            // ignored
                        }
                    }
                }
                else
                {
                    credentials = CredentialManager.GetCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}");
                }

                return credentials != null ? credentials.Password : string.Empty;
            }
            set
            {
                try
                {
                    CredentialManager.RemoveCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");
                }
                catch (Win32Exception)
                {
                    // ignored
                }

                try
                {
                    CredentialManager.RemoveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}");
                }
                catch (Win32Exception)
                {
                    // ignored
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    CredentialManager.SaveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}", new NetworkCredential
                    {
                        UserName = UserName,
                        Password = value
                    });
                }
            }
        }

        public bool SavePassword { get; set; }
        public bool UseSteamServiceAccount { get; set; }
        public bool UseOtp { get; set; }

        public string ChosenCharacterName;
        public string ChosenCharacterWorld;

        public string ThumbnailUrl;

        public string LastSuccessfulOtp;

        public XivAccount(string userName)
        {
            UserName = userName.ToLower();
        }

        public string FindCharacterThumb()
        {
            if (string.IsNullOrEmpty(ChosenCharacterName) || string.IsNullOrEmpty(ChosenCharacterWorld))
                return null;

            try
            {
                dynamic searchResponse = GetCharacterSearch(ChosenCharacterName, ChosenCharacterWorld)
                .GetAwaiter().GetResult();

                if (searchResponse.Results.Count > 1) //If we get more than one match from XIVAPI
                {
                    foreach (var accountInfo in searchResponse.Results)
                    {
                        //We have to check with it all lower in case they type their character name LiKe ThIsLoL. The server XIVAPI returns also contains the DC name, so let's just do a contains on the server to make it easy.
                        if (accountInfo.Name.Value.ToLower() == ChosenCharacterName.ToLower() && accountInfo.Server.Value.ToLower().Contains(ChosenCharacterWorld.ToLower()))
                        {
                            return accountInfo.Avatar.Value;
                        }
                    }
                }

                return searchResponse.Results.Count > 0 ? (string)searchResponse.Results[0].Avatar : null;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Couldn't download character search.");

                return null;
            }
        }

        private const string URL = "https://xivapi.com/";

        public static async Task<JObject> GetCharacterSearch(string name, string world)
        {
            return await Get("character/search" + $"?name={name}&server={world}");
        }

        public static async Task<dynamic> Get(string endpoint)
        {
            using var client = new WebClient();

            var result = await client.DownloadStringTaskAsync(URL + endpoint);

            var parsedObject = JObject.Parse(result);

            return parsedObject;
        }
    }
}
