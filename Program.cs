using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Note163Backup
{
    class Program
    {
        const string ROOT_ID_URL = "https://note.youdao.com/yws/api/personal/file?method=getByPath&keyfrom=web";
        const string DIR_MES_URL = "https://note.youdao.com/yws/api/personal/file/{0}?all=true&f=true&len=500&sort=1&isReverse=false&method=listPageByParentId&keyfrom=web";//指定目录下指定数量的数据（文件/文件夹）
        const string FILE_URL = "https://note.youdao.com/yws/api/personal/sync?method=download&keyfrom=web";
        const string DOWN_LOG_PATH = "down", COOKIE_PATH = "cookie";

        static readonly CookieContainer _container = new CookieContainer();
        static readonly HttpClient _client = new HttpClient(new SocketsHttpHandler { CookieContainer = _container, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
        static Conf _conf;
        static HttpClient _scClient;
        static async Task Main()
        {
            _conf = Deserialize<Conf>(GetEnvValue("CONF"));
            if (!string.IsNullOrWhiteSpace(_conf.ScKey))
            {
                _scClient = new HttpClient();
            }

            Console.WriteLine("有道云笔记备份开始运行...");

            #region 验证和登录

            string cookie = await File.ReadAllTextAsync(COOKIE_PATH);
            _client.DefaultRequestHeaders.Add("Cookie", cookie);
            _client.DefaultRequestHeaders.Connection.Add("keep-alive");

            Console.WriteLine("验证cookie...");
            var (isOK, rootData) = await GetRootData();
            if (isOK)
            {
                Console.WriteLine("cookie有效");
            }
            else
            {
                Console.WriteLine("cookie失效，使用账号密码登录...");
                var rspMsg = await _client.PostAsync("https://note.youdao.com/login/acc/urs/verify/check?app=web&product=YNOTE&tp=urstoken&cf=6&fr=1&systemName=&deviceType=&ru=https%3A%2F%2Fnote.youdao.com%2FsignIn%2F%2FloginCallback.html&er=https%3A%2F%2Fnote.youdao.com%2FsignIn%2F%2FloginCallback.html&vcode=&systemName=Windows&deviceType=WindowsPC", new StringContent($"username={_conf.Username}&password={MD5Hash(_conf.Password)}", Encoding.UTF8, "application/x-www-form-urlencoded"));
                if (rspMsg.RequestMessage.RequestUri.AbsoluteUri.Contains("ecode"))
                {//登录失败
                    await Notify($"账号{_conf.Task} 登录失败，请检查账号密码是否正确！或者在网页上登录后再次运行本程序！", true);
                    return;
                }

                Console.WriteLine("登录成功！");
                cookie = _container.GetCookieHeader(new Uri("https://note.youdao.com"));
                await File.WriteAllTextAsync(COOKIE_PATH, cookie);
                (isOK, rootData) = await GetRootData();
            }

            #endregion

            Entry entry = Deserialize<Entry>(rootData);
            string info = $"Root dirNum:{entry.fileEntry.dirNum}, fileNum:{entry.fileEntry.fileNum}";
            Console.WriteLine(info);
            await Log(info);

            //循环下载数据
            await Exec(entry.fileEntry);

            Console.WriteLine("备份运行完毕");
        }

        static int _num = 1;
        static async Task Exec(Fileentry fileentry)
        {
            if (!fileentry.dir)
            {//文件，下载到本地
                string result = string.Empty;
                try
                {
                    await RetryRun(async () =>
                    {
                        HttpResponseMessage rspMsg = await _client.PostAsync(FILE_URL, new StringContent($"fileId={fileentry.id}&version=-1&read=true", Encoding.UTF8, "application/x-www-form-urlencoded"));
                        using Stream stream = await rspMsg.Content.ReadAsStreamAsync();
                        Directory.CreateDirectory(Path.GetDirectoryName(fileentry.name));
                        using FileStream fileStream = File.Create(fileentry.name);
                        await stream.CopyToAsync(fileStream);
                        result = "ok";
                    });
                }
                catch (Exception ex)
                {
                    result = $"Ex! {ex.Message}";
                }

                Console.WriteLine($"file {_num}: {result}");
                await Log($"{fileentry.name}: {result}");
                _num++;
                return;
            }

            #region 目录

            //获取指定目录下指定数量的文件夹/文件
            string json = await RetryRun(() => _client.GetStringAsync(string.Format(DIR_MES_URL, fileentry.id)));
            YdRsp ydRsp = Deserialize<YdRsp>(json);
            foreach (var entry in ydRsp.entries)
            {
                entry.fileEntry.name = $"{fileentry.name}/{entry.fileEntry.name}";
                await Exec(entry.fileEntry);
            }

            #endregion
        }

        static async Task<(bool isOK, string rootData)> GetRootData()
        {
            HttpResponseMessage rootRspMsg = await _client.PostAsync(ROOT_ID_URL, new StringContent("path=/&entire=true&purge=false", Encoding.UTF8, "application/x-www-form-urlencoded"));
            string rootData = await rootRspMsg.Content.ReadAsStringAsync();
            return (rootData.Contains("fileEntry"), rootData);
        }

        static async Task Log(string msg)
        {
            await File.AppendAllTextAsync(DOWN_LOG_PATH, $"{msg}{Environment.NewLine}");
        }

        static T RetryRun<T>(Func<T> func, int retryNum = 1)
        {
            int maxRunNum = retryNum + 1;
            for (int i = 0; i < maxRunNum; i++)
            {
                try
                {
                    return func();
                }
                catch (Exception)
                {
                    if (i == retryNum)
                    {
                        throw;
                    }
                }
            }
            return default;
        }

        static string MD5Hash(string str)
        {
            StringBuilder sbHash = new StringBuilder(32);
            byte[] s = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
            for (int i = 0; i < s.Length; i++)
            {
                sbHash.Append(s[i].ToString("x2"));
            }
            return sbHash.ToString();
        }

        static async Task Notify(string msg, bool isFailed = false)
        {
            Console.WriteLine(msg);
            if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
            {
                await _scClient?.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?text={msg}");
            }
        }

        static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

        static string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);
    }

    #region Conf

    public class Conf
    {
        public string Task { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ScKey { get; set; }
        public string ScType { get; set; }
    }

    #endregion
}
