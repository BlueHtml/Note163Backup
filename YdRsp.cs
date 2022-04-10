namespace Note163Backup;

public class YdRsp
{
    public Entry[] entries { get; set; }
    public int count { get; set; }
}

public class Entry
{
    public Fileentry fileEntry { get; set; }
    public Filemeta fileMeta { get; set; }
    public Otherprop otherProp { get; set; }
    public object[] ocrHitInfo { get; set; }
}

public class Fileentry
{
    public string userId { get; set; }
    public string id { get; set; }
    public int version { get; set; }
    public string name { get; set; }
    public string parentId { get; set; }
    public int createTimeForSort { get; set; }
    public int modifyTimeForSort { get; set; }
    public int fileNum { get; set; }
    public int dirNum { get; set; }
    public int subTreeFileNum { get; set; }
    public int subTreeDirNum { get; set; }
    public int fileSize { get; set; }
    public bool favorited { get; set; }
    public bool deleted { get; set; }
    public bool erased { get; set; }
    public bool publicShared { get; set; }
    public string tags { get; set; }
    public int domain { get; set; }
    public int entryType { get; set; }
    public object createProduct { get; set; }
    public object namePath { get; set; }
    public int orgEditorType { get; set; }
    public int transactionTime { get; set; }
    public Entryprops entryProps { get; set; }
    public string transactionId { get; set; }
    public string modDeviceId { get; set; }
    public string checksum { get; set; }
    public bool myKeep { get; set; }
    public bool myKeepV2 { get; set; }
    public string myKeepAuthor { get; set; }
    public string myKeepAuthorV2 { get; set; }
    public string summary { get; set; }
    public string noteType { get; set; }
    public bool hasComment { get; set; }
    public int rightOfControl { get; set; }
    public bool dir { get; set; }
}

public class Entryprops
{
    public string modId { get; set; }
    public string orgEditorType { get; set; }
    public string encrypted { get; set; }
    public string public_link { get; set; }
    public string bgImageId { get; set; }
}

public class Filemeta
{
    public object chunkList { get; set; }
    public int sharedCount { get; set; }
    public string title { get; set; }
    public int fileSize { get; set; }
    public string author { get; set; }
    public string sourceURL { get; set; }
    public object[] resources { get; set; }
    public object resourceName { get; set; }
    public object resourceMime { get; set; }
    public bool storeAsWholeFile { get; set; }
    public int coopNoteVersion { get; set; }
    public Metaprops metaProps { get; set; }
    public object[] externalDownload { get; set; }
    public int createTimeForSort { get; set; }
    public int modifyTimeForSort { get; set; }
    public object contentType { get; set; }
}

public class Metaprops
{
    public string spaceused { get; set; }
    public string FILE_IDENTIFIER { get; set; }
    public string WHOLE_FILE_TYPE { get; set; }
    public string tp { get; set; }
    public string st { get; set; }
}

public class Otherprop
{
}
