using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RUKNBIM.ElementID
{
    public enum LicenseStatus
    {
        Valid,
        Expired,
        Disabled,
        NoConnectionNoCache,
        InvalidCredentials,
        Error
    }

    public class LicenseInfo
    {
        public LicenseStatus Status { get; set; }
        public string Message { get; set; }
        public string Email { get; set; }
        public string LicenseType { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public int DaysRemaining { get; set; }
    }

    public static class SupabaseLicensingClient
    {
        // CONFIGURATION: Supabase credentials.
        private const string SUPABASE_URL = "https://dfkcnyzuiquvozvncwph.supabase.co";
        private const string SUPABASE_ANON_KEY = "sb_publishable_zhW-Ox8_ssRAZKkGkBbsog_1juWTr1X";

        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RUKNBIM"
        );

        private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "license.dat");

        private static readonly HttpClient Client = new HttpClient();

        static SupabaseLicensingClient()
        {
            // Set security protocol for modern TLS support on .NET 4.8
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            
            // Set API Headers
            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.Add("apikey", SUPABASE_ANON_KEY);
        }

        /// <summary>
        /// Authenticates the user with Supabase Auth (GoTrue) and checks/creates their license info.
        /// </summary>
        public static async Task<LicenseInfo> LoginAsync(string email, string password)
        {
            try
            {
                // 1. Authenticate with Supabase GoTrue Auth API
                var authUrl = $"{SUPABASE_URL}/auth/v1/token?grant_type=password";
                var payload = $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var authResponse = await Client.PostAsync(authUrl, content);
                var authJson = await authResponse.Content.ReadAsStringAsync();

                if (!authResponse.IsSuccessStatusCode)
                {
                    string errorMsg = GetJsonValue(authJson, "error_description") ?? GetJsonValue(authJson, "error") ?? "Authentication failed";
                    return new LicenseInfo { Status = LicenseStatus.InvalidCredentials, Message = errorMsg };
                }

                var accessToken = GetJsonValue(authJson, "access_token");
                var userId = GetJsonValue(authJson, "id");
                
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId))
                {
                    // Supabase GoTrue sometimes nests user id inside the "user" object
                    userId = GetJsonValue(authJson, "user_id") ?? GetNestedJsonValue(authJson, "user", "id");
                }

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId))
                {
                    return new LicenseInfo { Status = LicenseStatus.Error, Message = "Failed to parse authentication response" };
                }

                // 2. Fetch or Create License in the custom `licenses` table via Supabase REST API
                var licenseInfo = await FetchOrCreateLicenseAsync(accessToken, userId, email);
                if (licenseInfo.Status == LicenseStatus.Valid)
                {
                    // Save to local cache with encryption
                    SaveCache(accessToken, userId, email, licenseInfo.LicenseType, 
                        licenseInfo.TrialEndDate ?? DateTime.UtcNow.AddDays(30), true);
                }

                return licenseInfo;
            }
            catch (Exception ex)
            {
                return new LicenseInfo { Status = LicenseStatus.Error, Message = $"Connection error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Validates current licensing status using cached token (with online verification if possible).
        /// </summary>
        public static async Task<LicenseInfo> ValidateLicenseAsync()
        {
            var cache = LoadCache();
            if (cache == null)
            {
                return new LicenseInfo { Status = LicenseStatus.NoConnectionNoCache, Message = "No local license found. Please login." };
            }

            // Check if online check is needed (more than 24 hours since last check)
            bool needsOnlineCheck = (DateTime.UtcNow - cache.LastValidationDate).TotalHours >= 24;

            if (needsOnlineCheck)
            {
                try
                {
                    // Attempt online check using the cached access token
                    var licenseInfo = await FetchLicenseAsync(cache.AccessToken, cache.UserId);
                    if (licenseInfo.Status == LicenseStatus.Valid)
                    {
                        // Update cache
                        SaveCache(cache.AccessToken, cache.UserId, cache.Email, licenseInfo.LicenseType, 
                            licenseInfo.TrialEndDate ?? DateTime.UtcNow.AddDays(30), true);
                        return licenseInfo;
                    }
                    else if (licenseInfo.Status == LicenseStatus.Expired || licenseInfo.Status == LicenseStatus.Disabled)
                    {
                        // Server explicitly invalidated license
                        DeleteCache();
                        return licenseInfo;
                    }
                }
                catch
                {
                    // Offline fallback - Internet connection failed during check
                }
            }

            // Check if within 7-day offline grace period
            bool withinGracePeriod = (DateTime.UtcNow - cache.LastValidationDate).TotalDays <= 7;
            if (!withinGracePeriod)
            {
                return new LicenseInfo { Status = LicenseStatus.NoConnectionNoCache, Message = "Internet connection required to validate license." };
            }

            // Check local cache dates
            if (DateTime.UtcNow > cache.TrialEndDate)
            {
                return new LicenseInfo
                {
                    Status = LicenseStatus.Expired,
                    Message = "Trial Expired",
                    Email = cache.Email,
                    LicenseType = cache.LicenseType,
                    TrialEndDate = cache.TrialEndDate,
                    DaysRemaining = 0
                };
            }

            int daysRemaining = (int)(cache.TrialEndDate - DateTime.UtcNow).TotalDays;
            return new LicenseInfo
            {
                Status = LicenseStatus.Valid,
                Email = cache.Email,
                LicenseType = cache.LicenseType,
                TrialEndDate = cache.TrialEndDate,
                DaysRemaining = daysRemaining > 0 ? daysRemaining : 0,
                Message = "Validated from local cache"
            };
        }

        private static async Task<LicenseInfo> FetchLicenseAsync(string token, string userId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{SUPABASE_URL}/rest/v1/licenses?user_id=eq.{userId}&select=*");
            request.Headers.Add("Authorization", $"Bearer {token}");

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new LicenseInfo { Status = LicenseStatus.Error, Message = "Failed to fetch license from server" };
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(json) || json == "[]")
            {
                return new LicenseInfo { Status = LicenseStatus.Disabled, Message = "No license found for user" };
            }

            return ParseLicenseJson(json);
        }

        private static async Task<LicenseInfo> FetchOrCreateLicenseAsync(string token, string userId, string email)
        {
            // 1. Try to fetch license
            var licenseInfo = await FetchLicenseAsync(token, userId);
            if (licenseInfo.Status == LicenseStatus.Valid || licenseInfo.Status == LicenseStatus.Expired)
            {
                return licenseInfo;
            }

            // 2. If no license found, create a new 30-day trial license
            var trialStart = DateTime.UtcNow;
            var trialEnd = trialStart.AddDays(30);

            var createUrl = $"{SUPABASE_URL}/rest/v1/licenses";
            var payload = $"{{\"user_id\":\"{userId}\",\"trial_start_date\":\"{trialStart:O}\",\"trial_end_date\":\"{trialEnd:O}\",\"last_validation\":\"{trialStart:O}\",\"machine_id\":\"{GetMachineId()}\"}}";
            
            var request = new HttpRequestMessage(HttpMethod.Post, createUrl);
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("Prefer", "return=representation");
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LicenseInfo { Status = LicenseStatus.Error, Message = "Failed to create license profile" };
            }

            return ParseLicenseJson(responseJson);
        }

        private static LicenseInfo ParseLicenseJson(string json)
        {
            // Clean brackets from array response
            if (json.StartsWith("[")) json = json.Substring(1);
            if (json.EndsWith("]")) json = json.Substring(0, json.Length - 1);

            var trialStartStr = GetJsonValue(json, "trial_start_date");
            var trialEndStr = GetJsonValue(json, "trial_end_date");
            
            DateTime trialEndDate = DateTime.TryParse(trialEndStr, out var d) ? d.ToUniversalTime() : DateTime.UtcNow;
            
            int daysRemaining = (int)(trialEndDate - DateTime.UtcNow).TotalDays;

            // Check if expired
            if (DateTime.UtcNow > trialEndDate)
            {
                return new LicenseInfo
                {
                    Status = LicenseStatus.Expired,
                    Message = "Trial Expired",
                    TrialEndDate = trialEndDate,
                    DaysRemaining = 0
                };
            }

            return new LicenseInfo
            {
                Status = LicenseStatus.Valid,
                LicenseType = "Trial",
                TrialEndDate = trialEndDate,
                DaysRemaining = daysRemaining > 0 ? daysRemaining : 0,
                Message = "License is valid"
            };
        }

        #region Local DPAPI Cache

        private class CachedLicense
        {
            public string AccessToken { get; set; }
            public string UserId { get; set; }
            public string Email { get; set; }
            public string LicenseType { get; set; }
            public DateTime TrialEndDate { get; set; }
            public DateTime LastValidationDate { get; set; }
        }

        private static void SaveCache(string token, string userId, string email, string licenseType, DateTime trialEnd, bool isActive)
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                // Create cache CSV string
                var dataString = $"{token}|{userId}|{email}|{licenseType}|{trialEnd:O}|{DateTime.UtcNow:O}";
                var rawBytes = Encoding.UTF8.GetBytes(dataString);
                
                // Encrypt using DPAPI
                var encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CacheFilePath, encryptedBytes);
            }
            catch { }
        }

        private static CachedLicense LoadCache()
        {
            try
            {
                if (!File.Exists(CacheFilePath)) return null;

                var encryptedBytes = File.ReadAllBytes(CacheFilePath);
                
                // Decrypt using DPAPI
                var rawBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var dataString = Encoding.UTF8.GetString(rawBytes);

                var parts = dataString.Split('|');
                if (parts.Length >= 6)
                {
                    return new CachedLicense
                    {
                        AccessToken = parts[0],
                        UserId = parts[1],
                        Email = parts[2],
                        LicenseType = parts[3],
                        TrialEndDate = DateTime.Parse(parts[4]).ToUniversalTime(),
                        LastValidationDate = DateTime.Parse(parts[5]).ToUniversalTime()
                    };
                }
            }
            catch { }
            return null;
        }

        public static void DeleteCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    File.Delete(CacheFilePath);
                }
            }
            catch { }
        }

        #endregion

        #region Helpers

        private static string GetJsonValue(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"?([^\",\\}}]+)\"?");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim().Trim('"');
            }
            return null;
        }

        private static string GetNestedJsonValue(string json, string parentKey, string childKey)
        {
            // Simple extraction of a parent block then child key
            var parentMatch = Regex.Match(json, $"\"{parentKey}\"\\s*:\\s*\\{{([^\\}}]+)\\}}");
            if (parentMatch.Success)
            {
                return GetJsonValue(parentMatch.Groups[1].Value, childKey);
            }
            return null;
        }

        private static string GetMachineId()
        {
            try
            {
                // Simple unique hardware string combining machine name and user
                return Environment.MachineName + "\\" + Environment.UserName;
            }
            catch
            {
                return "UnknownMachine";
            }
        }

        #endregion
    }
}
