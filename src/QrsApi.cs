#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Q2g.HelperQrs
{
    #region Usings
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
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
        public string ReportType { get; set; } = "Qlik report";
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

    #region Qlik Qrs Objects
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class HubInfo
    {
        public Guid? Id { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public List<string> CustomProperties { get; set; }
        public Owner Owner { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public Uri Uri { get; set; }
        public List<Tag> Tags { get; set; }
        public List<Reference> References { get; set; }
        public List<MetaData> MetaData { get; set; }
        public List<string> Privileges { get; set; }
        public bool? ImpactSecurityAccess { get; set; }
        public string SchemaPath { get; set; }

        public override string ToString()
        {
            return Id?.ToString();
        }
    }

    public class Tag
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
               NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikAnalyticConnection
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public List<object> CustomProperties { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string CertificateFilePath { get; set; }
        public int ReconnectTimeout { get; set; }
        public int RequestTimeout { get; set; }
        public List<string> Privileges { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikSecurityRule
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Rule { get; set; }
        public string ResourceFilter { get; set; }
        public int Actions { get; set; }
        public string Comment { get; set; }
        public bool Disabled { get; set; }
        public int RuleContext { get; set; }
        public string SeedId { get; set; }
        public int Version { get; set; }
        public List<Tag> Tags { get; set; }
        public List<string> Privileges { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class LogVerbosity
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public int LogVerbosityAuditActivity { get; set; }
        public int LogVerbosityAuditSecurity { get; set; }
        public int LogVerbosityService { get; set; }
        public int LogVerbosityAudit { get; set; }
        public int LogVerbosityPerformance { get; set; }
        public int LogVerbositySecurity { get; set; }
        public int LogVerbositySystem { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Role
    {
        public string Id { get; set; }
        public int Definition { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ServiceCluster
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class LoadBalancingServerNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HostName { get; set; }
        public string Temporaryfilepath { get; set; }
        public List<Role> Roles { get; set; }
        public ServiceCluster ServiceCluster { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class VirtualProxy
    {
        public string Id { get; set; }
        public string Prefix { get; set; }
        public string Description { get; set; }
        public string AuthenticationModuleRedirectUri { get; set; }
        public string SessionModuleBaseUri { get; set; }
        public string LoadBalancingModuleBaseUri { get; set; }
        public bool UseStickyLoadBalancing { get; set; }
        public List<LoadBalancingServerNode> LoadBalancingServerNodes { get; set; }
        public int AuthenticationMethod { get; set; }
        public int HeaderAuthenticationMode { get; set; }
        public string HeaderAuthenticationHeaderName { get; set; }
        public string HeaderAuthenticationStaticUserDirectory { get; set; }
        public string HeaderAuthenticationDynamicUserDirectory { get; set; }
        public int AnonymousAccessMode { get; set; }
        public string WindowsAuthenticationEnabledDevicePattern { get; set; }
        public string SessionCookieHeaderName { get; set; }
        public string SessionCookieDomain { get; set; }
        public string AdditionalResponseHeaders { get; set; }
        public int SessionInactivityTimeout { get; set; }
        public bool ExtendedSecurityEnvironment { get; set; }
        public List<string> WebsocketCrossOriginWhiteList { get; set; }
        public bool DefaultVirtualProxy { get; set; }
        public List<Tag> Tags { get; set; }
        public string SamlMetadataIdP { get; set; }
        public string SamlHostUri { get; set; }
        public string SamlEntityId { get; set; }
        public string SamlAttributeUserId { get; set; }
        public string SamlAttributeUserDirectory { get; set; }
        public int SamlAttributeSigningAlgorithm { get; set; }
        public List<object> SamlAttributeMap { get; set; }
        public string JwtAttributeUserId { get; set; }
        public string JwtAttributeUserDirectory { get; set; }
        public string JwtPublicKeyCertificate { get; set; }
        public List<object> JwtAttributeMap { get; set; }
        public string MagicLinkHostUri { get; set; }
        public string MagicLinkFriendlyName { get; set; }
        public bool SamlSlo { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Settings
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public int ListenPort { get; set; }
        public bool AllowHttp { get; set; }
        public int UnencryptedListenPort { get; set; }
        public int AuthenticationListenPort { get; set; }
        public bool KerberosAuthentication { get; set; }
        public int UnencryptedAuthenticationListenPort { get; set; }
        public string SslBrowserCertificateThumbprint { get; set; }
        public int KeepAliveTimeoutSeconds { get; set; }
        public int MaxHeaderSizeBytes { get; set; }
        public int MaxHeaderLines { get; set; }
        public LogVerbosity LogVerbosity { get; set; }
        public bool UseWsTrace { get; set; }
        public int PerformanceLoggingInterval { get; set; }
        public int RestListenPort { get; set; }
        public List<VirtualProxy> VirtualProxies { get; set; }
        public string FormAuthenticationPageTemplate { get; set; }
        public object LoggedOutPageTemplate { get; set; }
        public string ErrorPageTemplate { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ServerNodeConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HostName { get; set; }
        public string Temporaryfilepath { get; set; }
        public List<Role> Roles { get; set; }
        public ServiceCluster ServiceCluster { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                 NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikVirtualProxySettings
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public List<object> CustomProperties { get; set; }
        public Settings Settings { get; set; }
        public ServerNodeConfiguration ServerNodeConfiguration { get; set; }
        public List<Tag> Tags { get; set; }
        public List<string> Privileges { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikVersion
    {
        public string BuildVersion { get; set; }
        public string BuildDate { get; set; }
        public string DatabaseProvider { get; set; }
        public int NodeType { get; set; }
        public bool SharedPersistence { get; set; }
        public bool RequiresBootstrap { get; set; }
        public bool SingleNodeOnly { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Owner
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string UserDirectory { get; set; }
        public string Name { get; set; }
        public List<string> Privileges { get; set; }

        public override string ToString()
        {
            return $"{UserDirectory.ToLowerInvariant()}\\{UserId.ToLowerInvariant()}";
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                 NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class WhiteList
    {
        public string Id { get; set; }
        public int LibraryType { get; set; }
        public List<string> Privileges { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Reference
    {
        public string Id { get; set; }
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

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class MetaData
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public bool ImpactSecurityAccess { get; set; }
        public string SchemaPath { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikExtention
    {
        public string Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedByUserName { get; set; }
        public List<object> CustomProperties { get; set; }
        public Owner Owner { get; set; }
        public string Name { get; set; }
        public List<Tag> Tags { get; set; }
        public WhiteList WhiteList { get; set; }
        public List<Reference> References { get; set; }
        public List<string> Privileges { get; set; }
        public string SchemaPath { get; set; }
    }
    #endregion
}