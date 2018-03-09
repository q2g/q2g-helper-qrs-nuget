#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Q2gHelperQrs
{
    #region Usings
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using NLog;
    using SerApi;
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
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    #endregion

    public class QlikQrsHub
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        public Uri ConnectUri { get; private set; }
        public Cookie ConnectCookie { get; private set; }
        private Uri SharedContentUri = null;
        #endregion

        #region Constructor
        public QlikQrsHub(Uri connectUri, Cookie cookie)
        {
            ConnectUri = connectUri;
            ConnectCookie = cookie;
            SharedContentUri = new Uri(ConnectUri, $"{ConnectUri.AbsolutePath}/qrs/sharedcontent");
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

        private async Task<string> SendRequestAsync(Uri requestUri, HttpMethod method, string contentType, 
                                                    byte[] data = null, string filter = null, string orderby = null)
        {
            try
            {
                var key = GetRandomAlphanumericString(16);
                var keyRelativeUri = BuildUriWithKey(requestUri, key, filter, orderby);
                logger.Debug($"ConnectUri: {keyRelativeUri}");
                var connectionHandler = new HttpClientHandler();
                connectionHandler.CookieContainer.Add(ConnectUri, ConnectCookie);
                var httpClient = new HttpClient(connectionHandler) { BaseAddress = ConnectUri };
                var request = new HttpRequestMessage(method, keyRelativeUri);
                request.Headers.Add("X-Qlik-Xrfkey", key);
                if (data != null)
                {
                    request.Content = new ByteArrayContent(data);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                var result = httpClient.SendAsync(request).Result;
                if (result.IsSuccessStatusCode)
                {
                    logger.Debug($"Response: {result.StatusCode} - {result.RequestMessage}");
                    return await result.Content.ReadAsStringAsync();
                }
                    
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SendRequestAsync)}\" failed.");
                return null;
            }
        }

        private async Task<bool> UploadFileInternalAsync(HubInfo request)
        {
            try
            {
                if (request == null)
                {
                    logger.Debug("The request is null.");
                    return false;
                }

                if (!File.Exists(request.FullPath))
                {
                    logger.Debug($"The Document {request.FullPath} not exists.");
                    return false;
                }

                string result = String.Empty;
                Guid contentId = Guid.Empty;
                logger.Debug($"Upload type {request.InternalType}");

                var httpMethod = HttpMethod.Post;
                var uriString = SharedContentUri;
                if (request.InternalType == RequestType.UPDATE)
                {
                    httpMethod = HttpMethod.Put;
                    uriString = new Uri($"{SharedContentUri.OriginalString}/{request.Id.Value}");
                }

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                var jsonStr = JsonConvert.SerializeObject(request, settings);
                var data = Encoding.UTF8.GetBytes(jsonStr);
                result = await SendRequestAsync(uriString, httpMethod, "application/json", data);
                var hubInfo = JsonConvert.DeserializeObject<HubInfo>(result);

                //Upload File
                var fileData = File.ReadAllBytes(request.FullPath);
                var contentType = $"application/{Path.GetExtension(request.FullPath).TrimStart('.')}";
                var path = Path.GetFileName(request.FullPath);
                var newUploadUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{hubInfo.Id.Value}/uploadfile?externalpath={path}");
                await SendRequestAsync(newUploadUri, HttpMethod.Post, contentType, fileData);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "");
                return false;
            }
        }
        #endregion

        #region Public Methods
        public async Task<HubInfo> GetSharedContentByNameAsync(string contentName)
        {
            try
            {
                var result = await GetSharedContentAsync($"Name eq '{contentName}'");
                return result.FirstOrDefault() ?? null;
            }
            catch (Exception ex)
            {
                throw new Exception("The content could not be determined.", ex);
            }
        }

        public async Task<HubInfo> GetSharedContentByIdAsync(Guid sharedId)
        {
            try
            {
                var result = await GetSharedContentAsync($"Id eq {sharedId.ToString()}");
                return result.SingleOrDefault() ?? null;
            }
            catch (Exception ex)
            {
                throw new Exception("The content could not be determined.", ex);
            }
        }

        public async Task<List<HubInfo>> GetSharedContentAsync(string filter = null, string orderby = null)
        {
            try
            {
                var newUri = new Uri($"{SharedContentUri.OriginalString}/full");
                var result = await SendRequestAsync(newUri, HttpMethod.Get, "application/json", null, filter, orderby);
                return JsonConvert.DeserializeObject<List<HubInfo>>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentAsync)}\" failed.");
                return null;
            }
        }

        public async Task<int> GetSharedContentCountAsync(string filter = null)
        {
            try
            {
                var newUri = new Uri($"{SharedContentUri.OriginalString}/count");
                var result = await SendRequestAsync(newUri, HttpMethod.Get, "application/json", null, filter);
                var count = JsonConvert.DeserializeObject<JToken>(result).Value<int>("value");
                logger.Debug($"SharedContentCount: {count}");
                return count;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetSharedContentCountAsync)}\" failed.");
                return 0;
            }
        }

        public async Task<bool> CreateSharedContentAsync(string contentName, string fullpath, string description = null)
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

                await UploadFileInternalAsync(createRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(CreateSharedContentAsync)}\" with id failed.");
                return false;
            }
        }

        public async Task<bool> CreateSharedContentAsync(HubInfo createRequest)
        {
            try
            {
                if (!File.Exists(createRequest.FullPath))
                    throw new Exception($"The file {createRequest.FullPath} not exists.");

                createRequest.InternalType = RequestType.CREATE;
                await UploadFileInternalAsync(createRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(CreateSharedContentAsync)}\" with hub info failed.");
                return false;
            }
        }

        public async Task<bool> UpdateSharedContentByNameAsync(string contentName, string fullpath = null, string newContentName = null, string description = null)
        {
            try
            {
                var updateRequest = await GetSharedContentByNameAsync(contentName);
                if (updateRequest == null)
                    throw new Exception($"The content name {contentName} was not found.");

                updateRequest.FullPath = fullpath;
                updateRequest.NewContentName = newContentName;
                updateRequest.Description = description;
                updateRequest.References.Clear();
                return await UpdateSharedContentAsync(updateRequest);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(UpdateSharedContentByNameAsync)}\" failed.");
                return false;
            }
        }

        public async Task<bool> UpdateSharedContentByIdAsync(Guid sharedId, string fullpath = null, string newContentName = null, string description = null)
        {
            try
            {
                var updateRequest = await GetSharedContentByIdAsync(sharedId);
                if (updateRequest == null)
                    throw new Exception($"The content id {sharedId} was not found.");

                updateRequest.FullPath = fullpath;
                updateRequest.NewContentName = newContentName;
                updateRequest.Description = description;
                return await UpdateSharedContentAsync(updateRequest);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(UpdateSharedContentByIdAsync)}\" with shared id failed.");
                return false;
            }
        }

        public async Task<bool> UpdateSharedContentAsync(HubInfo updateRequest)
        {
            try
            {
                updateRequest.InternalType = RequestType.UPDATE;
                updateRequest.CreatedDate = updateRequest.ModifiedDate;
                await UploadFileInternalAsync(updateRequest);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(UpdateSharedContentAsync)}\" with hub info failed.");
                return false;
            }
        }

        public async Task<bool> DeleteAllSharedContentAsync()
        {
            try
            {
                var sharedInfos = await GetSharedContentAsync();
                foreach (var sharedInfo in sharedInfos)
                    await DeleteSharedContentByIdAsync(sharedInfo.Id.Value);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteAllSharedContentAsync)}\" failed.");
                return false;
            }
        }

        public async Task<bool> DeleteSharedContentByNameAsync(string contentName)
        {
            try
            {
                var firstHubInfo = await GetSharedContentByNameAsync(contentName);
                var newUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{firstHubInfo.Id.Value}");
                var result = await SendRequestAsync(newUri, HttpMethod.Delete, null, null);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteSharedContentByNameAsync)}\" with content name failed.");
                return false;
            }
        }

        public async Task<bool> DeleteSharedContentByIdAsync(Guid id)
        {
            try
            {
                var newUri = new Uri(ConnectUri, $"/qrs/sharedcontent/{id}");
                var result = await SendRequestAsync(newUri, HttpMethod.Delete, null, null);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(DeleteSharedContentByIdAsync)}\" with id failed.");
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