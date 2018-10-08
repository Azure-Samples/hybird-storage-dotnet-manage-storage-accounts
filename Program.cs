namespace StorageAccount
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Profile2018ResourceManager = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.ResourceManager;
    using Profile2018Storage = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.Storage;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure.Authentication;
    using Newtonsoft.Json.Linq;

    class Program
    {
        private const string ComponentName = "DotnetSDKStorageManagementSample";

        static void runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint)
        {
            var resourceGroupName = SdkContext.RandomResourceName("rgDotnetSdk", 24);
            var storageAccountName = SdkContext.RandomResourceName("storageaccount", 18);
            var storageAccount2Name = SdkContext.RandomResourceName("storageaccount", 18);
            Console.WriteLine("Get credential token");
            var adSettings = getActiveDirectoryServiceSettings(armEndpoint);
            var credentials = ApplicationTokenProvider.LoginSilentAsync(tenantId, servicePrincipalId, servicePrincipalSecret, adSettings).GetAwaiter().GetResult();

            Console.WriteLine("Instantiate resource management client");
            var rmClient = GetResourceManagementClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate storage account client");
            var storageClient = GetStorageClient(new Uri(armEndpoint), credentials, subscriptionId);

            // Create resource group.
            try
            {
                Console.WriteLine(String.Format("Creating a resource group with name:{0}", resourceGroupName));
                var rmCreateTask = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    new Profile2018ResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    });
                rmCreateTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroupName, ex.Message));
            }

            // Create storage account.
            try
            {
                Console.WriteLine(String.Format("Creating a storage account with name:{0}", storageAccountName));
                var storageProperties = new Profile2018Storage.Models.StorageAccountCreateParameters
                {
                    Location = location,
                    Kind = Profile2018Storage.Models.Kind.Storage,
                    Sku = new Profile2018Storage.Models.Sku(Profile2018Storage.Models.SkuName.StandardLRS)
                };

                var storageTask = storageClient.StorageAccounts.CreateWithHttpMessagesAsync(resourceGroupName, storageAccountName, storageProperties);
                storageTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccountName, ex.Message));
            }

            // Get | regenerate storage account access keys.
            try
            {
                Console.WriteLine("Getting storage account access keys");
                var storageAccountKeysTask = storageClient.StorageAccounts.ListKeysWithHttpMessagesAsync(resourceGroupName, storageAccountName);
                storageAccountKeysTask.Wait();
                var storageAccountKeysResults = storageAccountKeysTask.Result?.Body?.Keys;

                foreach (var key in storageAccountKeysResults)
                {
                    Console.WriteLine(String.Format("Storage account key name: {0}, key value: {1}", key.KeyName, key.Value));
                }

                Console.WriteLine("Regenerating first storage account access key");
                var storageAccountRegenerateTask = storageClient.StorageAccounts.RegenerateKeyWithHttpMessagesAsync(
                    resourceGroupName,
                    storageAccountName,
                    new Profile2018Storage.Models.StorageAccountRegenerateKeyParameters
                    {
                        KeyName = storageAccountKeysResults[0].KeyName
                    });
                storageAccountRegenerateTask.Wait();
                var storageAccountRegenerateResults = storageAccountRegenerateTask.Result?.Body?.Keys;
                foreach (var key in storageAccountRegenerateResults)
                {
                    Console.WriteLine(String.Format("Storage account key name: {0}, key value: {1}", key.KeyName, key.Value));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccountName, ex.Message));
            }

            // Create another storage account.
            try
            {
                Console.WriteLine(String.Format("Creating a storage account with name: {0}", storageAccount2Name));
                var storageProperties = new Profile2018Storage.Models.StorageAccountCreateParameters
                {
                    Location = location,
                    Kind = Profile2018Storage.Models.Kind.Storage,
                    Sku = new Profile2018Storage.Models.Sku(Profile2018Storage.Models.SkuName.StandardLRS)
                };

                var storageTask = storageClient.StorageAccounts.CreateWithHttpMessagesAsync(resourceGroupName, storageAccount2Name, storageProperties);
                storageTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccount2Name, ex.Message));
            }

            // Update storage account by enabling encryption.
            try
            {
                Console.WriteLine(String.Format("Enabling blob encryption for the storage account: {0}", storageAccount2Name));
                var storageAccountUpdateTask = storageClient.StorageAccounts.UpdateWithHttpMessagesAsync(resourceGroupName, storageAccount2Name, new Profile2018Storage.Models.StorageAccountUpdateParameters
                {
                    Encryption = new Profile2018Storage.Models.Encryption(new Profile2018Storage.Models.EncryptionServices
                    {
                        Blob = new Profile2018Storage.Models.EncryptionService()
                    })
                });

                storageAccountUpdateTask.Wait();
                var status = storageAccountUpdateTask.Result?.Body?.Encryption?.Services?.Blob?.Enabled.Value;
                if (status.HasValue && status.Value)
                {
                    Console.WriteLine(String.Format("Encryption status of the service  {0} is enabled", storageAccount2Name));
                }
                else
                {
                    Console.WriteLine(String.Format("Encryption status of the service  {0} is not enabled", storageAccount2Name));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not enable blob encryption for storage account {0}. Exception: {1}", storageAccount2Name, ex.Message));
            }

            // List storage accounts.
            var storageAccountResults = new List<Profile2018Storage.Models.StorageAccount>();
            try
            {
                Console.WriteLine("Listing storage accounts");
                var storageAccountListTask = storageClient.StorageAccounts.ListByResourceGroupWithHttpMessagesAsync(resourceGroupName);
                storageAccountListTask.Wait();
                storageAccountResults = storageAccountListTask.Result?.Body.ToList();

                foreach (var storageAccount in storageAccountResults)
                {
                    Console.WriteLine(String.Format("Storage account name: {0}, created at: {1}", storageAccount.Name, storageAccount.CreationTime.ToString()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not list storage accounts. Exception: {0}", ex.Message));
            }

            // Delete storage accounts.
            try
            {
                foreach (var storageAccount in storageAccountResults)
                {
                    Console.WriteLine(String.Format("Deleting a storage account with name: {0}", storageAccount.Name));

                    var storageDeleteTask = storageClient.StorageAccounts.DeleteWithHttpMessagesAsync(resourceGroupName, storageAccount.Name);
                    storageDeleteTask.Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete storage accounts. Exception: {0}", ex.Message));
            }
        }

        static ActiveDirectoryServiceSettings getActiveDirectoryServiceSettings(string armEndpoint)
        {
            var settings = new ActiveDirectoryServiceSettings();

            try
            {
                var request = (HttpWebRequest)HttpWebRequest.Create(string.Format("{0}/metadata/endpoints?api-version=1.0", armEndpoint));
                request.Method = "GET";
                request.UserAgent = ComponentName;
                request.Accept = "application/xml";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        var rawResponse = sr.ReadToEnd();
                        var deserialized = JObject.Parse(rawResponse);
                        var authenticationObj = deserialized.GetValue("authentication").Value<JObject>();
                        var loginEndpoint = authenticationObj.GetValue("loginEndpoint").Value<string>();
                        var audiencesObj = authenticationObj.GetValue("audiences").Value<JArray>();

                        settings.AuthenticationEndpoint = new Uri(loginEndpoint);
                        settings.TokenAudience = new Uri(audiencesObj[0].Value<string>());
                        settings.ValidateAuthority = loginEndpoint.TrimEnd('/').EndsWith("/adfs", StringComparison.OrdinalIgnoreCase) ? false : true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not get AD service settings. Exception: {0}", ex.Message));
            }
            return settings;
        }

        static void Main(string[] args)
        {
            //Set variables
            var location = Environment.GetEnvironmentVariable("RESOURCE_LOCATION");
            var baseUriString = Environment.GetEnvironmentVariable("ARM_ENDPOINT");
            var servicePrincipalId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var servicePrincipalSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");

            runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, baseUriString);
        }

        private static Profile2018Storage.StorageManagementClient GetStorageClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018Storage.StorageManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }

        private static Profile2018ResourceManager.ResourceManagementClient GetResourceManagementClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018ResourceManager.ResourceManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }
    }
}
