#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Q2gHelperQrsNuget
{
    #region Usings
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using NLog;
    using SerApiNuget;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Q2gHelperPemNuget;
    using System.Net.Http;
    using System.Net.Http.Headers;
    #endregion

    public class QlikQrsHub
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        public X509Certificate2 ClientCertficate { get; private set; }
        public X509Certificate2 RootCertificate { get; private set; }
        public Uri ConnectUri { get; private set; }
        public string UserId { get; set; }
        public string UserDirectory { get; set; }
        #endregion

        #region Constructor
        public QlikQrsHub(Uri connectUri,  string userId = "sa_reporting", string userDirectory = "INTERNAL",
                          X509Certificate2 rootCertificate = null, IQlikCredentials credentials = null)
        {
            var clientCert = new X509Certificate2();
            if (credentials == null)
                ClientCertficate = clientCert.GetQlikClientCertificate();
            else
            {
                if (credentials?.Type == QlikCredentialType.CERTIFICATE)
                {
                    var certAuth = credentials as CertificateAuth;
                    ClientCertficate = clientCert.GetQlikClientCertificate(certAuth?.CertificatePath, certAuth?.CertPassword);
                }
                else
                {
                    throw new NotImplementedException($"Unknown authentication type {credentials.Type}");
                }
            }

            ConnectUri = connectUri;
            UserId = userId;
            UserDirectory = userDirectory;
        }
        #endregion

        #region Privat Methods
        private string GetRandomAlphanumericString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new string(Enumerable.Repeat(chars, length)
                                              .Select(s => s[random.Next(s.Length)])
                                              .ToArray());
            return result;
        }

        private Uri BuildUriWithKey(Uri uri, string key, string filter, string orderby)
        {
            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["Xrfkey"] = key;

            if (filter != null)
                query["filter"] = filter;

            if (orderby != null)
                query["orderby"] = orderby;

            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri;
        }

        private string SendRequest(Uri requestUri, HttpMethod method, string contentType, byte[] data = null,
                                   string filter = null, string orderby = null)
        {
            //Create Key
            var key = GetRandomAlphanumericString(16);
            var keyRelativeUri = BuildUriWithKey(requestUri, key, filter, orderby);

            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ClientCertificates.Add(ClientCertficate);
            httpClientHandler.ServerCertificateCustomValidationCallback += ValidateCertificate;

            var httpClient = new HttpClient(httpClientHandler) { BaseAddress = ConnectUri };
            var request = new HttpRequestMessage(method, keyRelativeUri);
            request.Headers.Add("X-Qlik-Xrfkey", key);
            request.Headers.Add("X-Qlik-User", $"UserDirectory={UserDirectory};UserId={UserId}");

            if (data != null)
            {
                request.Content = new ByteArrayContent(data);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            var result = httpClient.SendAsync(request).Result;
            if (result.IsSuccessStatusCode)
                return result.Content.ReadAsStringAsync().Result;
            else
                return null;
        }

        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            try
            {
                var date = DateTime.Parse(certificate.GetExpirationDateString());
                var result = DateTime.Compare(date, DateTime.Now);
                if (result > 0)
                {
                    var primaryCert = new X509Certificate2(certificate);
                    return chain.Build(primaryCert);
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }
        }

        private string CreateOrUpdatePublishedReport(Uri address, HubInfo jsonRequest)
        {
            if (jsonRequest == null)
                return null;

            var httpMethod = HttpMethod.Post;
            if (jsonRequest.Id != null)
                httpMethod = HttpMethod.Put;

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            var jsonStr = JsonConvert.SerializeObject(jsonRequest, settings);
            var data = Encoding.UTF8.GetBytes(jsonStr);
            return SendRequest(address, httpMethod, "application/json", data);
        }

        private void UploadFileInternal(HubInfo request)
        {
            try
            {
                //Logging bei null
                if (request == null)
                    return;

                string result = String.Empty;
                Guid contentId = Guid.Empty;

                if (request.InternalType == RequestType.CREATE)
                {
                    //Create Published Report
                    var newUri = new Uri(ConnectUri, "/qrs/sharedcontent");
                    result = CreateOrUpdatePublishedReport(newUri, request);
                    var hubInfo = JsonConvert.DeserializeObject<HubInfo>(result);
                    contentId = hubInfo.Id.Value;
                }
                else if (request.InternalType == RequestType.UPDATE)
                {
                    //Update Published Report
                    //-->>Logging bei mehreren Dokumenten mit dem selben Namen //Warnung
                    contentId = request.Id.Value;
                    var newUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{contentId}");
                    CreateOrUpdatePublishedReport(newUri, request);
                }

                if (!String.IsNullOrEmpty(request.FullPath))
                {
                    //Upload File
                    var fileData = File.ReadAllBytes(request.FullPath);
                    var contentType = $"application/{Path.GetExtension(request.FullPath).TrimStart('.')}";
                    var path = Path.GetFileName(request.FullPath);
                    var newUploadUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{contentId}/uploadfile?externalpath={path}");
                    result = SendRequest(newUploadUri, HttpMethod.Post, contentType, fileData);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("The file upload to the hub is failed.", ex);
            }
        }

        public HubInfo GetFirstSharedContent(string contentName)
        {
            try
            {
                return GetAllSharedContent($"Name eq '{contentName}'").FirstOrDefault() ?? null;
            }
            catch (Exception ex)
            {
                throw new Exception("The content could not be determined.", ex);
            }
        }

        public HubInfo GetSharedContent(Guid sharedId)
        {
            try
            {
                return GetAllSharedContent($"Id eq {sharedId.ToString()}").SingleOrDefault() ?? null;
            }
            catch (Exception ex)
            {
                throw new Exception("The content could not be determined.", ex);
            }
        }
        #endregion

        #region Public Methods
        public int GetSharedContentCount(string filter = null)
        {
            try
            {
                var newUri = new Uri(ConnectUri, "/qrs/sharedcontent/count");
                var result = SendRequest(newUri, HttpMethod.Get, "application/json", null, filter);
                var count = JsonConvert.DeserializeObject<JToken>(result).Value<int>("value");
                logger.Debug($"SharedContentCount: {count}");
                return count;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentCount)}\" failed.");
                return 0;
            }
        }

        public List<HubInfo> GetAllSharedContent(string filter = null, string orderby = null)
        {
            try
            {
                var newUri = new Uri(ConnectUri, "/qrs/sharedcontent/full");
                var result = SendRequest(newUri, HttpMethod.Get, "application/json", null, filter, orderby);
                return JsonConvert.DeserializeObject<List<HubInfo>>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetAllSharedContent)}\" failed.");
                return null;
            }
        }

        public bool DeleteAll()
        {
            try
            {
                var sharedInfos = GetAllSharedContent();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                Parallel.ForEach(sharedInfos, parallelOptions, (sharedInfo) =>
                {
                    Delete(sharedInfo.Id.Value);
                });

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteAll)}\" failed.");
                return false;
            }
        }

        public bool Delete(string contentName)
        {
            try
            {
                var firstHubInfo = GetFirstSharedContent(contentName);
                var newUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{firstHubInfo.Id.Value}");
                var result = SendRequest(newUri, HttpMethod.Delete, null, null);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Delete)}\" with content name failed.");
                return false;
            }
        }

        public bool Delete(Guid id)
        {
            try
            {
                var newUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{id}");
                var result = SendRequest(newUri, HttpMethod.Delete, null, null);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Delete)}\" with id failed.");
                return false;
            }
        }

        public bool CreateMany(List<HubInfo> createRequests)
        {
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                Parallel.ForEach(createRequests, parallelOptions, (sharedInfo) =>
                {
                    Create(sharedInfo);
                });

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(CreateMany)}\" with id failed.");
                return false;
            }
        }

        public bool Create(string contentName, string fullpath, string description = null)
        {
            try
            {
                var createRequest = new HubInfo
                {
                    InternalType = RequestType.CREATE,
                    Description = description,
                    Name = contentName,
                    Type = "Qlik report",
                    FullPath = fullpath
                };

                if (!File.Exists(fullpath))
                    throw new Exception($"The file {fullpath} not exists.");

                UploadFileInternal(createRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Create)}\" with id failed.");
                return false;
            }
        }

        public bool Create(HubInfo createRequest)
        {
            try
            {
                if (!File.Exists(createRequest.FullPath))
                    throw new Exception($"The file {createRequest.FullPath} not exists.");

                createRequest.InternalType = RequestType.CREATE;
                UploadFileInternal(createRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Create)}\" with hub info failed.");
                return false;
            }
        }

        public bool Update(string contentName, string fullpath = null, string newContentName = null, string description = null)
        {
            try
            {
                var updateRequest = GetFirstSharedContent(contentName);
                if (updateRequest == null)
                    throw new Exception($"The content name {contentName} was not found.");

                updateRequest.FullPath = fullpath;
                updateRequest.NewContentName = newContentName;
                updateRequest.Description = description;
                updateRequest.References.Clear();
                Update(updateRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Update)}\" failed.");
                return false;
            }
        }

        public bool Update(Guid sharedId, string fullpath = null, string newContentName = null, string description = null)
        {
            try
            {
                var updateRequest = GetSharedContent(sharedId);
                if (updateRequest == null)
                    throw new Exception($"The content id {sharedId} was not found.");

                updateRequest.FullPath = fullpath;
                updateRequest.NewContentName = newContentName;
                updateRequest.Description = description;
                Update(updateRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Update)}\" with shared id failed.");
                return false;
            }
        }

        public bool Update(HubInfo updateRequest)
        {
            try
            {
                updateRequest.InternalType = RequestType.UPDATE;
                updateRequest.CreatedDate = updateRequest.ModifiedDate;
                UploadFileInternal(updateRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(Update)}\" with hub info failed.");
                return false;
            }
        }
        #endregion
    }

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

    public enum RequestType
    {
        CREATE,
        UPDATE,
        DELETE
    }

    public class HubInfo
    {
        [JsonIgnore]
        public RequestType InternalType { get; set; }

        [JsonIgnore]
        public string FullPath { get; set; }

        [JsonIgnore]
        public string NewContentName { get; set; }

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
}