using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RUKNBIM.SmartSelect
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
        // =====================================================================
        private const string SupabaseUrl = "https://dfkcnyzuiquvozvncwph.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_zhW-Ox8_ssRAZKkGkBbsog_1juWTr1X";
        // =====================================================================

        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RUKNBIM"
        );

        private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "license.dat");

        private static readonly string TrialFilePath = Path.Combine(CacheDirectory, "trial.dat");

        private static readonly HttpClient Client = new HttpClient();

        static SupabaseLicensingClient()
        {
            // Set security protocol for modern TLS support on .NET 4.8
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            
            // Set API Headers
            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        }

        /// <summary>
        /// Validates user email and activation code directly against Supabase database.
        /// </summary>
        public static async Task<LicenseInfo> LoginAsync(string email, string password)
        {
            try
            {
                var licenseInfo = await FetchLicenseOnlineAsync(email, password);
                if (licenseInfo.Status == LicenseStatus.Valid)
                {
                    // Save to local cache with encryption
                    SaveCache(password, email, email, licenseInfo.LicenseType,
                        licenseInfo.TrialEndDate ?? DateTime.UtcNow.AddDays(3650), true);
                }

                return licenseInfo;
            }
            catch (Exception ex)
            {
                return new LicenseInfo { Status = LicenseStatus.Error, Message = $"Connection error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Quick GET to check if Supabase is reachable.
        /// </summary>
        public static async Task<bool> PingAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, SupabaseUrl.TrimEnd('/') + "/rest/v1/");
                request.Headers.Add("apikey", SupabaseAnonKey);
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var response = await Client.SendAsync(request, cts.Token);
                    return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest;
                }
            }
            catch
            {
                return false;
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
                return await ValidateServerTrialAsync();
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
                            licenseInfo.TrialEndDate ?? DateTime.UtcNow.AddDays(3650), true);
                        return licenseInfo;
                    }
                    else if (licenseInfo.Status == LicenseStatus.Expired || 
                             licenseInfo.Status == LicenseStatus.Disabled || 
                             licenseInfo.Status == LicenseStatus.InvalidCredentials)
                    {
                        // Server explicitly invalidated license (expired, disabled, or revoked/not found)
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
            if (cache.TrialEndDate != DateTime.MinValue && DateTime.UtcNow > cache.TrialEndDate)
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

            int daysRemaining = cache.TrialEndDate == DateTime.MinValue ? 9999 : (int)(cache.TrialEndDate - DateTime.UtcNow).TotalDays;
            return new LicenseInfo
            {
                Status = LicenseStatus.Valid,
                Email = cache.Email,
                LicenseType = cache.LicenseType,
                TrialEndDate = cache.TrialEndDate == DateTime.MinValue ? (DateTime?)null : cache.TrialEndDate,
                DaysRemaining = daysRemaining > 0 ? daysRemaining : 0,
                Message = "Validated from local cache"
            };
        }

        private static async Task<LicenseInfo> FetchLicenseOnlineAsync(string email, string activationCode)
        {
            var cleanEmail = email.Trim().ToLowerInvariant();
            var cleanCode = activationCode.Trim();
            var machineId = GetMachineId();

            // Licenses table is locked down (no direct REST access); lookup + machine binding
            // happens inside the verify_license SECURITY DEFINER RPC.
            var requestUrl = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/rpc/verify_license";

            var payload = "{" +
                $"\"p_email\":\"{JsonEscape(cleanEmail)}\"," +
                $"\"p_code\":\"{JsonEscape(cleanCode)}\"," +
                $"\"p_machine_id\":\"{JsonEscape(machineId)}\"" +
            "}";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {SupabaseAnonKey}");

            var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorDetails = await response.Content.ReadAsStringAsync();
                return new LicenseInfo { Status = LicenseStatus.Error, Message = $"Failed to fetch license: {response.StatusCode} {errorDetails}" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var statusStr = GetJsonValue(json, "status");

            if (statusStr == "not_found")
            {
                return new LicenseInfo { Status = LicenseStatus.InvalidCredentials, Message = "Invalid email or activation code." };
            }
            if (statusStr == "machine_mismatch")
            {
                return new LicenseInfo { Status = LicenseStatus.Disabled, Message = "This license is already activated on a different machine." };
            }

            return ParseLicenseJson(json);
        }

        private static async Task<LicenseInfo> FetchLicenseAsync(string token, string userId)
        {
            // token represents the activation code, and userId represents the email
            return await FetchLicenseOnlineAsync(userId, token);
        }

        private static string JsonEscape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static LicenseInfo ParseLicenseJson(string json)
        {
            var expireDateStr = GetJsonValue(json, "expire_date");
            var createdAtStr = GetJsonValue(json, "created_at");
            var trialDaysStr = GetJsonValue(json, "trial_days");
            var productStr = GetJsonValue(json, "product");
            var emailStr = GetJsonValue(json, "email");

            DateTime? expiryDate = null;
            if (!string.IsNullOrEmpty(expireDateStr))
            {
                if (DateTime.TryParse(expireDateStr, out var exp))
                {
                    expiryDate = exp.ToUniversalTime();
                }
            }
            else if (!string.IsNullOrEmpty(trialDaysStr) && int.TryParse(trialDaysStr, out var trialDays))
            {
                if (DateTime.TryParse(createdAtStr, out var created))
                {
                    expiryDate = created.ToUniversalTime().AddDays(trialDays);
                }
            }

            string licenseType = string.IsNullOrEmpty(productStr) ? "Trial" : productStr;

            if (expiryDate.HasValue)
            {
                int daysRemaining = (int)Math.Ceiling((expiryDate.Value - DateTime.UtcNow).TotalDays);
                if (DateTime.UtcNow > expiryDate.Value)
                {
                    return new LicenseInfo
                    {
                        Status = LicenseStatus.Expired,
                        Email = emailStr,
                        LicenseType = licenseType,
                        TrialEndDate = expiryDate,
                        DaysRemaining = 0,
                        Message = "License has expired"
                    };
                }
                
                return new LicenseInfo
                {
                    Status = LicenseStatus.Valid,
                    Email = emailStr,
                    LicenseType = licenseType,
                    TrialEndDate = expiryDate,
                    DaysRemaining = daysRemaining > 0 ? daysRemaining : 0,
                    Message = "License is valid"
                };
            }
            else
            {
                // No expiry date means a lifetime / perpetual license
                return new LicenseInfo
                {
                    Status = LicenseStatus.Valid,
                    Email = emailStr,
                    LicenseType = licenseType,
                    TrialEndDate = null,
                    DaysRemaining = 9999,
                    Message = "License is valid (Lifetime)"
                };
            }
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
                if (File.Exists(TrialFilePath))
                {
                    File.Delete(TrialFilePath);
                }
            }
            catch { }
        }

        /// <summary>
        /// Starts (or resumes) the trial for this machine against the server. Safe to call
        /// repeatedly - the server only ever creates one trial row per machine_id.
        /// </summary>
        public static Task<LicenseInfo> StartTrialAsync()
        {
            return ValidateServerTrialAsync();
        }

        /// <summary>
        /// Caches the server-confirmed trial window locally, so the app has an offline
        /// fast-path. The server (public.trials, keyed by machine_id) remains the source
        /// of truth — this cache alone can never grant a trial.
        /// </summary>
        private static void CacheLocalTrial(DateTime startDate, DateTime endDate)
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                var dataString = $"{startDate:O}|{endDate:O}";
                var rawBytes = Encoding.UTF8.GetBytes(dataString);
                var encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(TrialFilePath, encryptedBytes);
            }
            catch { }
        }

        private static bool TryLoadCachedTrial(out DateTime startDate, out DateTime endDate)
        {
            startDate = DateTime.UtcNow;
            endDate = DateTime.UtcNow;
            try
            {
                if (!File.Exists(TrialFilePath)) return false;

                var encryptedBytes = File.ReadAllBytes(TrialFilePath);
                var rawBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var dataString = Encoding.UTF8.GetString(rawBytes);
                var parts = dataString.Split('|');
                if (parts.Length == 2 && DateTime.TryParse(parts[0], out startDate) && DateTime.TryParse(parts[1], out endDate))
                {
                    startDate = startDate.ToUniversalTime();
                    endDate = endDate.ToUniversalTime();
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Resolves trial eligibility against the server (public.trials, keyed by machine_id).
        /// Deleting the local cache cannot grant a new trial: a new machine_id row is only ever
        /// created once per machine, and the server's start/end dates are always authoritative.
        /// </summary>
        private static async Task<LicenseInfo> ValidateServerTrialAsync()
        {
            var machineId = GetMachineId();

            try
            {
                var existing = await FetchServerTrialAsync(machineId);
                if (existing.HasValue)
                {
                    var (start, end) = existing.Value;
                    CacheLocalTrial(start, end);
                    return BuildTrialResult(start, end);
                }

                // No trial on record for this machine yet — create one.
                var newStart = DateTime.UtcNow.Date;
                var newEnd = newStart.AddDays(7);
                bool created = await CreateServerTrialAsync(machineId, newStart, newEnd);
                if (created)
                {
                    CacheLocalTrial(newStart, newEnd);
                    return BuildTrialResult(newStart, newEnd);
                }

                // Insert failed (e.g. row already exists from a race) - re-fetch to get the real record.
                var refetched = await FetchServerTrialAsync(machineId);
                if (refetched.HasValue)
                {
                    var (start, end) = refetched.Value;
                    CacheLocalTrial(start, end);
                    return BuildTrialResult(start, end);
                }

                return new LicenseInfo { Status = LicenseStatus.Error, Message = "Unable to start trial. Please try again." };
            }
            catch
            {
                // Offline: fall back to the last server-confirmed window if we have one cached.
                if (TryLoadCachedTrial(out var start, out var end))
                {
                    return BuildTrialResult(start, end);
                }

                return new LicenseInfo { Status = LicenseStatus.NoConnectionNoCache, Message = "Internet connection required to start or verify your trial." };
            }
        }

        private static LicenseInfo BuildTrialResult(DateTime startDate, DateTime endDate)
        {
            double remaining = (endDate.Date - DateTime.UtcNow.Date).TotalDays;
            if (remaining > 0)
            {
                return new LicenseInfo
                {
                    Status = LicenseStatus.Valid,
                    Email = "Trial User",
                    LicenseType = "Trial",
                    TrialEndDate = endDate,
                    DaysRemaining = (int)Math.Ceiling(remaining),
                    Message = "Valid Trial"
                };
            }

            return new LicenseInfo
            {
                Status = LicenseStatus.Expired,
                Email = "Trial User",
                LicenseType = "Trial",
                TrialEndDate = endDate,
                DaysRemaining = 0,
                Message = "Trial Expired"
            };
        }

        private static async Task<(DateTime start, DateTime end)?> FetchServerTrialAsync(string machineId)
        {
            var encodedId = Uri.EscapeDataString(machineId);
            var requestUrl = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/trials?machine_id=eq.{encodedId}&select=start_date,end_date";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {SupabaseAnonKey}");

            var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(json) || json.Trim() == "[]") return null;

            var startStr = GetJsonValue(json, "start_date");
            var endStr = GetJsonValue(json, "end_date");
            if (DateTime.TryParse(startStr, out var start) && DateTime.TryParse(endStr, out var end))
            {
                return (start.ToUniversalTime(), end.ToUniversalTime());
            }
            return null;
        }

        private static async Task<bool> CreateServerTrialAsync(string machineId, DateTime start, DateTime end)
        {
            var requestUrl = $"{SupabaseUrl.TrimEnd('/')}/rest/v1/trials";
            var payload = "{" +
                $"\"machine_id\":\"{JsonEscape(machineId)}\"," +
                $"\"start_date\":\"{start:yyyy-MM-dd}\"," +
                $"\"end_date\":\"{end:yyyy-MM-dd}\"" +
            "}";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {SupabaseAnonKey}");
            request.Headers.Add("Prefer", "return=minimal");

            var response = await Client.SendAsync(request);
            // A 409 Conflict means another process already created the row for this machine_id (primary key clash).
            return response.IsSuccessStatusCode;
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

        private static string _cachedMachineId;

        /// <summary>
        /// Derives a stable hardware fingerprint (CPU ID + motherboard serial + disk serial +
        /// Windows MachineGuid), hashed with SHA-256. Unlike MachineName/UserName, this survives
        /// a Windows username change or app reinstall, making the trial harder to reset.
        /// </summary>
        private static string GetMachineId()
        {
            if (_cachedMachineId != null) return _cachedMachineId;

            try
            {
                var raw = GetWmiProperty("Win32_Processor", "ProcessorId")
                    + "|" + GetWmiProperty("Win32_BaseBoard", "SerialNumber")
                    + "|" + GetWmiProperty("Win32_DiskDrive", "SerialNumber")
                    + "|" + GetMachineGuid();

                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                    _cachedMachineId = BitConverter.ToString(hash).Replace("-", "");
                }
            }
            catch
            {
                // WMI unavailable (locked-down environment) - fall back to MachineGuid alone.
                try
                {
                    using (var sha = SHA256.Create())
                    {
                        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(GetMachineGuid()));
                        _cachedMachineId = BitConverter.ToString(hash).Replace("-", "");
                    }
                }
                catch
                {
                    _cachedMachineId = "UnknownMachine";
                }
            }

            return _cachedMachineId;
        }

        private static string GetWmiProperty(string wmiClass, string property)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        var value = obj[property]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetMachineGuid()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    return key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion
    }
}
