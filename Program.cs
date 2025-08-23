using Note163Backup;
using StackExchange.Redis;
using System.Net;
using System.Text;
using System.Text.Json;

const string ROOT_ID_URL = "https://note.youdao.com/yws/api/personal/file?method=getByPath&keyfrom=web";
const string DIR_MES_URL = "https://note.youdao.com/yws/api/personal/file/{0}?all=true&f=true&len=500&sort=1&isReverse=false&method=listPageByParentId&keyfrom=web";//指定目录下指定数量的数据（文件/文件夹）
const string FILE_URL = "https://note.youdao.com/yws/api/personal/sync?method=download&keyfrom=web";
const string DOWN_LOG_PATH = "down";

JsonSerializerOptions _options = new()
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};

Conf _conf = Deserialize<Conf>(GetEnvValue("CONF"));
HttpClient _scClient = new();

#region redis

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect($"{_conf.RdsServer},password={_conf.RdsPwd},name=Note163Backup,defaultDatabase=0,allowadmin=true,abortConnect=false");
IDatabase db = redis.GetDatabase();
bool isRedis = db.IsConnected("test");
Console.WriteLine("redis:{0}", isRedis ? "有效" : "无效");
if (!isRedis)
{
    await Notify($"账号{_conf.Task}redis无效", true);
    return;
}

#endregion

#region 获取cookie

string cookie = string.Empty;
bool isInvalid = true; string rootData = string.Empty;

string redisKey = $"Note163_{_conf.Username}";
cookie = await GetCookie(redisKey);
(isInvalid, rootData) = await IsInvalid(cookie);
Console.WriteLine("redis获取cookie,状态:{0}", isInvalid ? "无效" : "有效");

if (isInvalid)
{
    Console.WriteLine("刷新Cookie");
    await CommandHelper.ExecCommand(_conf.RefreshCookieCommands);

    cookie = await GetCookie(redisKey);
    (isInvalid, rootData) = await IsInvalid(cookie);
    Console.WriteLine("refresh获取cookie,状态:{0}", isInvalid ? "无效" : "有效");
    if (isInvalid)
    {//Cookie失效
        await Notify($"账号{_conf.Task}Cookie失效，请检查登录状态！", true);
        return;
    }
}

Console.WriteLine($"redis更新cookie:{await db.StringSetAsync(redisKey, cookie)}");

#endregion

Console.WriteLine("有道云笔记备份开始运行...");

int _num = 1;
HttpClient _client = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
_client.DefaultRequestHeaders.Add("Cookie", cookie);
_client.DefaultRequestHeaders.Connection.Add("keep-alive");

Entry entry = Deserialize<Entry>(rootData);
string info = $"Root dirNum:{entry.fileEntry.dirNum}, fileNum:{entry.fileEntry.fileNum}";
Console.WriteLine(info);
await Log(info);

//循环下载数据
await Exec(entry.fileEntry);

Console.WriteLine("备份运行完毕");


async Task Exec(Fileentry fileentry)
{
    if (!fileentry.dir)
    {//文件，下载到本地
        string result = string.Empty;
        try
        {
            await RetryRun(async () =>
            {
                HttpResponseMessage rspMsg = await _client.PostAsync(FILE_URL, new StringContent($"fileId={fileentry.id}&version=-1&read=true", Encoding.UTF8, "application/x-www-form-urlencoded"));
                if (rspMsg.IsSuccessStatusCode)
                {
                    using Stream stream = await rspMsg.Content.ReadAsStreamAsync();
                    Directory.CreateDirectory(Path.GetDirectoryName(fileentry.name));
                    using FileStream fileStream = File.Create(fileentry.name);
                    await stream.CopyToAsync(fileStream);
                    result = "ok";
                }
                else
                {
                    string oldFilePath = Path.Combine("data", fileentry.name);
                    FileInfo oldFileInfo = new(oldFilePath);
                    if (oldFileInfo.Exists)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fileentry.name));
                        oldFileInfo.MoveTo(fileentry.name);
                    }
                    result = $"old:{oldFileInfo.Exists}. {(int)rspMsg.StatusCode}:{rspMsg.StatusCode}";
                }
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

static async Task Log(string msg)
{
    await File.AppendAllTextAsync(DOWN_LOG_PATH, $"{msg}{Environment.NewLine}");
}

static T RetryRun<T>(Func<T> func, int retryNum = 5)
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

async Task<(bool isInvalid, string rootData)> IsInvalid(string cookie)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    HttpResponseMessage rootRspMsg = await client.PostAsync(ROOT_ID_URL, new StringContent("path=/&entire=true&purge=false", Encoding.UTF8, "application/x-www-form-urlencoded"));
    string rootData = await rootRspMsg.Content.ReadAsStringAsync();
    return (!rootData.Contains("fileEntry"), rootData);
}

async Task<string> GetCookie(string redisKey)
{
    string cookie = string.Empty;
    var redisValue = await db.StringGetAsync(redisKey);
    if (redisValue.HasValue)
    {
        cookie = redisValue.ToString();
    }

    return cookie;
}

async Task Notify(string msg, bool isFailed = false)
{
    Console.WriteLine(msg);
    if (_conf.NotifyType == "Always" || (isFailed && _conf.NotifyType == "Failed"))
    {
        await CommandHelper.ExecCommand(_conf.NotifyCommand.Replace("$title", "Note163Backup").Replace("$msg", msg));
    }
}

T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);

#region Conf

public class Conf
{
    public string Task { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string NotifyType { get; set; }
    public string NotifyCommand { get; set; }
    public string RdsServer { get; set; }
    public string RdsPwd { get; set; }
    public string[] RefreshCookieCommands { get; set; }
}

#endregion
