namespace Q2gHelperQrs
{
    #region Usings
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Text;
    #endregion

    #region Enumeration
    public enum RequestType
    {
        CREATE,
        UPDATE,
        DELETE,
        SELECT
    }
    #endregion

    #region Json Communication Classes
    public class HubOwer
    {
        public Guid? Id { get; set; }
        public string UserId { get; set; }
        public string UserDirectory { get; set; }
        public string Name { get; set; }
        public List<string> Privileges { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class HubReferences
    {
        public Guid? Id { get; set; }
        public string DataLocation { get; set; }
        public string LogicalPath { get; set; }
        public string ExternalPath { get; set; }
        public int ServeOptions { get; set; }
        public List<string> Privileges { get; set; }

        public override string ToString()
        {
            return Id?.ToString();
        }
    }

    public class HubInfo
    {
        public Guid? Id { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public List<string> CustomProperties { get; set; }
        public HubOwer Owner { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public Uri Uri { get; set; }
        public List<string> Tags { get; set; }
        public List<HubReferences> References { get; set; }
        public List<string> MetaData { get; set; }
        public List<string> Privileges { get; set; }
        public bool? ImpactSecurityAccess { get; set; }
        public string SchemaPath { get; set; }

        public override string ToString()
        {
            return Id?.ToString();
        }
    }
    #endregion

    #region Client Request Classes
    public class ContentData
    {
        public string ContentType { get; set; }
        public byte[] FileData { get; set; }
        public string ExternalPath { get; set; }
    }

    public interface IHubRequest
    {
        RequestType Type { get; }
    }

    public class HubCreateRequest : IHubRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ContentData Data { get; set; }
        public RequestType Type => RequestType.CREATE;
    }

    public class HubUpdateRequest : IHubRequest
    {
        public HubInfo Info { get; set; }
        public ContentData Data { get; set; }
        public RequestType Type => RequestType.UPDATE;
    }

    public class HubDeleteRequest : IHubRequest
    {
        public Guid Id { get; set; }
        public RequestType Type => RequestType.DELETE;
    }

    public class HubSelectCountRequest : IHubRequest
    {
        public string Filter { get; set; }
        public RequestType Type => RequestType.SELECT;

        public static string GetNameFilter(string contentName)
        {
            return $"Name eq '{contentName}'";
        }
        public static string GetIdFilter(Guid sharedContentId)
        {
            return $"Id eq {sharedContentId.ToString()}";
        }
    }

    public class HubSelectRequest : HubSelectCountRequest
    {
        public string OrderBy { get; set; }
    }
    #endregion
}