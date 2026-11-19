using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Banjjak
{
    /// <summary>
    /// Public Supabase project values. Both are meant to ship inside the game: the publishable/anon key only
    /// allows what the database's Row Level Security policies allow (each player reads and writes their own rows).
    /// Never put a secret / service_role key here.
    /// </summary>
    public static class CloudConfig
    {
        public const string Url = "https://hvaterqjhjirjfsijrpn.supabase.co";
        public const string Key = "sb_publishable_JSsvPEB0AHduddRoPcEDmw_PYWxPF6G";
        public static bool Enabled => Url.Length > 0 && Key.Length > 0;
        public const string EmailDomain = "@players.banjjak.game";
    }

    /// <summary>Very small username + password account on Supabase Auth, with cloud saves over its REST API.</summary>
    public class Account
    {
        [Serializable] class AuthUser { public string id; }
        [Serializable] class AuthResp { public string access_token; public string refresh_token; public int expires_in; public AuthUser user; public string error; public string error_description; public string msg; public string error_code; public string message; }
        [Serializable] class JobRow { public string job_id; public string data; }
        [Serializable] class JobRows { public JobRow[] rows; }

        const string RefreshPref = "banjjak-refresh";
        const string NamePref = "banjjak-username";

        public string UserId { get; private set; }
        public string Username { get; private set; }
        public bool LoggedIn => !string.IsNullOrEmpty(access);
        public bool Busy;

        string access, refresh;
        float expiresAt;
        public readonly Dictionary<string, string> JobCache = new Dictionary<string, string>();   // job id -> base64 progress

        public static bool ValidName(string n) => !string.IsNullOrEmpty(n) && n.Length >= 3 && n.Length <= 20 && System.Text.RegularExpressions.Regex.IsMatch(n, "^[a-zA-Z0-9_]+$");

        // ------------------------------------------------------------------ http
        UnityWebRequest Req(string method, string path, string json, string token)
        {
            var r = new UnityWebRequest(CloudConfig.Url.TrimEnd('/') + path, method);
            if (json != null) r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("apikey", CloudConfig.Key);
            r.SetRequestHeader("Content-Type", "application/json");
            if (token != null) r.SetRequestHeader("Authorization", "Bearer " + token);
            r.timeout = 20;
            return r;
        }

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string Friendly(UnityWebRequest r, AuthResp a)
        {
            string raw = ((a != null ? (a.error_code + " " + a.msg + " " + a.error_description + " " + a.message) : "") + " " + r.downloadHandler?.text).ToLowerInvariant();
            if (r.result == UnityWebRequest.Result.ConnectionError) return "인터넷 연결을 확인해 주세요";
            if (raw.Contains("already registered") || raw.Contains("user_already_exists") || raw.Contains("already been registered")) return "이미 있는 아이디예요";
            if (raw.Contains("invalid login") || raw.Contains("invalid_credentials")) return "아이디나 비밀번호가 맞지 않아요";
            if (raw.Contains("password") && (raw.Contains("short") || raw.Contains("weak") || raw.Contains("at least"))) return "비밀번호는 6자 이상이어야 해요";
            if (raw.Contains("email_not_confirmed") || raw.Contains("not confirmed")) return "서버 설정에서 이메일 확인을 꺼야 해요 (관리자)";
            if (raw.Contains("rate limit") || raw.Contains("over_request_rate")) return "잠시 후 다시 시도해 주세요";
            if (raw.Contains("email_provider_disabled") || raw.Contains("logins are disabled") || raw.Contains("signups are disabled")) return "서버에서 Email 로그인이 꺼져 있어요 (관리자)";
            if (raw.Contains("signup_disabled") || raw.Contains("signups not allowed")) return "서버에서 회원가입이 꺼져 있어요 (관리자)";
            if (raw.Contains("email_address_invalid")) return "이 아이디는 쓸 수 없어요. 다른 아이디를 써 주세요";
            // unknown: show the server's own short reason so it can be fixed
            string why = a != null ? (!string.IsNullOrEmpty(a.error_code) ? a.error_code : !string.IsNullOrEmpty(a.msg) ? a.msg : a.message) : null;
            return "잠시 후 다시 시도해 주세요 (" + (int)r.responseCode + (string.IsNullOrEmpty(why) ? "" : " " + why) + ")";
        }

        // ------------------------------------------------------------------ auth
        public IEnumerator SignUp(string name, string pw, Action<bool, string> done)
        {
            Busy = true;
            using (var r = Req("POST", "/auth/v1/signup", $"{{\"email\":\"{Esc(name.ToLowerInvariant() + CloudConfig.EmailDomain)}\",\"password\":\"{Esc(pw)}\",\"data\":{{\"username\":\"{Esc(name)}\"}}}}", null))
            {
                yield return r.SendWebRequest();
                Busy = false;
                var a = Parse(r.downloadHandler.text);
                if (r.result == UnityWebRequest.Result.Success && a != null && !string.IsNullOrEmpty(a.access_token)) { Accept(a, name); done(true, "가입 완료!"); }
                else if (r.result == UnityWebRequest.Result.Success && a != null && a.user != null && string.IsNullOrEmpty(a.access_token)) done(false, "서버 설정에서 이메일 확인을 꺼야 해요 (관리자)");
                else done(false, Friendly(r, a));
            }
        }

        public IEnumerator Login(string name, string pw, Action<bool, string> done)
        {
            Busy = true;
            using (var r = Req("POST", "/auth/v1/token?grant_type=password", $"{{\"email\":\"{Esc(name.ToLowerInvariant() + CloudConfig.EmailDomain)}\",\"password\":\"{Esc(pw)}\"}}", null))
            {
                yield return r.SendWebRequest();
                Busy = false;
                var a = Parse(r.downloadHandler.text);
                if (r.result == UnityWebRequest.Result.Success && a != null && !string.IsNullOrEmpty(a.access_token)) { Accept(a, name); done(true, "로그인했어요"); }
                else done(false, Friendly(r, a));
            }
        }

        /// <summary>Sign back in with the stored refresh token (auto-login on start, and before the access token expires).</summary>
        public IEnumerator Refresh(Action<bool> done)
        {
            string rt = refresh ?? PlayerPrefs.GetString(RefreshPref, "");
            if (string.IsNullOrEmpty(rt) || !CloudConfig.Enabled) { done(false); yield break; }
            using (var r = Req("POST", "/auth/v1/token?grant_type=refresh_token", $"{{\"refresh_token\":\"{Esc(rt)}\"}}", null))
            {
                yield return r.SendWebRequest();
                var a = Parse(r.downloadHandler.text);
                if (r.result == UnityWebRequest.Result.Success && a != null && !string.IsNullOrEmpty(a.access_token)) { Accept(a, PlayerPrefs.GetString(NamePref, "")); done(true); }
                else
                {
                    if (r.result == UnityWebRequest.Result.ProtocolError) { PlayerPrefs.DeleteKey(RefreshPref); PlayerPrefs.Save(); }
                    done(false);
                }
            }
        }

        public void Logout()
        {
            access = refresh = null; UserId = null; Username = null;
            JobCache.Clear();
            PlayerPrefs.DeleteKey(RefreshPref); PlayerPrefs.DeleteKey(NamePref); PlayerPrefs.Save();
        }

        static AuthResp Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<AuthResp>(json); } catch { return null; }
        }

        void Accept(AuthResp a, string name)
        {
            access = a.access_token; refresh = a.refresh_token;
            expiresAt = Time.realtimeSinceStartup + Mathf.Max(60, a.expires_in) - 60;
            UserId = a.user != null ? a.user.id : UserId;
            if (!string.IsNullOrEmpty(name)) Username = name;
            PlayerPrefs.SetString(RefreshPref, refresh ?? "");
            PlayerPrefs.SetString(NamePref, Username ?? "");
            PlayerPrefs.Save();
        }

        IEnumerator EnsureFresh()
        {
            if (LoggedIn && Time.realtimeSinceStartup > expiresAt) yield return Refresh(_ => { });
        }

        /// <summary>
        /// The token to send for user <paramref name="uid"/> after an <see cref="EnsureFresh"/> wait. Saves started just before
        /// a logout keep the token they captured, so the last save still reaches the right account.
        /// </summary>
        string TokenFor(string uid, string captured) => LoggedIn && UserId == uid ? access : captured;

        // ------------------------------------------------------------------ cloud data
        /// <summary>Download the profile JSON (null when the account has no save yet) and every job's progress.</summary>
        public IEnumerator Pull(Action<bool, string> done)
        {
            yield return EnsureFresh();
            string profile = null;
            using (var r = Req("GET", $"/rest/v1/saves?select=data&user_id=eq.{UserId}", null, access))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { done(false, null); yield break; }
                var t = r.downloadHandler.text.Trim();
                // [{"data":{...}}] -> {...}   ([] when the account has no save yet)
                int k = t.IndexOf("\"data\"", StringComparison.Ordinal);
                int c = k < 0 ? -1 : t.IndexOf(':', k);
                if (c > 0 && t.EndsWith("]"))
                {
                    var body = t.Substring(0, t.Length - 1).TrimEnd();          // drop ]
                    if (body.EndsWith("}")) profile = body.Substring(c + 1, body.Length - 1 - (c + 1)).Trim();   // drop the row's }
                }
            }
            using (var r = Req("GET", $"/rest/v1/job_saves?select=job_id,data&user_id=eq.{UserId}", null, access))
            {
                yield return r.SendWebRequest();
                JobCache.Clear();
                if (r.result == UnityWebRequest.Result.Success)
                {
                    var rows = JsonUtility.FromJson<JobRows>("{\"rows\":" + r.downloadHandler.text + "}");
                    if (rows?.rows != null) foreach (var row in rows.rows) JobCache[row.job_id] = row.data;
                }
            }
            done(true, profile);
        }

        public IEnumerator PushProfile(string json)
        {
            if (!LoggedIn) yield break;
            string uid = UserId, tok = access;
            yield return EnsureFresh();
            using (var r = Req("POST", "/rest/v1/saves?on_conflict=user_id", $"{{\"user_id\":\"{uid}\",\"data\":{json},\"updated_at\":\"{DateTime.UtcNow:o}\"}}", TokenFor(uid, tok)))
            {
                r.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) Debug.LogWarning("[Banjjak] cloud profile save failed: " + r.responseCode + " " + r.downloadHandler.text);
            }
        }

        public IEnumerator PushJob(string jobId, string b64)
        {
            if (!LoggedIn) yield break;
            JobCache[jobId] = b64;
            string uid = UserId, tok = access;
            yield return EnsureFresh();
            using (var r = Req("POST", "/rest/v1/job_saves?on_conflict=user_id,job_id", $"{{\"user_id\":\"{uid}\",\"job_id\":\"{Esc(jobId)}\",\"data\":\"{b64}\",\"updated_at\":\"{DateTime.UtcNow:o}\"}}", TokenFor(uid, tok)))
            {
                r.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) Debug.LogWarning("[Banjjak] cloud job save failed: " + r.responseCode + " " + r.downloadHandler.text);
            }
        }

        public IEnumerator DeleteJob(string jobId)
        {
            JobCache.Remove(jobId);
            if (!LoggedIn) yield break;
            string uid = UserId, tok = access;
            yield return EnsureFresh();
            using (var r = Req("DELETE", $"/rest/v1/job_saves?user_id=eq.{uid}&job_id=eq.{UnityWebRequest.EscapeURL(jobId)}", null, TokenFor(uid, tok)))
                yield return r.SendWebRequest();
        }
    }
}
